using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Adapter that makes DiffusionPipelineService implement IModelLoader for image generation models.
/// Provides memory estimation and unified model lifecycle management via IModelManager.
/// </summary>
public class DiffusionModelLoader : IModelLoader, IDisposable
{
    private readonly ILogger<DiffusionModelLoader>? _logger;
    private readonly DiffusionPipelineService _pipelineService;
    private readonly IModelRepository _modelRepo;
    private bool _disposed;
    private string? _currentModelId;

    public DiffusionModelLoader(ILogger<DiffusionModelLoader> logger, DiffusionPipelineService pipelineService, IModelRepository modelRepo)
    {
        _logger = logger;
        _pipelineService = pipelineService;
        _modelRepo = modelRepo;
    }

    public ModelType SupportedModelType => ModelType.ImageGeneration;

    public bool IsLoaded => !string.IsNullOrEmpty(_currentModelId);

    public async Task<bool> LoadModelAsync(string modelId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _pipelineService.LoadModelAsync(modelId);
            if (result)
            {
                _currentModelId = modelId;
                _logger?.LogInformation("Loaded image generation model '{ModelId}'", modelId);
                return true;
            }

            _logger?.LogWarning("Failed to load image generation model '{ModelId}'", modelId);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load image generation model '{ModelId}'", modelId);
            return false;
        }
    }

    public async Task UnloadModelAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.IsNullOrEmpty(_currentModelId))
            {
                await _pipelineService.UnloadModelAsync(_currentModelId);
                _logger?.LogInformation("Unloaded image generation model '{ModelId}'", _currentModelId);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to unload image generation model");
        }
        finally
        {
            _currentModelId = null;
        }
    }

    public async Task<ModelMetadata?> GetModelMetadataAsync(string modelId, CancellationToken cancellationToken = default)
    {
        var multimodalMeta = await _modelRepo.GetMultiModalModelByIdAsync(modelId);
        if (multimodalMeta != null)
        {
            // Convert MultiModalModelMetadata to ModelMetadata for interface compatibility.
            return new ModelMetadata
            {
                Id = multimodalMeta.Id,
                Name = multimodalMeta.Name,
                FilePath = multimodalMeta.FilePath,
                Architecture = multimodalMeta.PipelineType ?? multimodalMeta.Format.ToString(),
                Quantization = string.Empty,
                TensorDataType = string.Empty,
                ContextLength = 0,
                VocabularySize = 0,
                AttentionHeads = 0,
                AttentionHeadGroups = 0,
                TransformerLayers = 0,
                EmbeddingLength = 0,
                FfnLength = 0,
                RopeDimensionCount = 0,
                GpuSupportAvailable = true,
                FileSizeBytes = multimodalMeta.FileSizeBytes,
                LastModified = multimodalMeta.LastModified,
                Type = multimodalMeta.ModelType.ToString(),
                QuantizationVersion = 0,
                IsActive = multimodalMeta.IsActive,
                EstimatedSizeBytes = multimodalMeta.EstimatedSizeBytes
            };
        }

        return await _modelRepo.GetModelByIdAsync(modelId);
    }

    public async Task<IEnumerable<ModelMetadata>> ListAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        var multimodalModels = await _modelRepo.SearchMultiModalModelsAsync(modelTypeFilter: ModelType.ImageGeneration);
        return multimodalModels.Select(m => new ModelMetadata
        {
            Id = m.Id,
            Name = m.Name,
            FilePath = m.FilePath,
            Architecture = m.PipelineType ?? m.Format.ToString(),
            Quantization = string.Empty,
            TensorDataType = string.Empty,
            ContextLength = 0,
            VocabularySize = 0,
            AttentionHeads = 0,
            AttentionHeadGroups = 0,
            TransformerLayers = 0,
            EmbeddingLength = 0,
            FfnLength = 0,
            RopeDimensionCount = 0,
            GpuSupportAvailable = true,
            FileSizeBytes = m.FileSizeBytes,
            LastModified = m.LastModified,
            Type = m.ModelType.ToString(),
            QuantizationVersion = 0,
            IsActive = m.IsActive,
            EstimatedSizeBytes = m.EstimatedSizeBytes
        }).ToList();
    }

    public long GetEstimatedModelSizeBytes()
    {
        var meta = _currentModelId != null ? GetModelMetadataAsync(_currentModelId).GetAwaiter().GetResult() : null;
        return meta?.FileSizeBytes > 0 || meta?.EstimatedSizeBytes > 0 ? Math.Max(meta.FileSizeBytes, meta.EstimatedSizeBytes ?? 0) : -1;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (!string.IsNullOrEmpty(_currentModelId))
            UnloadModelAsync(CancellationToken.None).GetAwaiter().GetResult();
    }
}