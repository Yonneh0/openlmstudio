using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Orchestrates model loading with automatic fallback chain: GPU → CPU → degraded parameters.
/// When a model fails to load on the primary device due to OOM, it retries on secondary devices
/// and progressively degrades configuration (offload layers, lower precision).
/// </summary>
public class ModelLoadingFallbackService : IDisposable
{
    private readonly ILogger<ModelLoadingFallbackService> _logger;
    private readonly IChatCompletionService? _chatCompletionService;
    private readonly ConcurrentDictionary<string, LoadingStrategy> _loadingAttempts = new();

    public ModelLoadingFallbackService(ILogger<ModelLoadingFallbackService> logger, IChatCompletionService? chatCompletionService = null)
    {
        _logger = logger;
        _chatCompletionService = chatCompletionService;
    }

    /// <summary>
    /// Attempts to load a model with automatic fallback: GPU → CPU → degraded.
    /// Returns null if all strategies fail.
    /// Note: This returns a placeholder until Phase 2 IModelManager is wired up — the real implementation will return LoadedModelInstance.
    /// For now, it always triggers fallback chain and returns null (no real model loading).
    /// TODO: Implement real fallback chain that loads model on CPU when GPU fails.
    /// </summary>
    public async Task<Domain.Models.ModelMetadata?> LoadWithFallbackAsync(string modelId, DevicePreference initialDevice = DevicePreference.Gpu)
    {
        var attempts = GenerateLoadingAttemptSequence(initialDevice);

        foreach (var attempt in attempts)
        {
            try
            {
                _logger.LogInformation("Attempting to load model: {ModelId} — device: {Device}, offloadLayers: {OffloadLayers}, precision: {Precision}",
                    modelId, attempt.DevicePreference, attempt.OffloadLayers, attempt.Precision);

                // Try loading through the model manager (will be implemented in Phase 2)
                var result = await LoadModelDirectlyAsync(modelId, new ModelLoadOptions
                {
                    DevicePreference = attempt.DevicePreference,
                    OffloadLayers = attempt.OffloadLayers,
                    Precision = attempt.Precision
                });

                if (result != null)
                {
                    _loadingAttempts[modelId] = new LoadingStrategy(attempt.DevicePreference, attempt.OffloadLayers, attempt.Precision);
                    // NOTE: The real implementation will return a LoadedModelInstance when IChatCompletionService is wired up.
                    // For now, we don't have a model metadata object to return since no actual model was loaded.
                    return result;
                }

                _loadingAttempts[modelId] = new LoadingStrategy(attempt.DevicePreference, attempt.OffloadLayers, attempt.Precision);
            }
            catch (Exception ex) when (ex is OutOfMemoryException || (ex.InnerException != null && ex.InnerException.GetType().FullName?.Contains("Gpu") == true))
            {
                // OOM or GPU error — try next strategy in the fallback chain
                _logger.LogWarning(ex, "Model loading failed for device: {Device}, trying next fallback strategy", attempt.DevicePreference);
                // NOTE: result will always be null here since LoadModelDirectlyAsync returns null.
                _loadingAttempts[modelId] = new LoadingStrategy(attempt.DevicePreference, attempt.OffloadLayers, attempt.Precision);
            }
            catch (Exception ex) when ((ex is InvalidOperationException && ex.Message.Contains("CUDA")) || (ex.InnerException != null && ex.InnerException.GetType().FullName?.Contains("Cuda") == true))
            {
                // CUDA-specific error — try CPU fallback
                _logger.LogWarning(ex, "CUDA device unavailable for model: {ModelId}, trying CPU fallback", modelId);
                _loadingAttempts[modelId] = new LoadingStrategy(attempt.DevicePreference, attempt.OffloadLayers, attempt.Precision);
            }
        }

        _logger.LogError("All loading strategies failed for model: {ModelId}", modelId);
        return null;
    }

