using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Domain.Models;
using AppEngineType = OpenLMStudio.Application.Interfaces.EngineType;
using DomainEngineType = OpenLMStudio.Domain.Models.EngineType;
using DomainLogLevel = OpenLMStudio.Domain.Models.LogLevel;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Manages the SystemAI (system-level AI) lifecycle:
/// engine binary download → model loading → server start → streaming.
///
/// SystemAI runs a single model on a dedicated port (8082) for system orchestration tasks.
/// </summary>
public class SystemAIManager : IDisposable
{
    private const int DefaultPort = 8082;

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

    /// <summary>
    /// The current model's path.
    /// </summary>
    public string? CurrentModelPath => _currentModelPath;

    /// <summary>
    /// The current binary's path.
    /// </summary>
    public string? CurrentBinaryPath => _currentBinaryPath;

    /// <summary>
    /// The current backend type.
    /// </summary>
    public BackendType CurrentBackend => _currentBackend;

    /// <summary>
    /// The current recommended settings.
    /// </summary>
    public RecommendedSettings? CurrentSettings => _currentSettings;

    /// <summary>
    /// The current system AI state.
    /// </summary>
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

    /// <summary>
    /// Initializes a new instance of <see cref="SystemAIManager"/>.
    /// </summary>
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

    /// <summary>
    /// Starts SystemAI with the given GGUF model. Downloads the engine binary if needed.
    /// </summary>
    /// <param name="modelPath">Path to the GGUF model file.</param>
    /// <param name="backend">The backend to use. Auto-detected if null.</param>
    /// <returns>True if the model was loaded successfully.</returns>
    public async Task<bool> StartAsync(string modelPath, BackendType? backend = null)
    {
        return await StartAsync(modelPath, backend, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts SystemAI with the given GGUF model, with cancellation support.
    /// </summary>
    public async Task<bool> StartAsync(string modelPath, BackendType? backend, CancellationToken ct)
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
            Architecture: InferArchitecture(modelPath),
            Quantization: InferQuantization(modelPath),
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

    /// <summary>
    /// Stops SystemAI and cleans up resources.
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
        _logger.LogInformation("SystemAI stopped");
        StateChanged?.Invoke(this, new SystemAIStateChanged(SystemAIState.Stopped, null));
    }

    /// <summary>
    /// Switches to a different model without restarting the server.
    /// </summary>
    public async Task<bool> SwitchModelAsync(string modelPath, BackendType? backend = null)
    {
        return await SwitchModelAsync(modelPath, backend, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Switches to a different model without restarting the server, with cancellation support.
    /// </summary>
    public async Task<bool> SwitchModelAsync(string modelPath, BackendType? backend, CancellationToken ct)
    {
        _currentModelPath = modelPath;
        _currentBackend = backend ?? _currentBackend;

        var modelInfo = new GgufModelInfo(
            Id: Path.GetFileNameWithoutExtension(modelPath),
            Name: Path.GetFileNameWithoutExtension(modelPath),
            Architecture: InferArchitecture(modelPath),
            Quantization: InferQuantization(modelPath),
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

    /// <summary>
    /// Gets the recommended settings for a model.
    /// </summary>
    public RecommendedSettings GetRecommendedSettings(string modelPath)
    {
        var modelInfo = new GgufModelInfo(
            Id: Path.GetFileNameWithoutExtension(modelPath),
            Name: Path.GetFileNameWithoutExtension(modelPath),
            Architecture: InferArchitecture(modelPath),
            Quantization: InferQuantization(modelPath),
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
    /// Gets the current port SystemAI is using.
    /// </summary>
    public int CurrentPort => DefaultPort;

    /// <summary>
    /// Gets all available settings from llama-server --help.
    /// </summary>
    public async Task<HelpSetting[]> GetAvailableSettingsAsync()
    {
        if (string.IsNullOrEmpty(_currentBinaryPath))
            return Array.Empty<HelpSetting>();

        return await _helpParser.GetSettingsAsync(_currentBinaryPath).ConfigureAwait(false);
    }

    /// <summary>
    /// Saves the given settings to disk via the configuration service.
    /// </summary>
    public async Task SaveSettings(RecommendedSettings settings)
    {
        var modelPath = _currentModelPath ?? "local-model";
        var config = new EngineConfig(
            ModelPath: modelPath,
            Port: DefaultPort,
            Temperature: settings.GpuLayers,
            TopP: settings.ContextSize,
            RecommendedBackend: _currentBackend.ToString(),
            LastDownloadedBackend: null);

        await _configService.SaveAsync(config).ConfigureAwait(false);
        _logger.LogInformation("Saved SystemAI settings: GPU={GpuLayers}, Ctx={Ctx}, Batch={Batch}, Threads={Threads}",
            settings.GpuLayers, settings.ContextSize, settings.BatchSize, settings.Threads);
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
                _logger.LogInformation("SystemAI server ready on port {Port}", DefaultPort);
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
        var args = $"-m \"{modelPath}\" --port {DefaultPort}";

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
        // Check for CUDA binary in the cache directory
        var cudaPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenLMStudio", "engines", "cuda", "llama-server-cuda");

        if (File.Exists(cudaPath) || File.Exists(cudaPath + ".exe"))
            return BackendType.Cuda;

        // Check for Metal (macOS)
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
            return BackendType.Metal;

        return BackendType.Cpu;
    }

    private static string? InferArchitecture(string modelPath)
    {
        if (string.IsNullOrEmpty(modelPath)) return null;
        var name = Path.GetFileNameWithoutExtension(modelPath).ToLowerInvariant();

        if (name.Contains("llama")) return "llama";
        if (name.Contains("mistral")) return "mistral";
        if (name.Contains("phi")) return "phi";
        if (name.Contains("gemma")) return "gemma";
        if (name.Contains("qwen")) return "qwen";
        if (name.Contains("deepseek")) return "deepseek";
        return null;
    }

    private static string? InferQuantization(string modelPath)
    {
        if (string.IsNullOrEmpty(modelPath)) return null;
        var name = Path.GetFileNameWithoutExtension(modelPath).ToLowerInvariant();

        if (name.Contains("q8_0")) return "Q8_0";
        if (name.Contains("q6_")) return "Q6_K";
        if (name.Contains("q5_")) return "Q5_K_M";
        if (name.Contains("q4_")) return "Q4_K_M";
        if (name.Contains("q3_")) return "Q3_K_M";
        if (name.Contains("q2_")) return "Q2_K";
        if (name.Contains("bf16")) return "BF16";
        if (name.Contains("f16")) return "F16";
        if (name.Contains("f32")) return "F32";
        return null;
    }

    private static async Task<bool> WaitForServerReady()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await client.GetAsync($"http://127.0.0.1:{DefaultPort}/health").ConfigureAwait(false);
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
