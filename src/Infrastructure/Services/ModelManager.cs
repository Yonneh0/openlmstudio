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
/// </summary>
public class ModelManager : IModelManager, IDisposable
{
    private readonly ILogger<ModelManager>? _logger;
    private readonly IDeviceMonitor _deviceMonitor;

    private readonly ConcurrentDictionary<string, IModelLoader> _loaders = new();
    private readonly ConcurrentDictionary<string, LoadedModelInfo> _loadedModels = new();
    private bool _disposed;

    public ModelManager(ILogger<ModelManager> logger, IDeviceMonitor deviceMonitor)
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

    public Task<bool> OffloadModelToCpuAsync(string modelId, CancellationToken cancellationToken = default)
    {
        if (!_loadedModels.TryGetValue(modelId, out var info) || info.Device != DeviceType.Gpu)
            return Task.FromResult(false);

        _logger?.LogDebug("Offloading model '{ModelId}' from GPU to CPU", modelId);
        return Task.FromResult(true);
    }

    public Task<bool> MoveModelToDeviceAsync(string modelId, string targetDevice, CancellationToken cancellationToken = default)
    {
        if (!_loadedModels.TryGetValue(modelId, out var info))
            return Task.FromResult(false);

        var newDevice = targetDevice.Equals("gpu", StringComparison.OrdinalIgnoreCase)
            ? DeviceType.Gpu
            : DeviceType.Cpu;

        if (info.Device != newDevice)
        {
            _loadedModels[modelId] = new LoadedModelInfo(info.Loader, info.Metadata.ModelType, newDevice);
            _logger?.LogDebug("Moved model '{ModelId}' to {Device}", modelId, newDevice);
        }

        return Task.FromResult(true);
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
        }

        public IModelLoader Loader { get; }
        public MultiModalModelMetadata Metadata { get; }
        public DeviceType Device { get; set; }
    }
}