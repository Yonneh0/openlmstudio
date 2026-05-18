namespace OpenLMStudio.Domain.Interfaces;

using Models;

/// <summary>
/// Service interface for model management operations.
/// </summary>
public interface IModelService
{
    /// <summary>
    /// Discovers models in the configured repository path.
    /// </summary>
    Task<IReadOnlyList<ModelMetadata>> DiscoverModelsAsync(string repositoryPath);

    /// <summary>
    /// Downloads a model from a remote source (e.g., HuggingFace).
    /// </summary>
    Task<ModelMetadata> DownloadModelAsync(string url, string destinationPath);

    /// <summary>
    /// Searches for models by name or other criteria.
    /// </summary>
    Task<IReadOnlyList<ModelMetadata>> SearchModelsAsync(string query);

    /// <summary>
    /// Deletes a model from local storage.
    /// </summary>
    Task DeleteModelAsync(Guid modelId);
}