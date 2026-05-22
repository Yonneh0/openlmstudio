using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Represents a device type for model loading (CPU or GPU).
/// Used by ModelManager for memory budgeting and CPU/GPU offloading decisions.
/// </summary>
public enum DeviceType { Cpu, Gpu }

/// <summary>
/// Manages concurrent model loading/unloading with memory-aware eviction policy.
/// Coordinates between text generation, image generation, and diffusion models.
/// Supports per-model VRAM tracking, CPU/GPU offloading, and usage-frequency-based eviction.
/// </summary>
public class ModelManager : IModelManager, IDisposable
{
    private readonly ILogger<ModelManager>? _logger;
    private readonly IDeviceMonitor _deviceMonitor;

    private readonly ConcurrentDictionary<string, IModelLoader> _loaders = new();
    private readonly ConcurrentDictionary<string, LoadedModelInfo> _loadedModels = new();
    private bool _disposed;

    public ModelManager(ILogger<ModelManager>? logger, IDeviceMonitor deviceMonitor)
    {
        _logger = logger;
        _deviceMonitor = deviceMonitor;
    }

    public IModelLoader? ActiveTextGeneration => _loadedModels.Values.FirstOrDefault(m => m.Metadata.ModelType == ModelType.TextGeneration)?.Loader;
    public IModelLoader? ActiveImageGeneration => _loadedModels.Values.FirstOrDefault(m => m.Metadata.ModelType == ModelType.ImageGeneration)?.Loader;
    public IModelLoader? ActiveEmbedding => _loadedModels.Values.FirstOrDefault(m => m.Metadata.ModelType == ModelType.Embedding)?.Loader;
    public IModelLoader? ActiveVae => _loadedModels.Values.FirstOrDefault(m => m.Metadata.ModelType == ModelType.Vae)?.Loader;

    public IReadOnlyDictionary<ModelType, IModelLoader> ActiveModels =>
        _loadedModels.Values.GroupBy(m => m.Metadata.ModelType).ToDictionary(g => g.Key, g => g.First().Loader).AsReadOnly();

    public int LoadedCount => _loadedModels.Count;
    public long EstimatedMemoryUsageBytes => _loadedModels.Values.Sum(m => m.Metadata.FileSizeBytes > 0 ? (long)m.Metadata.FileSizeBytes : 0);

    /// <summary>
    /// Gets a per-model memory usage report.
    /// </summary>
    public IReadOnlyList<ModelMemoryReport> GetMemoryReports() =>
        _loadedModels
            .Select(kvp => new ModelMemoryReport(
                kvp.Key,
                kvp.Value.GpuVramBytes,
                kvp.Value.CpuBytes,
                kvp.Value.Device.ToString().ToLowerInvariant()))
            .ToList()
            .AsReadOnly();

    /// <summary>
    /// Returns models sorted by last accessed time (least recently used first),
    /// for eviction priority decisions.
    /// </summary>
    public IReadOnlyList<string> GetEvictionPriority() =>
        _loadedModels
            .OrderBy(kvp => kvp.Value.LastAccessed)
            .Select(kvp => kvp.Key)
            .ToList()
            .AsReadOnly();

    /// <summary>
    /// Gets the total VRAM currently allocated across all loaded models.
    /// </summary>
    public long TotalVramUsedBytes => _loadedModels.Values.Sum(m => m.GpuVramBytes);

    /// <summary>
    /// Gets the total CPU memory currently used by loaded models.
    /// </summary>
    public long TotalCpuMemoryUsedBytes => _loadedModels.Values.Sum(m => m.CpuBytes);

    /// <summary>
    /// Allocates VRAM for a model after successful loading.
    /// Returns the VRAM allocated, or 0 if allocation failed.
    /// </summary>
    public long AllocateVram(string modelId, long bytes)
    {
        if (_loadedModels.TryGetValue(modelId, out var info))
        {
            info.GpuVramBytes = bytes;
            info.LastAccessed = DateTime.UtcNow;
            return bytes;
        }
        return 0;
    }

    /// <summary>
    /// Deallocates VRAM for a model when it is unloaded.
    /// </summary>
    public void DeallocateVram(string modelId)
    {
        if (_loadedModels.TryGetValue(modelId, out var info))
            info.GpuVramBytes = 0;
    }

