using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models.ContextCompression;
using OpenLMStudio.Domain.Models.LLamaCpp;
using AppEngineType = OpenLMStudio.Application.Interfaces.EngineType;
using DomainEngineType = OpenLMStudio.Domain.Models.ContextCompression.EngineType;
using DomainLogLevel = OpenLMStudio.Domain.Models.ContextCompression.LogLevel;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Manages the SystemAI (system-level AI) lifecycle:
/// engine binary download → model loading → server start → streaming.
/// </summary>
public class SystemAIManager : IDisposable
{
    private readonly ILogger<SystemAIManager> _logger;
    private readonly EngineBinaryDownloader _binaryDownloader;
    private readonly BinaryRegistry _binaryRegistry;
    private readonly LlamaServerHelpParser _helpParser;
    private readonly LogViewerService _logViewer;
    private readonly IEngineLogger _engineLogger;
    private readonly ModelRecommendationService _recommendationService;
    private readonly GgufModelDownloader _modelDownloader;
    private readonly EngineConfigService _configService;
    private readonly object _lock = new();
    private Process? _serverProcess;
    private bool _disposed;
    private string? _currentModelPath;
    private string? _currentBinaryPath;
    private BackendType _currentBackend = BackendType.Cpu;
    private RecommendedSettings? _currentSettings;
    private AppEngineType _engineId = AppEngineType.SystemAI;

    public event EventHandler<SystemAIStateChanged>? StateChanged;
    public event EventHandler<LogEntry>? LogEntryReceived;

    public SystemAIManager(
        ILogger<SystemAIManager> logger,
        EngineBinaryDownloader binaryDownloader,
        BinaryRegistry binaryRegistry,
        LlamaServerHelpParser helpParser,
        LogViewerService logViewer,
        IEngineLogger engineLogger,
        ModelRecommendationService recommendationService,
        GgufModelDownloader modelDownloader,
        EngineConfigService configService)
    {
        _logger = logger;
        _binaryDownloader = binaryDownloader;
        _binaryRegistry = binaryRegistry;
        _helpParser = helpParser;
        _logViewer = logViewer;
        _engineLogger = engineLogger;
        _recommendationService = recommendationService;
        _modelDownloader = modelDownloader;
        _configService = configService;
    }

    public SystemAIState State
    {
        get
        {
            lock (_lock)
            {
                if (_serverProcess == null) return SystemAIState.Stopped;
                if (_serverProcess.HasExited) return SystemAIState.Stopped;
                return _currentModelPath != null ? SystemAIState.Running : SystemAIState.Idle;
            }
        }
    }

    public string? CurrentModelPath => _currentModelPath;
    public string? CurrentBinaryPath => _currentBinaryPath;
    public BackendType CurrentBackend => _currentBackend;
    public RecommendedSettings? CurrentSettings => _currentSettings;

    public async Task<bool> StartAsync(string modelPath, BackendType? backend = null)
    {
        lock (_lock)
        {
            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                _logger.LogInformation("SystemAI is already running, stopping first");
                Stop();
            }
        }

        _currentModelPath = modelPath;
        _currentBackend = backend ?? InferBackend();

        var modelInfo = new GgufModelInfo(
            Id: Path.GetFileNameWithoutExtension(modelPath),
            Name: Path.GetFileNameWithoutExtension(modelPath),
            Architecture: null,
            Quantization: null,
            ContextLength: null,
            EmbeddingDim: null,
            FileSizeBytes: new FileInfo(modelPath).Length,
            FilePath: modelPath,
            ChatTemplate: null,
            Description: null,
            LastUsed: null,
            UsageCount: null);
        _currentSettings = _recommendationService.GetRecommendation(modelInfo).Settings;

        var binaryPath = await DownloadEngineAsync(_currentBackend).ConfigureAwait(false);
        _currentBinaryPath = binaryPath;