    /// <summary>
    /// Generates the sequence of loading attempts based on device preference.
    /// GPU → CPU with offload layers → degraded precision parameters.
    /// </summary>
    public IEnumerable<LoadingStrategy> GenerateLoadingAttemptSequence(DevicePreference initialDevice)
    {
        if (initialDevice == DevicePreference.Gpu)
        {
            yield return new LoadingStrategy(DevicePreference.Gpu, 0, Precision.Full);          // Full GPU load
            yield return new LoadingStrategy(DevicePreference.Gpu, -1, Precision.Half);           // GPU with partial offload + FP16
            yield return new LoadingStrategy(DevicePreference.Cpu, 0, Precision.Full);            // CPU load (no GPU at all)
            yield return new LoadingStrategy(DevicePreference.Cpu, 128, Precision.Half);          // CPU with offload layers + FP16
        }
        else
        {
            yield return new LoadingStrategy(DevicePreference.Cpu, 0, Precision.Full);            // Full CPU load
            yield return new LoadingStrategy(DevicePreference.Cpu, 128, Precision.Half);          // CPU with offload + FP16
            yield return new LoadingStrategy(DevicePreference.Gpu, -1, Precision.Half);           // GPU fallback with partial offload + FP16
        }
    }

    /// <summary>
    /// Gets the current loading attempt history for a model.
    /// </summary>
    public IReadOnlyList<LoadingStrategy> GetLoadingAttempts(string modelId)
    {
        return _loadingAttempts.TryGetValue(modelId, out var attempts)
            ? new[] { attempts } : Array.Empty<LoadingStrategy>();
    }

    private async Task<Domain.Models.ModelMetadata?> LoadModelDirectlyAsync(string modelId, ModelLoadOptions options)
    {
        _logger.LogDebug("Attempting direct load of model: {ModelId} with device: {Device}, precision: {Precision}", modelId, options.DevicePreference, options.Precision);

        // Use IChatCompletionService if available (llama.cpp backend).
        // The service handles GGUF model loading, KV cache allocation, and inference session setup.
        if (_chatCompletionService != null)
        {
            try
            {
                // Build a minimal context to request loading the model (the service will load into memory)
                var messages = new List<Message>
                {
                    new()
                    {
                        Role = MessageRole.User,
                        Content = "[load-test]",
                        TokenCount = 1
                    }
                };

                var chatRequest = new ChatRequest(
                    modelId,
                    messages,
                    Temperature: 0.7,
                    MaxTokens: 1, // Minimal request — just loading the model
                    TopP: 1.0);

                // The message doesn't carry model metadata — return null to trigger fallback
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "IChatCompletionService failed to load model {ModelId} with {Device} device", modelId, options.DevicePreference);
                // Return null to trigger fallback
                return null;
            }
        }

        // No chat completion service available — return null to trigger fallback chain
        _logger.LogDebug("No IChatCompletionService available for model load — triggering fallback");
        return null;
    }

    public void Dispose()
    {
        // No unmanaged resources to clean up
    }
}

/// <summary>
/// Represents a single attempt in the model loading fallback chain.
/// </summary>
public record LoadingStrategy(
    DevicePreference DevicePreference,
    int OffloadLayers,
    Precision Precision);

/// <summary>
/// Supported device preferences for model loading.
/// </summary>
public enum DevicePreference
{
    /// <summary>Prefer GPU acceleration (CUDA/Metal).</summary>
    Gpu,
    /// <summary>Force CPU-only execution.</summary>
    Cpu
}

/// <summary>
/// Model precision for inference.
/// </summary>
public enum Precision
{
    /// <summary>Full 32-bit floating point (highest accuracy).</summary>
    Full,
    /// <summary>Half-precision 16-bit floating point (reduced memory usage).</summary>
    Half
}

/// <summary>
/// Options for model loading passed to IModelManager.
/// </summary>
public class ModelLoadOptions
{
    public DevicePreference DevicePreference { get; set; } = DevicePreference.Gpu;
    /// <summary>Number of layers to offload to CPU. -1 means auto-optimal.</summary>
    public int OffloadLayers { get; init; }
    /// <summary>Precision mode for inference.</summary>
    public Precision Precision { get; set; } = Precision.Full;
}