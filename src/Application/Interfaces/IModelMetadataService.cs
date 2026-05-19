using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Service interface for model metadata operations.
/// Provides operations for metadata extraction, validation, and storage.
/// </summary>
public interface IModelMetadataService
{
    /// <summary>
    /// Extracts metadata from a model file (GGUF or safetensors).
    /// </summary>
    /// <param name="modelPath">Path to the model file.</param>
    /// <returns>Extracted metadata, or null if extraction fails.</returns>
    Task<ModelMetadata?> ExtractMetadataAsync(string modelPath);

    /// <summary>
    /// Gets metadata for a specific model by ID.
    /// </summary>
    /// <param name="modelId">The model ID.</param>
    /// <returns>The model metadata, or null if not found.</returns>
    Task<ModelMetadata?> GetMetadataAsync(string modelId);

    /// <summary>
    /// Updates metadata for an existing model.
    /// </summary>
    /// <param name="metadata">The updated metadata.</param>
    Task UpdateMetadataAsync(ModelMetadata metadata);

    /// <summary>
    /// Removes metadata for a model.
    /// </summary>
    /// <param name="modelId">The model ID.</param>
    Task RemoveMetadataAsync(string modelId);

    /// <summary>
    /// Lists all registered models.
    /// </summary>
    Task<IReadOnlyList<ModelMetadata>> ListModelsAsync();

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
    /// Validates a model file for correctness.
    /// </summary>
    /// <param name="modelPath">Path to the model file.</param>
    /// <returns>True if the model is valid, false otherwise.</returns>
    Task<bool> ValidateModelAsync(string modelPath);

    /// <summary>
    /// Gets total storage used by all registered models.
    /// </summary>
    Task<long> GetTotalStorageUsageAsync();
}