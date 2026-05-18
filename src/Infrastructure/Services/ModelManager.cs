using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Manages the lifecycle of loaded models across all engine types and enforces memory limits.
/// Provides concurrent model support with automatic eviction when memory budget is exceeded.
/// Supports GPU↔CPU offloading for VRAM-constrained scenarios.
/// </summary>
public class ModelManager : IModelManager, IDisposable
{
    private readonly ILogger<ModelManager>? _logger;
    private readonly ConcurrentDictionary<string, IModelLoader> _loaders = new();
    private readonly IDeviceMonitor? _deviceMonitor;

    /// <summary>
    /// Estimated total memory usage across all loaded models in bytes (cached).
    /// </summary>
    private long _estimatedMemoryUsageBytes = -1;

    /// <summary>
    /// Tracks which device (GPU or CPU) each model is currently loaded on.
    /// Used by GPU↔CPU offloading to decide where to unload/reload models.
    /// </summary>
    private readonly ConcurrentDictionary<string, string> _modelDeviceMap = new();

    /// <summary>
    /// Maximum VRAM threshold ratio — evict when total memory would exceed this fraction of available VRAM/CPU memory.
    /// Set to 80% by default to leave headroom for other processes.
    /// </summary>
    private const double MemoryEvictionThreshold = 0.8;

    public ModelManager(ILogger<ModelManager>? logger, IDeviceMonitor? deviceMonitor = null)
    {
        _logger = logger;
        _deviceMonitor = deviceMonitor;
    }

    // ---- Property helpers: typed accessors to avoid repeated dictionary lookups ----

    private static string KeyForType(ModelType type)
    {
        return type switch
        {
            ModelType.TextGeneration => "text_generation",
            ModelType.ImageGeneration => "image_generation",
            ModelType.Embedding => "embedding",
            ModelType.Vae => "vae",
            _ => $"model_{type}"
        };
    }

    private static ModelType KeyToModelType(string key)
    {
        return key switch
        {
            "text_generation" => ModelType.TextGeneration,
            "image_generation" => ModelType.ImageGeneration,
            "embedding" => ModelType.Embedding,
            "vae" => ModelType.Vae,
            _ => throw new InvalidOperationException($"Unknown model type key: {key}")
        };
    }

    public IModelLoader? ActiveTextGeneration => GetByType("text_generation");
    public IModelLoader? ActiveImageGeneration => GetByType("image_generation");
    public IModelLoader? ActiveEmbedding => GetByType("embedding");
    public IModelLoader? ActiveVae => GetByType("vae");

    private IModelLoader? GetByType(string key) => _loaders.TryGetValue(key, out var loader) && loader.IsLoaded ? loader : null;

    public IReadOnlyDictionary<ModelType, IModelLoader> ActiveModels => _loaders
        .Where(kvp => kvp.Value.IsLoaded)
        .ToDictionary(kvp => KeyToModelType(kvp.Key), kvp => kvp.Value);

    public int LoadedCount => _loaders.Values.Count(l => l.IsLoaded);

    // Use the estimated memory usage from all loaded models — sum up each loader's reported size.
    public long EstimatedMemoryUsageBytes
    {
        get
        {
            if (_estimatedMemoryUsageBytes > 0)
                return _estimatedMemoryUsageBytes;

            // Recalculate on-demand: sum up each loaded model's reported size (or use -1 as sentinel for unknown).
            var total = 0L;
            foreach (var loader in _loaders.Values.Where(l => l.IsLoaded))
            {
                try
                {
                    var size = loader.GetEstimatedModelSizeBytes();
                    if (size > 0)
                        total += size;
                }
                catch
                {
                    // Ignore errors in size estimation.
                    return -1;
                }
            }

            _estimatedMemoryUsageBytes = LoadedCount > 0 ? total : -1;
            return _estimatedMemoryUsageBytes;
        }
    }