        var success = await StartServerAsync(binaryPath, modelPath).ConfigureAwait(false);
        if (success)
        {
            _logger.LogInformation("SystemAI started with model: {Model}", modelPath);
            StateChanged?.Invoke(this, new SystemAIStateChanged(SystemAIState.Running, modelPath));
        }
        return success;
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_serverProcess == null) return;
            try
            {
                _serverProcess.Kill();
                _serverProcess.WaitForExit(5000);
            }
            catch { /* ignore */ }
            finally
            {
                _serverProcess?.Dispose();
                _serverProcess = null;
            }
        }
        _currentModelPath = null;
        _currentBinaryPath = null;
        _currentSettings = null;
        _logger.LogInformation("SystemAI stopped");
        StateChanged?.Invoke(this, new SystemAIStateChanged(SystemAIState.Stopped, null));
    }

    public async Task<bool> SwitchModelAsync(string modelPath, BackendType? backend = null)
    {
        _currentModelPath = modelPath;
        _currentBackend = backend ?? _currentBackend;

        var modelInfo = new GgufModelInfo(
            Id: Path.GetFileNameWithoutExtension(modelPath),
            Name: Path.GetFileNameWithoutExtension(modelPath),
            Architecture: null,
            Quantization: null,
            ContextLength: null,
            EmbeddingDim: null,
            FileSizeBytes: new FileInfo(modelPath).Length,
            FilePath: modelPath,
            ChatTemplate: null,
            Description: null,
            LastUsed: null,
            UsageCount: null);
        _currentSettings = _recommendationService.GetRecommendation(modelInfo).Settings;

        var binaryPath = await DownloadEngineAsync(_currentBackend).ConfigureAwait(false);
        _currentBinaryPath = binaryPath;

        Stop();
        var success = await StartServerAsync(binaryPath, modelPath).ConfigureAwait(false);
        if (success)
        {
            _logger.LogInformation("SystemAI switched to model: {Model}", modelPath);
            StateChanged?.Invoke(this, new SystemAIStateChanged(SystemAIState.Running, modelPath));
        }
        return success;
    }

    public RecommendedSettings GetRecommendedSettings(string modelPath)
    {
        var modelInfo = new GgufModelInfo(
            Id: Path.GetFileNameWithoutExtension(modelPath),
            Name: Path.GetFileNameWithoutExtension(modelPath),
            Architecture: null,
            Quantization: null,
            ContextLength: null,
            EmbeddingDim: null,
            FileSizeBytes: new FileInfo(modelPath).Length,
            FilePath: modelPath,
            ChatTemplate: null,
            Description: null,
            LastUsed: null,
            UsageCount: null);
        return _recommendationService.GetRecommendation(modelInfo).Settings;
    }

    public async Task<HelpSetting[]> GetAvailableSettingsAsync()
    {
        return await _helpParser.GetSettingsAsync(_currentBinaryPath).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    private async Task<string> DownloadEngineAsync(BackendType backend)
    {
        try
        {
            var path = await _binaryDownloader.DownloadForBackendAsync(backend).ConfigureAwait(false);
            _logger.LogInformation("SystemAI engine binary ready: {Path}", path);
            return path;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download SystemAI engine binary for {Backend}, falling back to PATH", backend);
            return "llama-server";
        }
    }

    private async Task<bool> StartServerAsync(string binaryPath, string modelPath)
    {
        await _engineLogger.StartSessionAsync(_engineId).ConfigureAwait(false);

        var args = BuildServerArgs(binaryPath, modelPath);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = binaryPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var proc = Process.Start(psi);
            if (proc == null)
            {
                _logger.LogError("Failed to start SystemAI server process");
                return false;
            }

            proc.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _engineLogger.HandleEngineStdout(_engineId, e.Data);
                    var level = InferLogLevel(e.Data);
                    var logEntry = new LogEntry(
                        Id: Guid.NewGuid().ToString(),
                        Timestamp: DateTime.UtcNow,
                        Level: level,
                        Message: e.Data,
                        Source: (DomainEngineType)(int)_engineId,
                        IsImportant: IsImportantMessage(e.Data));
                    _logViewer.AddLogEntry(_engineId, level, e.Data, IsImportantMessage(e.Data));
                    LogEntryReceived?.Invoke(this, logEntry);
                }
            };

            proc.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _engineLogger.HandleEngineStderr(_engineId, e.Data);
                    var logEntry = new LogEntry(
                        Id: Guid.NewGuid().ToString(),
                        Timestamp: DateTime.UtcNow,
                        Level: DomainLogLevel.Warn,
                        Message: $"stderr: {e.Data}",
                        Source: (DomainEngineType)(int)_engineId,
                        IsImportant: false);
                    _logViewer.AddLogEntry(_engineId, DomainLogLevel.Warn, $"stderr: {e.Data}");
                    LogEntryReceived?.Invoke(this, logEntry);
                }
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            var ready = await WaitForServerReady().ConfigureAwait(false);
            if (ready)
            {
                _serverProcess = proc;
                _logger.LogInformation("SystemAI server ready on port {Port}", 8082);
                return true;
            }
            else
            {
                proc.Kill();
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SystemAI server");
            return false;
        }
    }

    private string BuildServerArgs(string binaryPath, string modelPath)
    {
        var settings = _currentSettings ?? GetRecommendedSettings(modelPath);
        var args = $"--mlock -m \"{modelPath}\" --port 8082";

        args += $" --ngl {settings.GpuLayers}";
        args += $" --ctx-size {settings.ContextSize}";
        args += $" --batch-size {settings.BatchSize}";
        args += $" --threads {settings.Threads}";
        args += $" --threads-batch {settings.Threads}";

        if (settings.FlashAttention)
            args += " --flash-attn";

        if (settings.KvOffload)
            args += " --kv-offload";

        if (settings.Mmap)
            args += " --mmap";

        if (settings.Mlock)
            args += " --mlock";

        if (settings.Embedding)
            args += " --embedding";

        if (settings.Reranking)
            args += " --reranking";

        if (settings.Pooling != null)
            args += $" --pooling {settings.Pooling}";

        return args;
    }

    private static BackendType InferBackend()
    {
        if (File.Exists(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenLMStudio", "engines", "cuda", "llama-server-cuda")))
            return BackendType.Cuda;

        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
            return BackendType.Metal;

        return BackendType.Cpu;
    }

    private static async Task<bool> WaitForServerReady()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await client.GetAsync("http://127.0.0.1:8082/health").ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                    return true;
            }
            catch { /* server not ready yet */ }
            await Task.Delay(500).ConfigureAwait(false);
        }
        return false;
    }

    private static DomainLogLevel InferLogLevel(string line)
    {
        if (line.Contains("error", StringComparison.OrdinalIgnoreCase)) return DomainLogLevel.Error;
        if (line.Contains("warn", StringComparison.OrdinalIgnoreCase)) return DomainLogLevel.Warn;
        if (line.Contains("info", StringComparison.OrdinalIgnoreCase)) return DomainLogLevel.Info;
        if (line.Contains("debug", StringComparison.OrdinalIgnoreCase)) return DomainLogLevel.Debug;
        return DomainLogLevel.Info;
    }

    private static bool IsImportantMessage(string line)
    {
        return line.Contains("loaded", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("model", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("gpu", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("error", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("kv", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// State of the SystemAI engine.
/// </summary>
public enum SystemAIState
{
    Stopped,
    Idle,
    Starting,
    Running,
    Stopping,
    Error
}

/// <summary>
/// Event args for SystemAI state changes.
/// </summary>
public record SystemAIStateChanged(SystemAIState NewState, string? ModelPath);