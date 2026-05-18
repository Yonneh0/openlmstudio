using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Unified interface for loading and managing AI models across different types (text, image, embedding, etc.).
/// Provides a common API for model lifecycle management regardless of the underlying inference engine.
/// </summary>
public interface IModelLoader : IDisposable
{
    /// <summary>
    /// Gets the type of model this loader handles.
    /// </summary>
    ModelType SupportedModelType { get; }

    /// <summary>
    /// Gets whether a model is currently loaded in memory.
    /// </summary>
    bool IsLoaded { get; }

    /// <summary>
    /// Loads the specified model into memory and prepares it for inference.
    /// </summary>
    /// <param name="modelId">The unique identifier of the model to load.</param>
    /// <param name="cancellationToken">Cancellation token for the loading operation.</param>
    Task<bool> LoadModelAsync(string modelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unloads the currently loaded model from memory.
    /// </summary>
    Task UnloadModelAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the metadata for a specific model without loading it into memory.
    /// </summary>
    Task<ModelMetadata?> GetModelMetadataAsync(string modelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all available models that this loader can handle.
    /// </summary>
    Task<IEnumerable<ModelMetadata>> ListAvailableModelsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a context length configuration for text generation models.
/// </summary>
public record ContextLengthConfig(
    int DefaultContextLength,
    int MaxContextLength,
    int BatchSize,
    double? MemoryBudgetRatio = null)
{
    public int EffectiveMaxTokens => MaxContextLength;
}

/// <summary>
/// Configuration options for model loading across all engine types.
/// </summary>
public record ModelLoadingConfig(
    ContextLengthConfig? TextGeneration = null,
    ImageGenParams? ImageGeneration = null,
    bool PreferGpuOnnx = false)
{
    public ImageGenParams EffectiveImageParams => ImageGeneration ?? new();
}

/// <summary>
/// Configuration for image generation parameters (resolution, steps, CFG scale, seed).
/// </summary>
public record ImageGenParams(
    int Resolution = 1024,
    int Steps = 30,
    double CfgScale = 7.5,
    long? DefaultSeed = null)
{
    public long EffectiveSeed => DefaultSeed ?? (long)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % int.MaxValue);
}

/// <summary>
/// Manages the lifecycle of loaded models across all engine types and enforces memory limits.
/// Provides concurrent model support with automatic eviction when memory budget is exceeded.
/// </summary>
public interface IModelManager : IDisposable
{
    /// <summary>
    /// Gets the currently loaded text generation model, if any.
    /// </summary>
    IModelLoader? ActiveTextGeneration { get; }

    /// <summary>
    /// Gets the currently loaded image generation model, if any.
    /// </summary>
    IModelLoader? ActiveImageGeneration { get; }

    /// <summary>
    /// Gets the currently loaded embedding model, if any.
    /// </summary>
    IModelLoader? ActiveEmbedding { get; }

    /// <summary>
    /// Gets the currently loaded VAE model, if any.
    /// </summary>
    IModelLoader? ActiveVae { get; }

    /// <summary>
    /// Gets all active (loaded) models across all types.
    /// </summary>
    IReadOnlyDictionary<ModelType, IModelLoader> ActiveModels { get; }

    /// <summary>
    /// Gets the number of currently loaded models.
    /// </summary>
    int LoadedCount { get; }

    /// <summary>
    /// Gets the total VRAM/memory usage across all loaded models in bytes (approximate).
    /// Returns -1 if unable to determine memory usage.
    /// </summary>
    long EstimatedMemoryUsageBytes { get; }

    /// <summary>
    /// Loads a model of the specified type into memory, evicting another model if necessary.
    /// </summary>
    /// <param name="modelId">The unique identifier of the model to load.</param>
    /// <param name="modelType">The type of model to load.</param>
    /// <param name="cancellationToken">Cancellation token for the loading operation.</param>
    Task LoadModelAsync(string modelId, ModelType modelType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unloads a specific model by its ID.
    /// </summary>
    Task UnloadModelByIdAsync(string modelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unloads all currently loaded models.
    /// </summary>
    Task UnloadAllModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a new model loader for the specified type.
    /// Called during DI initialization to register type-specific loaders.
    /// </summary>
    void RegisterLoader(IModelLoader loader);

    /// <summary>
    /// Gets the available VRAM/memory across all GPUs and CPU memory.
    /// Used by eviction policy when deciding which model to unload.
    /// </summary>
    Task<DeviceMemoryInfo> GetAvailableMemoryAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents device memory information for model eviction decisions.
/// </summary>
public record DeviceMemoryInfo(
    long GpuVramTotalBytes,
    long GpuVramFreeBytes,
    long CpuMemoryTotalBytes,
    long CpuMemoryFreeBytes)
{
    /// <summary>
    /// Gets the total available memory across all devices in bytes.
    /// </summary>
    public long TotalAvailable => GpuVramFreeBytes + CpuMemoryFreeBytes;

    /// <summary>
    /// Whether there is sufficient VRAM to load a model of the given size.
    /// Returns true if any GPU has enough free VRAM or CPU memory can be used as fallback.
    /// </summary>
    public bool HasSufficientVramFor(long modelSizeBytes) =>
        GpuVramFreeBytes >= modelSizeBytes || CpuMemoryFreeBytes >= modelSizeBytes;

    /// <summary>
    /// Whether GPU VRAM is available for this device type.
    /// </summary>
    public bool HasGpuVram => GpuVramFreeBytes > 0;
}