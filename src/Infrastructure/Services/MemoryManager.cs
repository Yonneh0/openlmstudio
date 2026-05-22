// Brought to you by Carls' Jr.
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Tracks GPU VRAM and CPU memory allocations across loaded models, manages model eviction
/// based on usage frequency and recency, and provides memory status queries.
/// </summary>
public class MemoryManager : IMemoryManager
{
    private readonly ILogger<MemoryManager>? _logger;
    private readonly object _lock = new();
    private readonly Dictionary<string, ModelMemoryEntry> _modelEntries = new();

    // Eviction scores: models with older last-access times are evicted first.
    private const double WeightRecency = 0.7;
    private const double WeightUsageFrequency = 0.3;

    public MemoryManager(ILogger<MemoryManager>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Registers a loaded model with its memory footprint.
    /// </summary>
    public void RegisterModel(string modelId, string modelName, ModelType modelType, long vramBytes, long cpuBytes)
    {
        lock (_lock)
        {
            var entry = new ModelMemoryEntry(
                modelId, modelName, modelType, vramBytes, cpuBytes,
                DateTime.UtcNow, 1);
            _modelEntries[modelId] = entry;
        }
        _logger?.LogDebug("Registered model '{Model}' ({ModelType}) — VRAM: {Vram} bytes, CPU: {Cpu} bytes",
            modelName, modelType, vramBytes, cpuBytes);
    }

    /// <summary>
    /// Updates access time for a model when it is used.
    /// </summary>
    public void RecordAccess(string modelId)
    {
        lock (_lock)
        {
            if (_modelEntries.TryGetValue(modelId, out var entry))
            {
                _modelEntries[modelId] = entry with
                {
                    LastAccessed = DateTime.UtcNow,
                    AccessCount = entry.AccessCount + 1
                };
            }
        }
    }

    /// <summary>
    /// Returns all registered model memory entries.
    /// </summary>
    public IReadOnlyList<ModelMemoryEntry> GetModelEntries()
    {
        lock (_lock)
            return _modelEntries.Values.ToList().AsReadOnly();
    }

    /// <summary>
    /// Computes the total VRAM and CPU memory used across all models.
    /// </summary>
    public (long VramBytes, long CpuBytes) GetTotalUsage()
    {
        lock (_lock)
        {
            long vram = _modelEntries.Values.Sum(e => e.VramBytes);
            long cpu = _modelEntries.Values.Sum(e => e.CpuBytes);
            return (vram, cpu);
        }
    }

    /// <summary>
    /// Returns the number of registered models.
    /// </summary>
    public int ModelCount => _modelEntries.Count;

    /// <summary>
    /// Gets eviction priority for a model — lower score means it should be evicted first.
    /// Recency and usage frequency are weighted factors.
    /// </summary>
    public double GetEvictionPriority(string modelId)
    {
        lock (_lock)
        {
            if (!_modelEntries.TryGetValue(modelId, out var entry))
                return 0;

            var age = (DateTime.UtcNow - entry.LastAccessed).TotalSeconds;
            // Normalize age to 0..1 (use a cap of 1 hour)
            var normalizedAge = Math.Min(1.0, age / 3600.0);
            // Normalize access count to 0..1 (use a cap of 100 accesses)
            var normalizedAccess = Math.Min(1.0, entry.AccessCount / 100.0);

            var score = (1.0 - normalizedAge) * WeightRecency + (1.0 - normalizedAccess) * WeightUsageFrequency;
            return score;
        }
    }

    /// <summary>
    /// Returns the model ID with the lowest eviction priority (should be evicted first).
    /// Returns null if no models are registered.
    /// </summary>
    public string? GetModelToEvict()
    {
        lock (_lock)
        {
            if (_modelEntries.Count == 0)
                return null;

            return _modelEntries
                .Where(e => e.Value.ModelType != ModelType.TextGeneration) // Never evict the primary text model
                .OrderBy(e => GetEvictionPriority(e.Key))
                .Select(e => e.Key)
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// Unregisters a model (called when it is unloaded).
    /// </summary>
    public void UnregisterModel(string modelId)
    {
        lock (_lock)
        {
            _modelEntries.Remove(modelId);
            _logger?.LogDebug("Unregistered model '{ModelId}'", modelId);
        }
    }

    /// <summary>
    /// Clears all model registrations.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _modelEntries.Clear();
        }
    }

    /// <summary>
    /// Resets the access count for a model (useful after a major operation).
    /// </summary>
    public void ResetAccessCount(string modelId)
    {
        lock (_lock)
        {
            if (_modelEntries.TryGetValue(modelId, out var entry))
            {
                _modelEntries[modelId] = entry with { AccessCount = 0 };
            }
        }
    }
}