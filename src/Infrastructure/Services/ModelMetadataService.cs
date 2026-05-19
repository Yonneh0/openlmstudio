using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Concrete implementation of IModelMetadataService backed by GgufParser and JsonModelRepository.
/// Provides model metadata extraction, validation, and storage operations.
/// </summary>
public class ModelMetadataService : IModelMetadataService
{
    private readonly IModelRepository _modelRepository;
    private readonly GgufParser _ggufParser;
    private readonly ILogger<ModelMetadataService> _logger;

    public ModelMetadataService(IModelRepository modelRepository, GgufParser ggufParser, ILogger<ModelMetadataService> logger)
    {
        _modelRepository = modelRepository;
        _ggufParser = ggufParser;
        _logger = logger;
    }

    public async Task<ModelMetadata?> ExtractMetadataAsync(string modelPath)
    {
        try
        {
            // Try GGUF parser first
            var ggufInfo = await _ggufParser.ParseHeaderAsync(modelPath);
            if (ggufInfo != null)
            {
                var metadata = ggufInfo.ToModelMetadata();
                metadata.Id = metadata.Id ?? Guid.NewGuid().ToString();
                metadata.LastModified = File.GetLastWriteTimeUtc(modelPath);
                metadata.IsActive = true;

                await _modelRepository.SaveModelMetadataAsync(metadata);

                _logger.LogInformation("Extracted GGUF metadata: {Name} from {Path}", metadata.Name, modelPath);
                return metadata;
            }

            _logger.LogWarning("Could not extract GGUF metadata from {Path}", modelPath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract metadata from {Path}", modelPath);
            return null;
        }
    }

    public async Task<ModelMetadata?> GetMetadataAsync(string modelId)
    {
        try
        {
            var metadata = await _modelRepository.GetModelByIdAsync(modelId);
            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get metadata for {Id}", modelId);
            return null;
        }
    }

    public async Task UpdateMetadataAsync(ModelMetadata metadata)
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

    public async Task RemoveMetadataAsync(string modelId)
    {
        try
        {
            await _modelRepository.RemoveFromIndexAsync(modelId);

            _logger.LogInformation("Removed metadata for: {Id}", modelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove metadata for: {Id}", modelId);
            throw;
        }
    }

    public async Task<IReadOnlyList<ModelMetadata>> ListModelsAsync()
    {
        try
        {
            var models = await _modelRepository.ListModelsAsync();
            return models;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list models");
            return new List<ModelMetadata>();
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

    public async Task<bool> ValidateModelAsync(string modelPath)
    {
        try
        {
            if (!File.Exists(modelPath))
            {
                _logger.LogWarning("Model file does not exist: {Path}", modelPath);
                return false;
            }

            // Check file extension
            var extension = Path.GetExtension(modelPath).ToLowerInvariant();
            if (extension != ".gguf" && extension != ".safetensors")
            {
                _logger.LogWarning("Invalid model file extension: {Extension}", extension);
                return false;
            }

            // Try to parse the header
            var metadata = await ExtractMetadataAsync(modelPath);
            return metadata != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate model: {Path}", modelPath);
            return false;
        }
    }

    public async Task<long> GetTotalStorageUsageAsync()
    {
        try
        {
            var models = await _modelRepository.ListModelsAsync();
            return models.Sum(m => m.FileSizeBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate total storage usage");
            return 0;
        }
    }
}