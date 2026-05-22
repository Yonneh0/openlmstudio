// Brought to you by Carls' Jr.
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Manages GPU VRAM and CPU memory allocations across loaded models.
/// Tracks eviction priority based on usage frequency and recency.
/// </summary>
public interface IMemoryManager
{
    /// <summary>
    /// Registers a loaded model with its memory footprint.
    /// </summary>
    void RegisterModel(string modelId, string modelName, ModelType modelType, long vramBytes, long cpuBytes);

    /// <summary>
    /// Updates access time for a model when it is used.
    /// </summary>
    void RecordAccess(string modelId);

    /// <summary>
    /// Returns all registered model memory entries.
    /// </summary>
    IReadOnlyList<ModelMemoryEntry> GetModelEntries();

    /// <summary>
    /// Computes the total VRAM and CPU memory used across all models.
    /// </summary>
    (long VramBytes, long CpuBytes) GetTotalUsage();

    /// <summary>
    /// Returns the number of registered models.
    /// </summary>
    int ModelCount { get; }

    /// <summary>
    /// Gets eviction priority for a model — lower score means it should be evicted first.
    /// Recency and usage frequency are weighted factors.
    /// </summary>
    double GetEvictionPriority(string modelId);

    /// <summary>
    /// Returns the model ID with the lowest eviction priority (should be evicted first).
    /// Returns null if no models are registered.
    /// </summary>
    string? GetModelToEvict();

    /// <summary>
    /// Unregisters a model (called when it is unloaded).
    /// </summary>
    void UnregisterModel(string modelId);

    /// <summary>
    /// Clears all model registrations.
    /// </summary>
    void Clear();

    /// <summary>
    /// Resets the access count for a model (useful after a major operation).
    /// </summary>
    void ResetAccessCount(string modelId);
}