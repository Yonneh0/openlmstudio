using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Adapter that makes LlamaCppChatCompletionService implement IModelLoader for text generation models.
/// Provides memory estimation and unified model lifecycle management via IModelManager.
/// </summary>
public class GgufChatCompletionLoader : IModelLoader, IDisposable
{
    private readonly ILogger<GgufChatCompletionLoader>? _logger;
    private readonly LlamaCppChatCompletionService _chatService;
    private bool _disposed;
    private string? _currentModelId;

    public GgufChatCompletionLoader(ILogger<GgufChatCompletionLoader> logger, LlamaCppChatCompletionService chatService)
    {
        _logger = logger;
        _chatService = chatService;
    }

    public ModelType SupportedModelType => ModelType.TextGeneration;

    public bool IsLoaded => !string.IsNullOrEmpty(_currentModelId);

    public async Task<bool> LoadModelAsync(string modelId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _chatService.LoadModelAsync(modelId);
            if (result != null && result.State == ModelLoadState.Loaded)
            {
                _currentModelId = modelId;
                _logger?.LogInformation("Loaded text generation model '{ModelId}'", modelId);
                return true;
            }

            _logger?.LogWarning("Failed to load text generation model '{ModelId}' — model not found or could not be loaded", modelId);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load text generation model '{ModelId}'", modelId);
            return false;
        }
    }

    public async Task UnloadModelAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.IsNullOrEmpty(_currentModelId))
            {
                await _chatService.UnloadModelAsync(_currentModelId);
                _logger?.LogInformation("Unloaded text generation model '{ModelId}'", _currentModelId);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to unload text generation model");
        }
        finally
        {
            _currentModelId = null;
        }
    }

    public async Task<ModelMetadata?> GetModelMetadataAsync(string modelId, CancellationToken cancellationToken = default)
    {
        return await _chatService.GetLoadedModelsAsync()
            .ContinueWith(t => t.Result.FirstOrDefault(m => m.ModelId == modelId)?.Metadata);
    }

    // Return the current loaded model's metadata if there is one.
    public async Task<ModelMetadata?> GetModelMetadataAsync(CancellationToken cancellationToken = default)
    {
        return !string.IsNullOrEmpty(_currentModelId)
            ? await _chatService.GetLoadedModelsAsync()
                .ContinueWith(t => t.Result.FirstOrDefault(m => m.ModelId == _currentModelId)?.Metadata)
            : null;
    }

    public async Task<IEnumerable<ModelMetadata>> ListAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        // Delegate to the chat service — returns loaded models only.
        // TODO: Return all available models from the repository, not just loaded ones.
        return await _chatService.GetLoadedModelsAsync()
            .ContinueWith(t => t.Result.Select(m => m.Metadata), cancellationToken);
    }

    public long GetEstimatedModelSizeBytes()
    {
        // Use synchronous call for this simple operation (metadata is already cached)
        var meta = _currentModelId != null ? GetModelMetadataAsync(_currentModelId).GetAwaiter().GetResult() : null;
        return meta?.FileSizeBytes > 0 || meta?.EstimatedSizeBytes > 0 ? Math.Max(meta.FileSizeBytes, meta.EstimatedSizeBytes ?? 0) : -1;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (!string.IsNullOrEmpty(_currentModelId))
            UnloadModelAsync(CancellationToken.None).GetAwaiter().GetResult();

        _chatService.Dispose();
    }
}