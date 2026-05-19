using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Model cache cleanup service that scans the model directory and removes orphaned or unused files.
/// Tracks usage via a JSON metadata index file.
/// </summary>
public class ModelCacheCleanupService : IModelCacheCleanupService, IDisposable
{
    private readonly ILogger<ModelCacheCleanupService> _logger;
    private readonly AppDataDirectoryResolver _appDataResolver;
    private readonly string _cacheIndexPath;

    public ModelCacheCleanupService(
        ILogger<ModelCacheCleanupService> logger,
        AppDataDirectoryResolver appDataResolver)
    {
        _logger = logger;
        _appDataResolver = appDataResolver;
        _cacheIndexPath = Path.Combine(appDataResolver.MetadataDirectory, "cache_index.json");
    }

    public async Task<ModelCacheCleanupResult> CleanAsync(ModelCacheRetentionPolicy policy, CancellationToken ct = default)
    {
        var models = await GetCachedModelsAsync(ct);
        long totalFreed = 0;
        var removedModels = new List<string>();

        switch (policy)
        {
            case ModelCacheRetentionPolicy.KeepAll:
                _logger.LogInformation("Cache cleanup skipped — policy is KeepAll");
                return new ModelCacheCleanupResult(0, Array.Empty<string>());

            case ModelCacheRetentionPolicy.RemoveByLastUsed:
                var oldModels = models
                    .Where(m => m.LastUsedUtc < DateTimeOffset.UtcNow.AddDays(-30))
                    .ToList();
                foreach (var m in oldModels)
                {
                    if (await RemoveModelInternalAsync(m, ct))
                    {
                        totalFreed += m.FileSizeBytes;
                        removedModels.Add(m.ModelId);
                    }
                }
                _logger.LogInformation("RemoveByLastUsed: removed {Count} models, freed {Freed} bytes",
                    removedModels.Count, totalFreed);
                break;

            case ModelCacheRetentionPolicy.EvictBySize:
                var totalSize = await GetCacheSizeAsync(ct);
                var maxSize = 50L * 1024 * 1024 * 1024; // 50 GB soft limit
                if (totalSize > maxSize)
                {
                    var sorted = models.OrderBy(m => m.LastUsedUtc).ToList();
                    foreach (var m in sorted)
                    {
                        if (totalSize <= maxSize) break;
                        if (await RemoveModelInternalAsync(m, ct))
                        {
                            totalFreed += m.FileSizeBytes;
                            totalSize -= m.FileSizeBytes;
                            removedModels.Add(m.ModelId);
                        }
                    }
                    _logger.LogInformation("EvictBySize: removed {Count} models, freed {Freed} bytes",
                        removedModels.Count, totalFreed);
                }
                break;

            case ModelCacheRetentionPolicy.Aggressive:
                // Remove all unused models AND models larger than 10GB
                var aggressiveRemove = models
                    .Where(m => m.LastUsedUtc < DateTimeOffset.UtcNow.AddDays(-7) || m.FileSizeBytes > 10L * 1024 * 1024 * 1024)
                    .ToList();
                foreach (var m in aggressiveRemove)
                {
                    if (await RemoveModelInternalAsync(m, ct))
                    {
                        totalFreed += m.FileSizeBytes;
                        removedModels.Add(m.ModelId);
                    }
                }
                _logger.LogInformation("Aggressive: removed {Count} models, freed {Freed} bytes",
                    removedModels.Count, totalFreed);
                break;
        }

        return new ModelCacheCleanupResult(totalFreed, removedModels);
    }

    public async Task<long> GetCacheSizeAsync(CancellationToken ct = default)
    {
        var models = await GetCachedModelsAsync(ct);
        return models.Sum(m => m.FileSizeBytes);
    }

    public async Task<IReadOnlyList<CachedModelInfo>> GetCachedModelsAsync(CancellationToken ct = default)
    {
        var models = new List<CachedModelInfo>();

        // Scan models directory for model files
        var modelsDir = _appDataResolver.ModelsDirectory;
        if (!Directory.Exists(modelsDir))
            return models;

        foreach (var file in Directory.EnumerateFiles(modelsDir, "*", SearchOption.AllDirectories))
        {
            if (ct.IsCancellationRequested) break;

            var fileInfo = new FileInfo(file);
            var modelId = Path.GetFileNameWithoutExtension(file);

            models.Add(new CachedModelInfo(
                modelId,
                file,
                fileInfo.Length,
                fileInfo.LastAccessTimeUtc,
                fileInfo.CreationTimeUtc));
        }

        return models;
    }

    public async Task<bool> RemoveModelAsync(string modelId, CancellationToken ct = default)
    {
        var models = await GetCachedModelsAsync(ct);
        var model = models.FirstOrDefault(m => m.ModelId == modelId);
        if (model == null)
            return false;

        return await RemoveModelInternalAsync(model, ct);
    }

    public void Dispose()
    {
        // Nothing to dispose
    }

    private async Task<bool> RemoveModelInternalAsync(CachedModelInfo model, CancellationToken ct)
    {
        try
        {
            if (File.Exists(model.FilePath))
            {
                File.Delete(model.FilePath);
                _logger.LogDebug("Removed model file: {FilePath}", model.FilePath);
            }

            // Remove associated metadata files
            var metadataFile = Path.Combine(_appDataResolver.MetadataDirectory, $"{model.ModelId}.json");
            if (File.Exists(metadataFile))
                File.Delete(metadataFile);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove model {ModelId}", model.ModelId);
            return false;
        }
    }
}