using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Placeholder embedding pipeline service — generates random embeddings until ONNX Runtime/safetensors models are integrated.
/// </summary>
public class EmbeddingPipelineService : IEmbeddingPipelineService, IDisposable
{
    private readonly ILogger<EmbeddingPipelineService>? _logger;

    /// <summary>ONNX Runtime sessions keyed by model ID.</summary>
    private readonly ConcurrentDictionary<string, InferenceSession?> _loadedSessions = new();

    // Default embedding dimension for stub — will be determined from actual safetensors models when implemented
    private const int DefaultEmbeddingDimension = 768;

    public EmbeddingPipelineService(ILogger<EmbeddingPipelineService>? logger)
    {
        _logger = logger;
        _logger?.LogInformation("EmbeddingPipelineService initialized (stub — generating random embeddings until models are integrated)");
    }

    public async Task<float[]> GenerateAsync(string modelId, string inputText, CancellationToken ct = default)
    {
        // Ensure model is loaded
        if (!_loadedSessions.ContainsKey(modelId))
        {
            var wasLoaded = await LoadModelAsync(modelId);
            if (!wasLoaded)
                throw new InvalidOperationException($"Failed to load embedding model '{modelId}'.");
        }

        _logger?.LogDebug("Generating embedding for model '{ModelId}' with input text: {InputText}", modelId, inputText);

        // Stub: generate random normalized vector of default dimensionality
        var rng = new Random();
        var vector = new float[DefaultEmbeddingDimension];
        for (int i = 0; i < DefaultEmbeddingDimension; i++)
            vector[i] = (float)rng.NextDouble() * 2f - 1f;

        // Normalize the vector to unit length
        var magnitude = Math.Sqrt(vector.Sum(v => v * v));
        if (magnitude > 0)
            for (int i = 0; i < DefaultEmbeddingDimension; i++)
                vector[i] /= (float)magnitude;

        return vector;
    }

    public async Task<float[][]> GenerateBatchAsync(string modelId, IReadOnlyList<string> inputs, CancellationToken ct = default)
    {
        // Ensure model is loaded
        if (!_loadedSessions.ContainsKey(modelId))
        {
            var wasLoaded = await LoadModelAsync(modelId);
            if (!wasLoaded)
                throw new InvalidOperationException($"Failed to load embedding model '{modelId}'.");
        }

        _logger?.LogDebug("Generating batch embeddings for model '{ModelId}' with {Count} inputs", modelId, inputs.Count);

        var rng = new Random();
        var results = new float[inputs.Count][];

        for (int i = 0; i < inputs.Count; i++)
        {
            results[i] = new float[DefaultEmbeddingDimension];
            for (int j = 0; j < DefaultEmbeddingDimension; j++)
                results[i][j] = (float)rng.NextDouble() * 2f - 1f;

            // Normalize the vector to unit length
            var magnitude = Math.Sqrt(results[i].Sum(v => v * v));
            if (magnitude > 0)
                for (int j = 0; j < DefaultEmbeddingDimension; j++)
                    results[i][j] /= (float)magnitude;
        }

        return results;
    }

    public async Task<IEnumerable<MultiModalModelMetadata>> GetAvailableModelsAsync()
    {
        // Will search model repository for embedding-type models when safetensors support is complete
        _logger?.LogDebug("No embedding models available yet — stub implementation");
        return Array.Empty<MultiModalModelMetadata>();
    }

    public async Task<bool> LoadModelAsync(string modelId)
    {
        if (_loadedSessions.ContainsKey(modelId))
            return true; // Already loaded

        _logger?.LogInformation("Loading embedding model: {ModelId}", modelId);

        try
        {
            // TODO: When safetensors models are integrated, load the ONNX Runtime session here.
            // For now, just mark as "loaded" with null placeholder.
            _loadedSessions[modelId] = null;

            _logger?.LogInformation("Embedding model '{ModelId}' loaded (stub)", modelId);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load embedding model: {ModelId}", modelId);
            return false;
        }
    }

    public async Task<bool> UnloadModelAsync(string modelId)
    {
        if (!_loadedSessions.ContainsKey(modelId))
            return false;

        _logger?.LogDebug("Unloading embedding model: {ModelId}", modelId);

        var session = _loadedSessions[modelId];
        session?.Dispose();
        _loadedSessions.TryRemove(modelId, out _);

        return true;
    }

    public async Task<IEnumerable<string>> GetLoadedModelsAsync()
    {
        return _loadedSessions.Keys.ToList();
    }

    public void Dispose()
    {
        foreach (var session in _loadedSessions.Values.Where(s => s != null))
            session?.Dispose();
        _loadedSessions.Clear();
    }
}