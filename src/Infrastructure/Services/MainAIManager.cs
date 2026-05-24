using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models.LLamaCpp;
using DomainLogLevel = OpenLMStudio.Domain.Models.ContextCompression.LogLevel;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Manages the MainAI (primary text generation) lifecycle:
/// engine binary download → model loading → server start → streaming.
/// </summary>
public class MainAIManager : IDisposable
{
    private readonly ILogger<MainAIManager> _logger;
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
    private EngineType _engineId = EngineType.Primary;

    public event EventHandler<MainAIStateChanged>? StateChanged;
    public event EventHandler<LogEntry>? LogEntryReceived;

    public MainAIManager(
        ILogger<MainAIManager> logger,
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

    public MainAIState State
    {
        get
        {
            lock (_lock)
            {
                if (_serverProcess == null) return MainAIState.Stopped;
                if (_serverProcess.HasExited) return MainAIState.Stopped;
                return _currentModelPath != null ? MainAIState.Running : MainAIState.Idle;
            }
        }
    }

    public string? CurrentModelPath => _currentModelPath;
    public string? CurrentBinaryPath => _currentBinaryPath;
    public BackendType CurrentBackend => _currentBackend;
    public RecommendedSettings? CurrentSettings => _currentSettings;

    /// <summary>
    /// Starts MainAI with the given GGUF model. Downloads the engine binary if needed.
    /// </summary>
    public async Task<bool> StartAsync(string modelPath, BackendType? backend = null)
    {
        lock (_lock)
        {
            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                _logger.LogInformation("MainAI is already running, stopping first");
                Stop();
            }
        }

        _currentModelPath = modelPath;
        _currentBackend = backend ?? InferBackend();

        // Get recommendation for this model
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

        // Download engine binary
        var binaryPath = await DownloadEngineAsync(_currentBackend).ConfigureAwait(false);
        _currentBinaryPath = binaryPath;

        // Start the server
        var success = await StartServerAsync(binaryPath, modelPath).ConfigureAwait(false);
        if (success)
        {
            _logger.LogInformation("MainAI started with model: {Model}", modelPath);
            StateChanged?.Invoke(this, new MainAIStateChanged(State, modelPath));
        }
        return success;
    }

    /// <summary>
    /// Stops MainAI and cleans up resources.
    /// </summary>
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
        _logger.LogInformation("MainAI stopped");
        StateChanged?.Invoke(this, new MainAIStateChanged(MainAIState.Stopped, null));
    }

    /// <summary>
    /// Switches to a different model without restarting the server.
    /// </summary>
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

        // Download engine binary if needed
        var binaryPath = await DownloadEngineAsync(_currentBackend).ConfigureAwait(false);
        _currentBinaryPath = binaryPath;

        // Restart server with new model
        Stop();
        var success = await StartServerAsync(binaryPath, modelPath).ConfigureAwait(false);
        if (success)
        {
            _logger.LogInformation("MainAI switched to model: {Model}", modelPath);
            StateChanged?.Invoke(this, new MainAIStateChanged(MainAIState.Running, modelPath));
        }
        return success;
    }

    /// <summary>
    /// Gets the recommended settings for a model.
    /// </summary>
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

    /// <summary>
    /// Gets all available settings from llama-server --help.
    /// </summary>
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
            _logger.LogInformation("Engine binary ready: {Path}", path);
            return path;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download engine binary for {Backend}, falling back to PATH", backend);
            return "llama-server";
        }
    }

    private async Task<bool> StartServerAsync(string binaryPath, string modelPath)
    {
        // Start engine logging session
        await _engineLogger.StartSessionAsync(_engineId).ConfigureAwait(false);

        // Build args from current settings
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
                _logger.LogError("Failed to start llama-server process");
                return false;
            }

            proc.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _engineLogger.HandleEngineStdout(_engineId, e.Data);
                    // Parse and add to LogViewerService
                    var level = InferLogLevel(e.Data);
                    _logViewer.AddLogEntry(_engineId, level, e.Data, IsImportantMessage(e.Data));
                }
            };

            proc.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _engineLogger.HandleEngineStderr(_engineId, e.Data);
                    _logViewer.AddLogEntry(_engineId, DomainLogLevel.Warn, $"stderr: {e.Data}");
                }
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            // Wait for server to be ready
            var ready = await WaitForServerReady(binaryPath).ConfigureAwait(false);
            if (ready)
            {
                _serverProcess = proc;
                _logger.LogInformation("MainAI server ready on port {Port}", 8081);
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
            _logger.LogError(ex, "Failed to start MainAI server");
            return false;
        }
    }

    private string BuildServerArgs(string binaryPath, string modelPath)
    {
        var settings = _currentSettings ?? GetRecommendedSettings(modelPath);
        var args = $"--mlock -m \"{modelPath}\" --port 8081";

        // GPU settings
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
        // Check for CUDA
        if (File.Exists(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenLMStudio", "engines", "cuda", "llama-server-cuda")))
            return BackendType.Cuda;

        // Check for Metal (macOS)
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
            return BackendType.Metal;

        return BackendType.Cpu;
    }

    private static async Task<bool> WaitForServerReady(string binaryPath)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await client.GetAsync("http://127.0.0.1:8081/health").ConfigureAwait(false);
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
/// State of the MainAI engine.
/// </summary>
public enum MainAIState
{
    Stopped,
    Idle,
    Starting,
    Running,
    Stopping,
    Error
}

/// <summary>
/// Event args for MainAI state changes.
/// </summary>
public record MainAIStateChanged(MainAIState NewState, string? ModelPath);