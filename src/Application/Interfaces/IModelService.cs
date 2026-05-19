using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Service interface for model management operations.
/// Provides operations for model discovery, metadata extraction, and model lifecycle management.
/// </summary>
public interface IModelService
{
    /// <summary>
    /// Discovers available models in the specified directory.
    /// </summary>
    /// <param name="path">The directory to scan, or null for the default models directory.</param>
    /// <returns>List of discovered model metadata.</returns>
    Task<IReadOnlyList<ModelMetadata>> DiscoverModelsAsync(string? path = null);

    /// <summary>
    /// Extracts metadata from a model file (GGUF or safetensors).
    /// </summary>
    /// <param name="modelPath">Path to the model file.</param>
    /// <returns>Extracted metadata, or null if extraction fails.</returns>
    Task<ModelMetadata?> ExtractMetadataAsync(string modelPath);

    /// <summary>
    /// Gets metadata for a specific model by ID.
    /// </summary>
    /// <param name="id">The model ID.</param>
    /// <returns>The model metadata, or null if not found.</returns>
    Task<ModelMetadata?> GetMetadataAsync(string id);

    /// <summary>
    /// Updates metadata for an existing model.
    /// </summary>
    /// <param name="metadata">The updated metadata.</param>
    Task UpdateMetadataAsync(ModelMetadata metadata);

    /// <summary>
    /// Removes a model from the registry.
    /// </summary>
    /// <param name="id">The model ID.</param>
    Task RemoveModelAsync(string id);

    /// <summary>
    /// Registers a new model file with the registry.
    /// </summary>
    /// <param name="filePath">Path to the model file.</param>
    Task RegisterModelAsync(string filePath);

    /// <summary>
    /// Gets models filtered by architecture type.
    /// </summary>
    /// <param name="architecture">The architecture (e.g., "llama", "mistral").</param>
    /// <returns>Matching models.</returns>
    Task<IReadOnlyList<ModelMetadata>> GetModelsByArchitectureAsync(string architecture);

    /// <summary>
    /// Gets models filtered by quantization type.
    /// </summary>
    /// <param name="quantization">The quantization type (e.g., "Q4_0", "F16").</param>
    /// <returns>Matching models.</returns>
    Task<IReadOnlyList<ModelMetadata>> GetModelsByQuantizationAsync(string quantization);

    /// <summary>
    /// Gets total storage used by all models.
    /// </summary>
    Task<long> GetTotalModelStorageAsync();

    /// <summary>
    /// Gets metadata for a model by its file path.
    /// </summary>
    /// <param name="filePath">The model file path.</param>
    /// <returns>The model metadata, or null if not found.</returns>
    Task<ModelMetadata?> GetByFilePathAsync(string filePath);
}