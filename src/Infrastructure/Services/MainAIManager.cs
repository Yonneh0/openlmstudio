using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models.ContextCompression;
using OpenLMStudio.Domain.Models.LLamaCpp;
using AppEngineType = OpenLMStudio.Application.Interfaces.EngineType;
using DomainEngineType = OpenLMStudio.Domain.Models.ContextCompression.EngineType;
using DomainLogLevel = OpenLMStudio.Domain.Models.ContextCompression.LogLevel;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Represents a loaded MainAI model with its associated llama-server process.
/// </summary>
public record MainAIModelSlot(
    string ModelPath,
    string BinaryPath,
    int Port,
    BackendType Backend,
    Process ServerProcess,
    RecommendedSettings Settings,
    string Id);

/// <summary>
/// Manages the MainAI (primary text generation) lifecycle with multi-model support:
/// engine binary download → model loading → server start → streaming.
///
/// Each loaded model runs on its own llama-server instance with a unique port
/// allocated from the range starting at 4200.
/// </summary>
public class MainAIManager : IDisposable
{
    private const int InitialPort = 4200;
    private const int MaxPort = 4400;

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
    private bool _disposed;
    private int _nextPort = InitialPort;
    private readonly List<MainAIModelSlot> _loadedModels = new();
    private string? _activeModelId;

    public event EventHandler<MainAIStateChanged>? StateChanged;
    public event EventHandler<LogEntry>? LogEntryReceived;

    /// <summary>
    /// The currently active model's ID.
    /// </summary>
    public string? ActiveModelId => _activeModelId;

    /// <summary>
    /// The currently active model's path.
    /// </summary>
    public string? ActiveModelPath => _loadedModels.FirstOrDefault(m => m.Id == _activeModelId)?.ModelPath;

    /// <summary>
    /// All loaded MainAI models.
    /// </summary>
    public IReadOnlyList<MainAIModelSlot> LoadedModels => _loadedModels.AsReadOnly();

    /// <summary>
    /// The state of the active model.
    /// </summary>
    public MainAIState State
    {
        get
        {
            lock (_lock)
            {
                var active = _loadedModels.FirstOrDefault(m => m.Id == _activeModelId);
                if (active is null || active.ServerProcess is null || active.ServerProcess.HasExited) return MainAIState.Stopped;
                return active.ModelPath != null ? MainAIState.Running : MainAIState.Idle;
            }
        }
    }

    public string? CurrentModelPath => ActiveModelPath;
    public string? CurrentBinaryPath => _loadedModels.FirstOrDefault(m => m.Id == _activeModelId)?.BinaryPath;
    public BackendType CurrentBackend => _loadedModels.FirstOrDefault(m => m.Id == _activeModelId)?.Backend ?? BackendType.Cpu;
    public RecommendedSettings? CurrentSettings => _loadedModels.FirstOrDefault(m => m.Id == _activeModelId)?.Settings;

    /// <summary>
    /// Initializes a new instance of <see cref="MainAIManager"/>.
    /// </summary>
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

    /// <summary>
    /// Loads a GGUF model into MainAI. Downloads the engine binary if needed.
    /// Each call creates a new llama-server instance on a unique port.
    /// </summary>
    /// <param name="modelPath">Path to the GGUF model file.</param>
    /// <param name="backend">The backend to use (CPU, CUDA, Metal, Vulkan). Auto-detected if null.</param>
    /// <returns>True if the model was loaded successfully.</returns>
    public async Task<bool> LoadModelAsync(string modelPath, BackendType? backend = null)
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

        var settings = _recommendationService.GetRecommendation(modelInfo).Settings;
        var port = AllocatePort();
        var binaryPath = await DownloadEngineAsync(backend ?? InferBackend()).ConfigureAwait(false);