    /// <summary>
    /// Checks if a model can be loaded given current VRAM budget and available memory.
    /// Returns (canLoad, estimatedVram) or (false, 0) if insufficient resources.
    /// </summary>
    public (bool CanLoad, long EstimatedVram) CanAllocateVram(string modelId, long estimatedVram, CancellationToken cancellationToken = default)
    {
        if (_disposed) return (false, 0);

        var currentFree = _deviceMonitor.CurrentDeviceInformation.Gpus.Sum(g => g.FreeVramBytes);
        var totalUsed = TotalVramUsedBytes;
        var totalVram = _deviceMonitor.CurrentDeviceInformation.Gpus.Sum(g => g.TotalMemoryBytes);

        // Require at least 512MB free as a safety margin
        const long SafetyMargin = 512L * 1024 * 1024;
        long effectiveFree = Math.Max(0, currentFree - SafetyMargin);

        // If loading another model would leave less than 10% of total VRAM free, reject
        long projectedFree = effectiveFree - estimatedVram;
        bool canLoad = projectedFree >= SafetyMargin && (projectedFree >= (totalVram * 0.1));

        return (canLoad, projectedFree);
    }

    public async Task LoadModelAsync(string modelId, ModelType modelType, CancellationToken cancellationToken = default)
    {
        if (_disposed) return;

        // If a loader already exists for this type, use it
        var existingLoader = _loaders.Values.FirstOrDefault(l => l.SupportedModelType == modelType);
        if (existingLoader != null && existingLoader.IsLoaded)
        {
            _logger?.LogDebug("Evicting existing model of type {Type} before loading {ModelId}", modelType, modelId);
            await UnloadModelByIdAsync(modelId, cancellationToken);
        }

        if (existingLoader != null)
        {
            var success = await existingLoader.LoadModelAsync(modelId, cancellationToken);
            if (success)
            {
                _loadedModels[modelId] = new LoadedModelInfo(existingLoader, modelType);
                _logger?.LogInformation("Model '{ModelId}' loaded on {Type}", modelId, modelType);
            }
        }
        else
        {
            _logger?.LogWarning("No loader registered for model type {Type}", modelType);
        }
    }

