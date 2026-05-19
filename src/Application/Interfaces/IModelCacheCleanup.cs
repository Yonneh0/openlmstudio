namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Retention policy for model cache cleanup.
/// </summary>
public enum ModelCacheRetentionPolicy
{
    /// <summary>Keep all models; no automatic cleanup.</summary>
    KeepAll,

    /// <summary>Remove models not used in the last N days.</summary>
    RemoveByLastUsed,

    /// <summary>Remove the oldest models until total size is under the limit.</summary>
    EvictBySize,

    /// <summary>Aggressive: remove unused models and models exceeding a size threshold.</summary>
    Aggressive
}

/// <summary>
/// Result of a model cache cleanup operation.
/// </summary>
public record ModelCacheCleanupResult(
    long BytesFreed,
    IReadOnlyList<string> RemovedModels);

/// <summary>
/// Interface for model cache maintenance operations.
/// </summary>
public interface IModelCacheCleanupService : IDisposable
{
    /// <summary>
    /// Performs model cache cleanup according to the specified policy.
    /// </summary>
    Task<ModelCacheCleanupResult> CleanAsync(ModelCacheRetentionPolicy policy, CancellationToken ct = default);

    /// <summary>
    /// Gets the total size of the model cache in bytes.
    /// </summary>
    Task<long> GetCacheSizeAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets a list of cached models with their sizes and last-used timestamps.
    /// </summary>
    Task<IReadOnlyList<CachedModelInfo>> GetCachedModelsAsync(CancellationToken ct = default);

    /// <summary>
    /// Manually removes a specific model from the cache.
    /// </summary>
    Task<bool> RemoveModelAsync(string modelId, CancellationToken ct = default);
}

/// <summary>
/// Information about a cached model file.
/// </summary>
public record CachedModelInfo(
    string ModelId,
    string FilePath,
    long FileSizeBytes,
    DateTimeOffset LastUsedUtc,
    DateTime CreatedUtc);