        var serverProcess = await StartServerAsync(binaryPath, modelPath, port, settings).ConfigureAwait(false);
        if (serverProcess != null)
        {
            var slot = new MainAIModelSlot(
                ModelPath: modelPath,
                BinaryPath: binaryPath,
                Port: port,
                Backend: backend ?? InferBackend(),
                ServerProcess: serverProcess,
                Settings: settings,
                Id: Guid.NewGuid().ToString("N"));

            lock (_lock)
            {
                _loadedModels.Add(slot);
                _activeModelId = slot.Id;
            }

            _logger.LogInformation("MainAI loaded model: {Model} on port {Port}", modelPath, port);
            StateChanged?.Invoke(this, new MainAIStateChanged(MainAIState.Running, modelPath));
            return true;
        }

        return false;
    }

    /// <summary>
    /// Switches the active model to a different loaded model.
    /// </summary>
    /// <param name="modelId">The ID of the model to activate.</param>
    public void SwitchActiveModel(string modelId)
    {
        lock (_lock)
        {
            var slot = _loadedModels.FirstOrDefault(m => m.Id == modelId);
            if (slot is null || slot.ServerProcess is null || slot.ServerProcess.HasExited)
            {
                _logger.LogWarning("Cannot switch to model {ModelId} — process has exited", modelId);
                return;
            }

            _activeModelId = modelId;
            _logger.LogInformation("MainAI switched to model: {Model}", slot.ModelPath);
            StateChanged?.Invoke(this, new MainAIStateChanged(MainAIState.Running, slot.ModelPath));
        }
    }

    /// <summary>
    /// Unloads a specific model by ID.
    /// </summary>
    /// <param name="modelId">The ID of the model to unload.</param>
    public void UnloadModel(string modelId)
    {
        lock (_lock)
        {
            var index = _loadedModels.FindIndex(m => m.Id == modelId);
            if (index == -1) return;

            var slot = _loadedModels[index];
            try
            {
                slot.ServerProcess?.Kill();
                slot.ServerProcess?.WaitForExit(5000);
                slot.ServerProcess?.Dispose();
            }
            catch { /* ignore kill errors */ }

            _loadedModels.RemoveAt(index);

            // If this was the active model, pick the next one
            if (_activeModelId == modelId && _loadedModels.Count > 0)
            {
                _activeModelId = _loadedModels.Last().Id;
            }
            else if (_loadedModels.Count == 0)
            {
                _activeModelId = null;
            }

            _logger.LogInformation("MainAI unloaded model: {Model}", slot.ModelPath);
            StateChanged?.Invoke(this, new MainAIStateChanged(MainAIState.Stopped, slot.ModelPath));
        }
    }

    /// <summary>
    /// Stops the active model only.
    /// </summary>
    public void StopActiveModel()
    {
        lock (_lock)
        {
            var active = _loadedModels.FirstOrDefault(m => m.Id == _activeModelId);
            if (active is null || active.ServerProcess is null) return;

            try
            {
                active.ServerProcess.Kill();
                active.ServerProcess.WaitForExit(5000);
            }
            catch { /* ignore */ }
            finally
            {
                active.ServerProcess?.Dispose();
            }

            _loadedModels.Remove(active);

            if (_loadedModels.Count > 0)
            {
                _activeModelId = _loadedModels.Last().Id;
                StateChanged?.Invoke(this, new MainAIStateChanged(MainAIState.Running, active.ModelPath));
            }
            else
            {
                _activeModelId = null;
                StateChanged?.Invoke(this, new MainAIStateChanged(MainAIState.Stopped, null));
            }

            _logger.LogInformation("MainAI stopped active model: {Model}", active.ModelPath);
        }
    }

    /// <summary>
    /// Stops all loaded models and cleans up resources.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            foreach (var slot in _loadedModels)
            {
                try
                {
                    slot.ServerProcess?.Kill();
                    slot.ServerProcess?.WaitForExit(5000);
                    slot.ServerProcess?.Dispose();
                }
                catch { /* ignore */ }
            }

            _loadedModels.Clear();
            _activeModelId = null;
        }

        _logger.LogInformation("MainAI stopped all models");
        StateChanged?.Invoke(this, new MainAIStateChanged(MainAIState.Stopped, null));
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
    /// Gets all available settings from llama-server --help.
    /// </summary>
    public async Task<HelpSetting[]> GetAvailableSettingsAsync()
    {
        var activeBinary = CurrentBinaryPath;
        if (string.IsNullOrEmpty(activeBinary))
            return Array.Empty<HelpSetting>();

        return await _helpParser.GetSettingsAsync(activeBinary).ConfigureAwait(false);
    }

    /// <summary>
    /// Saves the given settings to disk via the configuration service.
    /// </summary>
    public async Task SaveSettings(RecommendedSettings settings)
    {
        var modelPath = ActiveModelPath ?? "local-model";
        var activePort = LoadedModels.FirstOrDefault(m => m.Id == _activeModelId)?.Port ?? 4200;

        // Store settings in the config with proper field mappings
        // Note: EngineConfig stores temperature/top-p but we use them for GPU layers/context size
        var config = new EngineConfig(
            ModelPath: modelPath,
            Port: activePort,
            Temperature: settings.GpuLayers,
            TopP: settings.ContextSize,
            RecommendedBackend: CurrentBackend.ToString(),
            LastDownloadedBackend: null);

        await _configService.SaveAsync(config).ConfigureAwait(false);
        _logger.LogInformation("Saved engine settings: GPU={GpuLayers}, Ctx={Ctx}, Batch={Batch}, Threads={Threads}",
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
            _logger.LogInformation("Engine binary ready: {Path}", path);
            return path;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download engine binary for {Backend}, falling back to PATH", backend);
            return "llama-server";
        }
    }

    private async Task<Process?> StartServerAsync(string binaryPath, string modelPath, int port, RecommendedSettings settings)
    {
        var engineId = (AppEngineType)(int)CurrentBackend;
        await _engineLogger.StartSessionAsync(engineId).ConfigureAwait(false);

        var args = BuildServerArgs(binaryPath, modelPath, port, settings);

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
                _logger.LogError("Failed to start llama-server process on port {Port}", port);
                return null;
            }

            proc.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _engineLogger.HandleEngineStdout(engineId, e.Data);
                    var level = InferLogLevel(e.Data);
                    var logEntry = new LogEntry(
                        Id: Guid.NewGuid().ToString(),
                        Timestamp: DateTime.UtcNow,
                        Level: level,
                        Message: e.Data,
                        Source: (DomainEngineType)(int)engineId,
                        IsImportant: IsImportantMessage(e.Data));
                    _logViewer.AddLogEntry(engineId, level, e.Data, IsImportantMessage(e.Data));
                    LogEntryReceived?.Invoke(this, logEntry);
                }
            };

            proc.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _engineLogger.HandleEngineStderr(engineId, e.Data);
                    var logEntry = new LogEntry(
                        Id: Guid.NewGuid().ToString(),
                        Timestamp: DateTime.UtcNow,
                        Level: DomainLogLevel.Warn,
                        Message: $"stderr: {e.Data}",
                        Source: (DomainEngineType)(int)engineId,
                        IsImportant: false);
                    _logViewer.AddLogEntry(engineId, DomainLogLevel.Warn, $"stderr: {e.Data}");
                    LogEntryReceived?.Invoke(this, logEntry);
                }
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            var ready = await WaitForServerReady(port).ConfigureAwait(false);
            if (ready)
            {
                _logger.LogInformation("MainAI server ready on port {Port}", port);
                return proc;
            }
            else
            {
                proc.Kill();
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start MainAI server on port {Port}", port);
            return null;
        }
    }

    private string BuildServerArgs(string binaryPath, string modelPath, int port, RecommendedSettings settings)
    {
        // Use --port and only add --mlock once (from settings, not hardcoded)
        var args = $"-m \"{modelPath}\" --port {port}";

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

    private int AllocatePort()
    {
        lock (_lock)
        {
            var port = _nextPort;
            _nextPort++;
            if (_nextPort > MaxPort)
                _nextPort = InitialPort;

            // Ensure port isn't already in use
            while (_loadedModels.Any(m => m.Port == port))
            {
                port = _nextPort;
                _nextPort++;
                if (_nextPort > MaxPort)
                    _nextPort = InitialPort;
            }

            return port;
        }
    }

    private static async Task<bool> WaitForServerReady(int port)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await client.GetAsync($"http://127.0.0.1:{port}/health").ConfigureAwait(false);
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