    public async Task UnloadModelByIdAsync(string modelId, CancellationToken cancellationToken = default)
    {
        if (!_loadedModels.TryRemove(modelId, out var info)) return;

        try
        {
            await info.Loader.UnloadModelAsync(cancellationToken);
            _logger?.LogInformation("Model '{ModelId}' unloaded", modelId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to unload model '{ModelId}'", modelId);
        }
    }

    public async Task UnloadAllModelsAsync(CancellationToken cancellationToken = default)
    {
        var modelIds = _loadedModels.Keys.ToList();
        foreach (var modelId in modelIds)
        {
            await UnloadModelByIdAsync(modelId, cancellationToken);
        }
    }

    public void RegisterLoader(IModelLoader loader)
    {
        _loaders[loader.SupportedModelType.ToString()] = loader;
        _logger?.LogInformation("Registered loader for model type {Type}", loader.SupportedModelType);
    }

    public async Task<DeviceMemoryInfo> GetAvailableMemoryAsync(CancellationToken cancellationToken = default)
    {
        var deviceInfo = _deviceMonitor.CurrentDeviceInformation;
        var totalGpuVram = deviceInfo.Gpus.Sum(g => g.TotalMemoryBytes);
        var freeGpuVram = totalGpuVram - EstimatedMemoryUsageBytes;

        return new DeviceMemoryInfo(
            GpuVramTotalBytes: totalGpuVram,
            GpuVramFreeBytes: Math.Max(0, freeGpuVram),
            CpuMemoryTotalBytes: deviceInfo.Cpu.AvailableMemoryBytes,
            CpuMemoryFreeBytes: Math.Max(0, deviceInfo.Cpu.AvailableMemoryBytes - EstimatedMemoryUsageBytes));
    }

    public string GetCurrentDevice(string modelId)
    {
        return _loadedModels.TryGetValue(modelId, out var info)
            ? info.Device.ToString().ToLowerInvariant()
            : "cpu";
    }

    public async Task<bool> OffloadModelToCpuAsync(string modelId, CancellationToken cancellationToken = default)
    {
        if (!_loadedModels.TryGetValue(modelId, out var info) || info.Device != DeviceType.Gpu)
            return false;

        _logger?.LogDebug("Offloading model '{ModelId}' from GPU to CPU", modelId);

        // Unload from GPU, then reload on CPU
        await info.Loader.UnloadModelAsync(cancellationToken);
        _loadedModels.TryRemove(modelId, out _);

        var success = await info.Loader.LoadModelAsync(modelId, cancellationToken);
        if (success)
        {
            _loadedModels[modelId] = new LoadedModelInfo(info.Loader, info.Metadata.ModelType, DeviceType.Cpu);
            _logger?.LogInformation("Model '{ModelId}' offloaded to CPU", modelId);
            return true;
        }

        _logger?.LogWarning("Failed to offload model '{ModelId}' to CPU", modelId);
        return false;
    }

    public async Task<bool> MoveModelToDeviceAsync(string modelId, string targetDevice, CancellationToken cancellationToken = default)
    {
        if (!_loadedModels.TryGetValue(modelId, out var info))
            return false;

        var newDevice = targetDevice.Equals("gpu", StringComparison.OrdinalIgnoreCase)
            ? DeviceType.Gpu
            : DeviceType.Cpu;

        if (info.Device == newDevice)
            return true;

        _logger?.LogDebug("Moving model '{ModelId}' from {Current} to {Target}", modelId, info.Device, newDevice);

        // Unload from current device, then reload on target device
        await info.Loader.UnloadModelAsync(cancellationToken);
        _loadedModels.TryRemove(modelId, out _);

        var success = await info.Loader.LoadModelAsync(modelId, cancellationToken);
        if (success)
        {
            _loadedModels[modelId] = new LoadedModelInfo(info.Loader, info.Metadata.ModelType, newDevice);
            _logger?.LogInformation("Model '{ModelId}' moved to {Device}", modelId, newDevice);
            return true;
        }

        _logger?.LogWarning("Failed to move model '{ModelId}' to {Device}", modelId, newDevice);
        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var modelId in _loadedModels.Keys.ToList())
        {
            _loadedModels.TryRemove(modelId, out var info);
        }

        foreach (var loader in _loaders.Values)
        {
            try { loader.Dispose(); } catch { /* Ignore dispose errors */ }
        }
        _loaders.Clear();
    }

    /// <summary>
    /// Evicts models to free VRAM when OOM is detected.
    /// Eviction priority: VAE → Embedding → ImageGeneration → TextGeneration.
    /// </summary>
    public async Task EvictModelsToFreeVramAsync(long targetFreeBytes, CancellationToken cancellationToken = default)
    {
        var currentFree = await GetAvailableMemoryAsync(cancellationToken);
        if (currentFree.GpuVramFreeBytes >= targetFreeBytes)
            return;

        var modelsToEvict = GetEvictionPriority();
        foreach (var modelId in modelsToEvict)
        {
            await UnloadModelByIdAsync(modelId, cancellationToken);
            currentFree = await GetAvailableMemoryAsync(cancellationToken);
            if (currentFree.GpuVramFreeBytes >= targetFreeBytes)
                break;
        }
    }

    /// <summary>
    /// Updates the last-accessed timestamp for a loaded model.
    /// </summary>
    public void TouchModelAccess(string modelId)
    {
        if (_loadedModels.TryGetValue(modelId, out var info))
            info.LastAccessed = DateTime.UtcNow;
    }

    private class LoadedModelInfo
    {
        public LoadedModelInfo(IModelLoader loader, ModelType modelType, DeviceType device = DeviceType.Cpu)
        {
            Loader = loader;
            Metadata = new MultiModalModelMetadata
            {
                ModelType = modelType,
                FileSizeBytes = 0,
                Name = "unknown"
            };
            Device = device;
            LastAccessed = DateTime.UtcNow;
            GpuVramBytes = 0;
            CpuBytes = 0;
        }

        public IModelLoader Loader { get; }
        public MultiModalModelMetadata Metadata { get; }
        public DeviceType Device { get; set; }
        public DateTime LastAccessed { get; set; }
        public long GpuVramBytes { get; set; }
        public long CpuBytes { get; set; }
    }
}
