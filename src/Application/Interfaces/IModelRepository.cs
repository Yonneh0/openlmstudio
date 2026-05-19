using System.Collections.Generic;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Interface for discovering and managing GGUF model files on disk.
/// </summary>
public interface IModelRepository
{
    /// <summary>
    /// Scans the configured model directory for GGUF files and indexes them.
    /// </summary>
    /// <returns>Awaitable task returning a list of discovered model metadata.</returns>
    Task<IEnumerable<ModelMetadata>> DiscoverModelsAsync();

    /// <summary>
    /// Gets metadata for a specific model by its ID (filename without extension).
    /// </summary>
    /// <param name="modelId">The unique identifier of the model.</param>
    /// <returns>The model metadata, or null if not found.</returns>
    Task<ModelMetadata?> GetModelByIdAsync(string modelId);

    /// <summary>
    /// Gets multi-modal (safetensors-based) model metadata for a specific model by its ID.
    /// </summary>
    /// <param name="modelId">The unique identifier of the model.</param>
    /// <returns>The multi-modal model metadata, or null if not found.</returns>
    Task<MultiModalModelMetadata?> GetMultiModalModelByIdAsync(string modelId);

    /// <summary>
    /// Searches for models matching the given filter criteria.
    /// </summary>
    /// <param name="searchTerm">Text to search in model names and architecture fields.</param>
    /// <param name="architecture">Optional architecture filter (e.g., "llama").</param>
    /// <returns>List of matching model metadata entries.</returns>
    Task<IEnumerable<ModelMetadata>> SearchModelsAsync(string searchTerm, string? architecture = null);

    /// <summary>
    /// Gets all models filtered by architecture type.
    /// </summary>
    /// <param name="architecture">The architecture to filter by (e.g., "llama", "mistral").</param>
    /// <returns>List of models matching the architecture.</returns>
    Task<IEnumerable<ModelMetadata>> GetModelsByArchitectureAsync(string architecture);

    /// <summary>
    /// Updates or adds model metadata to the index.
    /// </summary>
    /// <param name="metadata">The model metadata to save.</param>
    /// <returns>Awaitable task indicating completion.</returns>
    Task SaveModelMetadataAsync(ModelMetadata metadata);

    /// <summary>
    /// Removes a model from the index without deleting the file.
    /// </summary>
    /// <param name="modelId">The ID of the model to remove from the index.</param>
    /// <returns>Awaitable task indicating completion.</returns>
    Task RemoveFromIndexAsync(string modelId);

    /// <summary>
    /// Gets a list of all unique architectures found in the indexed models.
    /// </summary>
    /// <returns>List of architecture names.</returns>
    Task<IEnumerable<string>> GetAvailableArchitecturesAsync();

    /// <summary>
    /// Gets a list of all models (alias for DiscoverModelsAsync).
    /// </summary>
    /// <param name="path">Optional path to scan (uses default if null).</param>
    /// <returns>List of all model metadata entries.</returns>
    Task<IReadOnlyList<ModelMetadata>> ListModelsAsync(string? path = null);

    /// <summary>
    /// Discovers models from a specific path.
    /// </summary>
    /// <param name="path">The path to scan for models.</param>
    /// <returns>List of discovered model metadata entries.</returns>
    Task<IEnumerable<ModelMetadata>> DiscoverModelsAsync(string path);

    // ---- Multi-Modal Model Methods ----

    /// <summary>
    /// Searches for multi-modal (safetensors-based) models matching the given criteria.
    /// Filters by model type, search term, and pipeline type (e.g., "sdxl", "sd15").
    /// </summary>
    Task<IEnumerable<MultiModalModelMetadata>> SearchMultiModalModelsAsync(
        string? searchTerm = null,
        ModelType? modelTypeFilter = null,
        string? pipelineTypeFilter = null);

    /// <summary>
    /// Gets a list of all multi-modal models (safetensors-based).
    /// </summary>
    Task<IReadOnlyList<MultiModalModelMetadata>> ListMultiModalModelsAsync();

    // ---- Metadata Operations (convenience methods) ----

    /// <summary>
    /// Gets model metadata by ID (alias for GetModelByIdAsync).
    /// </summary>
    Task<ModelMetadata?> GetMetadataAsync(string id);

    /// <summary>
    /// Saves model metadata (alias for SaveModelMetadataAsync).
    /// </summary>
    Task SaveMetadataAsync(ModelMetadata metadata);

    /// <summary>
    /// Removes model from index (alias for RemoveFromIndexAsync).
    /// </summary>
    Task RemoveMetadataAsync(string id);

    /// <summary>
    /// Gets models filtered by quantization type.
    /// </summary>
    /// <param name="quantization">The quantization type (e.g., "Q4_K_M").</param>
    /// <returns>List of models with matching quantization.</returns>
    Task<IEnumerable<ModelMetadata>> GetModelsByQuantizationAsync(string quantization);

    /// <summary>
    /// Gets model metadata by its file path.
    /// </summary>
    /// <param name="filePath">The full file path of the model.</param>
    /// <returns>Model metadata, or null if not found.</returns>
    Task<ModelMetadata?> GetByFilePathAsync(string filePath);
}
