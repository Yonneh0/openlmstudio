using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Concrete implementation of IModelService backed by GgufParser and JsonModelRepository.
/// Provides model discovery, metadata extraction, and model management operations.
/// </summary>
public class ModelService : IModelService
{
    private readonly IModelRepository _modelRepository;
    private readonly GgufParser _ggufParser;
    private readonly ILogger<ModelService> _logger;

    public ModelService(IModelRepository modelRepository, GgufParser ggufParser, ILogger<ModelService> logger)
    {
        _modelRepository = modelRepository;
        _ggufParser = ggufParser;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ModelMetadata>> DiscoverModelsAsync(string? path = null)
    {
        try
        {
            var models = await _modelRepository.ListModelsAsync(path);

            _logger.LogInformation("Discovered {Count} model(s) from {Path}", models.Count, path ?? "default");
            return models;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover models from {Path}", path ?? "default");
            return new List<ModelMetadata>();
        }
    }

    public async Task<Domain.Models.ModelMetadata?> ExtractMetadataAsync(string modelPath)
    {
        try
        {
            // Try GGUF parser first
            var ggufInfo = await _ggufParser.ParseHeaderAsync(modelPath);
            if (ggufInfo != null)
            {
                var metadata = ggufInfo.ToModelMetadata();
                await _modelRepository.SaveModelMetadataAsync(metadata);

                _logger.LogInformation("Extracted GGUF metadata: {Name} from {Path}", metadata.Name, modelPath);
                return metadata;
            }

            _logger.LogWarning("Could not extract metadata from {Path}", modelPath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract metadata from {Path}", modelPath);
            return null;
        }
    }

    public async Task<Domain.Models.ModelMetadata?> GetMetadataAsync(string id)
    {
        try
        {
            var metadata = await _modelRepository.GetModelByIdAsync(id);
            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get metadata for {Id}", id);
            return null;
        }
    }

    public async Task UpdateMetadataAsync(Domain.Models.ModelMetadata metadata)
    {
        try
        {
            await _modelRepository.SaveModelMetadataAsync(metadata);

            _logger.LogInformation("Updated metadata for: {Name}", metadata.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update metadata for: {Name}", metadata.Name);
            throw;
        }
    }

    public async Task RemoveModelAsync(string id)
    {
        try
        {
            await _modelRepository.RemoveFromIndexAsync(id);

            _logger.LogInformation("Removed model: {Id}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove model: {Id}", id);
            throw;
        }
    }

    public async Task RegisterModelAsync(string filePath)
    {
        try
        {
            var metadata = await ExtractMetadataAsync(filePath);
            if (metadata != null)
            {
                _logger.LogInformation("Registered new model: {Name} at {Path}", metadata.Name, filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register model: {FilePath}", filePath);
            throw;
        }
    }

    public async Task<IReadOnlyList<ModelMetadata>> GetModelsByArchitectureAsync(string architecture)
    {
        try
        {
            var models = await _modelRepository.GetModelsByArchitectureAsync(architecture);
            return models.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get models by architecture: {Architecture}", architecture);
            return new List<ModelMetadata>();
        }
    }

    public async Task<IReadOnlyList<ModelMetadata>> GetModelsByQuantizationAsync(string quantization)
    {
        try
        {
            var models = await _modelRepository.GetModelsByQuantizationAsync(quantization);
            return models.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get models by quantization: {Quantization}", quantization);
            return new List<ModelMetadata>();
        }
    }

    public async Task<long> GetTotalModelStorageAsync()
    {
        try
        {
            var models = await _modelRepository.ListModelsAsync();
            return models.Sum(m => m.FileSizeBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate total model storage");
            return 0;
        }
    }

    public async Task<Domain.Models.ModelMetadata?> GetByFilePathAsync(string filePath)
    {
        try
        {
            var metadata = await _modelRepository.GetByFilePathAsync(filePath);
            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get metadata by file path: {FilePath}", filePath);
            return null;
        }
    }
}