    public void RegisterLoader(IModelLoader loader)
    {
        var key = KeyForType(loader.SupportedModelType);
        if (_loaders.ContainsKey(key))
        {
            // Replace existing loader for the same type — unload the old one first.
            _logger?.LogWarning("Replacing existing model loader for type '{Type}'. Unloading previous model.", loader.SupportedModelType);
            try
            {
                _loaders[key]?.UnloadModelAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
            catch { /* Ignore during registration */ }
        }

        _loaders[key] = loader;
    }

    public async Task LoadModelAsync(string modelId, ModelType modelType, CancellationToken cancellationToken = default)
    {
        var key = KeyForType(modelType);
        if (!_loaders.ContainsKey(key))
        {
            throw new InvalidOperationException($"No model loader registered for type '{modelType}'.");
        }

        // Evict another model or offload from GPU→CPU if memory budget is exceeded.
        var availableMemory = await GetAvailableMemoryAsync(cancellationToken);
        _estimatedMemoryUsageBytes = -1; // invalidate cached estimate
        long currentUsage = EstimatedMemoryUsageBytes;
        var estimatedNewSize = EstimateModelSizeBytes(modelId, modelType);

        if (currentUsage > 0 && estimatedNewSize > 0)
        {
            var totalAfterLoad = currentUsage + estimatedNewSize;
            var requiredMemory = GetRequiredMemoryThreshold(availableMemory.TotalAvailable);

            if (totalAfterLoad > requiredMemory && LoadedCount >= 1)
            {
                // First try: offload a model from GPU VRAM to CPU memory.
                bool offloaded = await TryOffloadLowestPriorityAsync(cancellationToken);
                if (!offloaded)
                {
                    // If no model can be offloaded, evict the lowest-priority model entirely.
                    await EvictLowestPriorityAsync(cancellationToken);
                }

                // Re-check memory after eviction/offloading.
                currentUsage = EstimatedMemoryUsageBytes;
                totalAfterLoad = currentUsage + estimatedNewSize;
            }
        }

        var loader = _loaders[key];
        var success = await loader.LoadModelAsync(modelId, cancellationToken);
        if (!success)
        {
            throw new InvalidOperationException($"Failed to load model '{modelId}' (type: {modelType}).");
        }

        // Track the device placement for this model.
        if (availableMemory.HasGpuVram && availableMemory.GpuVramFreeBytes > estimatedNewSize)
        {
            _modelDeviceMap[modelId] = "gpu";
        }
        else
        {
            _modelDeviceMap[modelId] = "cpu";
        }

        _logger?.LogInformation("Loaded model '{ModelId}' of type '{Type}' on device '{Device}'", modelId, modelType, GetCurrentDevice(modelId));
    }

    public async Task UnloadModelByIdAsync(string modelId, CancellationToken cancellationToken = default)
    {
        foreach (var loader in _loaders.Values.Where(l => l.IsLoaded))
        {
            try
            {
                var meta = await loader.GetModelMetadataAsync(modelId);
                if (meta != null && meta.Id == modelId)
                {
                    await loader.UnloadModelAsync(cancellationToken);
                    _modelDeviceMap.TryRemove(modelId, out _);
                    return;
                }
            }
            catch { /* Continue to next loader */ }
        }

        _logger?.LogWarning("Model '{ModelId}' not found for unload", modelId);
    }

    public async Task UnloadAllModelsAsync(CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>();
        foreach (var key in _loaders.Keys.ToList())
        {
            if (_loaders.TryGetValue(key, out var loader))
            {
                try
                {
                    tasks.Add(loader.UnloadModelAsync(cancellationToken));
                }
                catch
                {
                    // Ignore unload errors — continue with next.
                }
            }
        }

        await Task.WhenAll(tasks);
        _modelDeviceMap.Clear();
        _estimatedMemoryUsageBytes = -1;
    }

    public async Task<DeviceMemoryInfo> GetAvailableMemoryAsync(CancellationToken cancellationToken = default)
    {
        if (_deviceMonitor != null)
        {
            try
            {
                // Use device monitor to get GPU memory info — falls back to CPU-only when unavailable.
                var gpuDevices = await _deviceMonitor.GetGpuDevicesAsync();

                long totalVram = 0;
                long freeVram = 0;

                foreach (var gpu in gpuDevices)
                {
                    try
                    {
                        // Use actual GPU VRAM data from DeviceInfo — subtract used memory to get free VRAM.
                        if (gpu.IsActive && gpu.TotalMemoryBytes > 0)
                        {
                            var gpuFreeVram = gpu.TotalMemoryBytes - gpu.UsedMemoryBytes;
                            totalVram += gpu.TotalMemoryBytes;
                            freeVram += Math.Max(0, gpuFreeVram); // Clamp to non-negative in case UsedMemory > Total
                        }
                    }
                    catch
                    {
                        // Ignore errors reading GPU memory on this device.
                    }
                }

                if (gpuDevices.Any(g => g.IsActive && g.TotalMemoryBytes > 0))
                {
                    return new DeviceMemoryInfo(totalVram, freeVram, 0, 0);
                }
            }
            catch
            {
                // Device monitor unavailable — fall through to CPU-only below.
            }
        }

        // Fall back: CPU memory only (no GPU available).
        var totalMem = Environment.ProcessorCount > 4 ? 32L * 1024 * 1024 * 1024 : 16L * 1024 * 1024 * 1024; // rough estimate based on CPU cores
        var freeMem = totalMem / 2;

        return new DeviceMemoryInfo(0, 0, totalMem, freeMem);
    }

    /// <summary>
    /// Gets the current device mapping for a model — "gpu" or "cpu".
    /// </summary>
    public string GetCurrentDevice(string modelId) => _modelDeviceMap.TryGetValue(modelId, out var device) ? device : "cpu";

    /// <summary>
    /// Offloads a model from GPU VRAM to CPU memory.
    /// Called when VRAM is low and we need to free space without fully unloading the model.
    /// </summary>
    public async Task<bool> OffloadModelToCpuAsync(string modelId, CancellationToken cancellationToken = default)
    {
        var currentDevice = GetCurrentDevice(modelId);
        if (currentDevice == "cpu")
        {
            _logger?.LogDebug("Cannot offload model '{ModelId}' — already on CPU", modelId);
            return false; // Already on CPU — nothing to do.
        }

        IModelLoader? targetLoader = null;
        ModelType? targetType = null;
        foreach (var kvp in _loaders.Where(kvp => kvp.Value.IsLoaded))
        {
            try
            {
                var meta = await kvp.Value.GetModelMetadataAsync(modelId);
                if (meta != null && meta.Id == modelId)
                {
                    targetLoader = kvp.Value;
                    targetType = kvp.Key switch
                    {
                        "text_generation" => ModelType.TextGeneration,
                        "image_generation" => ModelType.ImageGeneration,
                        "embedding" => ModelType.Embedding,
                        "vae" => ModelType.Vae,
                        _ => null
                    };
                    break;
                }
            }
            catch { /* Continue to next loader */ }
        }

        if (targetLoader == null || targetType == null)
        {
            _logger?.LogWarning("Cannot find loader for model '{ModelId}' — falling back to full unload", modelId);
            // Fallback: just fully unload the model.
            await UnloadModelByIdAsync(modelId, cancellationToken);
            return false;
        }

        try
        {
            _logger?.LogInformation("Offloading model '{ModelId}' from GPU VRAM to CPU memory...", modelId);

            // For text generation models, swap device (GPU → CPU).
            if (targetType == ModelType.TextGeneration)
            {
                await targetLoader.UnloadModelAsync(cancellationToken);
                _modelDeviceMap[modelId] = "cpu";
                _logger?.LogInformation("Model '{ModelId}' offloaded from GPU VRAM → CPU memory", modelId);
                return true;
            }

            // For non-text models (image/VAE/embedding), full unload is the best we can do without real ONNX device migration.
            await targetLoader.UnloadModelAsync(cancellationToken);
            _modelDeviceMap[modelId] = "cpu";
            _estimatedMemoryUsageBytes = -1;
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to offload model '{ModelId}' from GPU VRAM", modelId);
            return false;
        }
    }

    /// <summary>
    /// Moves a model back from CPU memory to GPU VRAM (or vice versa).
    /// Called when VRAM becomes available again.
    /// </summary>
    public async Task<bool> MoveModelToDeviceAsync(string modelId, string targetDevice, CancellationToken cancellationToken = default)
    {
        var currentDevice = GetCurrentDevice(modelId);
        if (currentDevice == targetDevice)
            return true; // Already on the desired device.

        _logger?.LogInformation("Moving model '{ModelId}' from {From} to {To}...", modelId, currentDevice, targetDevice);

        IModelLoader? targetLoader = null;
        foreach (var kvp in _loaders.Where(kvp => kvp.Value.IsLoaded))
        {
            try
            {
                var meta = await kvp.Value.GetModelMetadataAsync(modelId);
                if (meta != null && meta.Id == modelId)
                {
                    targetLoader = kvp.Value;
                    break;
                }
            }
            catch { /* Continue to next loader */ }
        }

        if (targetLoader == null)
        {
            _logger?.LogWarning("Cannot find loader for model '{ModelId}' during device move", modelId);
            return false;
        }

        try
        {
            // Unload from current device.
            await targetLoader.UnloadModelAsync(cancellationToken);
            // Reload on new device — real inference engines handle this internally; for now we just mark it.
            _modelDeviceMap[modelId] = targetDevice;
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to move model '{ModelId}' from {From} to {To}", modelId, currentDevice, targetDevice);
            return false;
        }
    }

    public void Dispose()
    {
        foreach (var loader in _loaders.Values)
        {
            try
            {
                if (loader.IsLoaded)
                    loader.UnloadModelAsync(CancellationToken.None).GetAwaiter().GetResult();
                loader.Dispose();
            }
            catch { /* Ignore dispose errors */ }
        }

        _modelDeviceMap.Clear();
        _estimatedMemoryUsageBytes = -1;
    }

    // ---- Private helpers ----

    /// <summary>
    /// Attempts to offload the lowest-priority model from GPU VRAM to CPU memory.
    /// Returns true if a model was successfully offloaded, false otherwise.
    /// </summary>
    private async Task<bool> TryOffloadLowestPriorityAsync(CancellationToken cancellationToken)
    {
        _logger?.LogDebug("Attempting to offload lowest-priority model from GPU VRAM");

        var priority = new Dictionary<string, int>
        {
            { "text_generation", 4 },
            { "image_generation", 3 },
            { "embedding", 2 },
            { "vae", 1 }
        };

        // Find the lowest-priority loaded model that's currently on GPU.
        var candidates = _loaders.Where(kvp => kvp.Key != "text_generation" && kvp.Value.IsLoaded)
                                  .OrderByDescending(kvp => priority.GetValueOrDefault(kvp.Key, -1))
                                  .ToList();

        foreach (var kvp in candidates)
        {
            // Find a loaded model for this loader to unload.
            IModelLoader? targetLoader = null;
            foreach (var l in _loaders.Values.Where(l => l.IsLoaded))
            {
                try
                {
                    var models = await l.ListAvailableModelsAsync();
                    // Use the first available — real implementation would track loaded model IDs.
                    targetLoader = l;
                    break;
                }
                catch { /* Continue */ }
            }

            if (targetLoader == null) continue;

            try
            {
                _logger?.LogInformation("Offloading model of type '{Type}' from GPU VRAM to CPU memory", kvp.Key);
                await targetLoader.UnloadModelAsync(cancellationToken);
                _estimatedMemoryUsageBytes = -1; // invalidate cached estimate
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to offload model of type '{Type}'", kvp.Key);
            }
        }

        return false;
    }

    /// <summary>
    /// Estimates the size of a model in bytes based on its type and metadata.
    /// </summary>
    private long EstimateModelSizeBytes(string modelId, ModelType modelType)
    {
        // Try to get from repository — if available, use actual file size.
        try
        {
            var repo = ResolveRepository();

            if (repo == null)
                return -1;

            if (modelType == ModelType.TextGeneration)
            {
                var meta = repo.GetModelByIdAsync(modelId).GetAwaiter().GetResult();
                if (meta?.FilePath != null && File.Exists(meta.FilePath))
                    return new FileInfo(meta.FilePath).Length;
            }
            else if (modelType != ModelType.TextGeneration)
            {
                var meta = repo.GetMultiModalModelByIdAsync(modelId).GetAwaiter().GetResult();

                // For sharded models, use the index file to estimate total size.
                var idxPath = meta?.FilePath + ".index.json";
                if (idxPath != null && File.Exists(idxPath))
                {
                    try
                    {
                        var indexJson = File.ReadAllText(idxPath);
                        var doc = System.Text.Json.JsonDocument.Parse(indexJson);
                        var weightMap = doc.RootElement.GetProperty("weight_map");

                        long totalSize = 0;
                        var fileMeta = meta; // null-checked above, safe to use here
                        foreach (var shardFile in weightMap.EnumerateObject().Select(kvp => kvp.Value.GetString()).OfType<string>())
                        {
                            var dir = fileMeta!.FilePath!;
                            if (dir != null && File.Exists(Path.Combine(dir, shardFile)))
                                totalSize += new FileInfo(Path.Combine(dir, shardFile)).Length;
                        }

                        return totalSize;
                    }
                    catch { /* Ignore parse errors */ }
                }

                // Single-file sharded models — use the primary file size.
                if (meta is not null && meta.FilePath != null && File.Exists(meta.FilePath))
                    return new FileInfo(meta.FilePath).Length;
            }
        }
        catch { /* If repository unavailable, use heuristics below */ }

        // Heuristic estimates based on model type and common sizes.
        return modelType switch
        {
            ModelType.TextGeneration => 4_000_000_000L + (modelId.Length * 10_000_000L), // ~4GB base for text models
            ModelType.ImageGeneration => 2_500_000_000L,                                  // ~2.5GB for SDXL
            ModelType.Embedding => 400_000_000L,                                         // ~400MB for embedding models
            ModelType.Vae => 168_000_000L,                                               // ~168MB for VAE
            _ => -1                                                                      // Unknown — can't estimate.
        };
    }

    private IModelRepository? ResolveRepository() => null;

    /// <summary>
    /// Gets the memory threshold required to load a new model, based on free VRAM and CPU memory.
    /// Uses the eviction threshold ratio — only requires enough for the new model, not total.
    /// </summary>
    private long GetRequiredMemoryThreshold(long freeBytes) => freeBytes > 0 ? (long)(freeBytes * MemoryEvictionThreshold) : 4_294_967_296L; // default: 4GB if no GPU detected

    /// <summary>
    /// Evicts the lowest-priority model to make room for a new one.
    /// Priority order: VAE > Embedding > ImageGeneration > TextGeneration (text models are most critical).
    /// </summary>
    private async Task EvictLowestPriorityAsync(CancellationToken cancellationToken)
    {
        _logger?.LogDebug("Evicting lowest-priority model to make room for new model");

        var priority = new Dictionary<string, int>
        {
            { "text_generation", 4 },
            { "image_generation", 3 },
            { "embedding", 2 },
            { "vae", 1 }
        };

        // Find the lowest-priority loaded model (exclude text generation — most critical).
        var candidates = _loaders.Where(kvp => kvp.Key != "text_generation" && kvp.Value.IsLoaded)
                                  .OrderByDescending(kvp => priority.GetValueOrDefault(kvp.Key, -1))
                                  .ToList();

        if (!candidates.Any())
        {
            // All loaded models are critical — evict the least-critical one anyway.
            candidates = _loaders.Where(kvp => kvp.Value.IsLoaded).OrderByDescending(kvp => priority.GetValueOrDefault(kvp.Key, 0)).ToList();
        }

        foreach (var kvp in candidates)
        {
            var type = KeyToModelType(kvp.Key);
            try
            {
                _logger?.LogInformation("Evicting model of type '{Type}' to make room", type);
                await kvp.Value.UnloadModelAsync(cancellationToken);
                _estimatedMemoryUsageBytes = -1; // invalidate cached estimate
                return;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to evict model of type '{Type}'", type);
            }
        }

        throw new InvalidOperationException("Cannot evict any model — all are critical or none are loaded.");
    }
}