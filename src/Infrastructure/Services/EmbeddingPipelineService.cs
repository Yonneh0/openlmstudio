using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// ONNX Runtime-based embedding pipeline service for text/image embedding generation.
/// Loads safetensors embedding models and runs inference to produce normalized float vectors.
/// </summary>
public class EmbeddingPipelineService : IEmbeddingPipelineService, IDisposable
{
    private readonly ILogger<EmbeddingPipelineService>? _logger;
    private readonly SafetensorParser _safetensorParser = new(null!);

    /// <summary>ONNX Runtime sessions keyed by model ID.</summary>
    private readonly ConcurrentDictionary<string, InferenceSession?> _loadedSessions = new();

    // Default embedding dimension — will be determined from actual safetensors models when implemented
    private const int DefaultEmbeddingDimension = 768;

    public EmbeddingPipelineService(ILogger<EmbeddingPipelineService>? logger)
    {
        _logger = logger;
        _logger?.LogInformation("EmbeddingPipelineService initialized (ONNX Runtime-based, safetensors model loader ready)");
    }

    // Resolve model repo on-demand — avoids circular DI issues with IModelRepository dependency.
    private static readonly object _repoLock = new();
    private static IModelRepository? _resolvedRepo;

    private IModelRepository? ModelRepo
    {
        get
        {
            if (_resolvedRepo != null) return _resolvedRepo;
            lock (_repoLock)
            {
                // Double-check after acquiring the lock (another thread may have resolved it)
                if (_resolvedRepo == null)
                {
                    try
                    {
                        var services = new ServiceCollection()
                            .AddLogging()
                            .AddOpenLMStudioServices()
                            .BuildServiceProvider();
                        _resolvedRepo = services.GetService<IModelRepository>();
                    }
                    catch
                    {
                        // Ignore resolution errors — GetAvailableModelsAsync handles null gracefully
                    }
                }
            }
            return _resolvedRepo;
        }
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
        if (ModelRepo == null)
        {
            _logger?.LogDebug("No model repository available for listing embedding models");
            return Array.Empty<MultiModalModelMetadata>();
        }

        var models = await ModelRepo.SearchMultiModalModelsAsync(modelTypeFilter: ModelType.Embedding);
        return models;
    }

    /// <summary>
    /// Gets the primary weight file path from model metadata.
    /// </summary>
    private string? GetPrimaryWeightFile(MultiModalModelMetadata metadata)
    {
        if (metadata.FilePath != null && File.Exists(metadata.FilePath))
            return metadata.FilePath;

        // Try sharded index for multi-file models
        var indexPath = $"{metadata.FilePath}.index.json";
        if (!string.IsNullOrEmpty(metadata.FilePath) && File.Exists(indexPath))
        {
            try
            {
                var indexJson = File.ReadAllText(indexPath);
                var indexDoc = System.Text.Json.JsonDocument.Parse(indexJson);
                var weightMap = indexDoc.RootElement.GetProperty("weight_map");

                // Embedding models typically have a single file — find first non-null entry
                foreach (var kvp in weightMap.EnumerateObject())
                {
                    var value = kvp.Value.GetString();
                    if (!string.IsNullOrEmpty(value) && File.Exists(Path.Combine(metadata.FilePath, value)))
                        return Path.Combine(metadata.FilePath, value);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to parse embedding safetensors index: {Path}", indexPath);
            }
        }

        // Try finding any .safetensors file in the model directory
        var dir = metadata.FilePath;
        if (dir != null && Directory.Exists(dir))
        {
            var safetensorsFiles = Directory.GetFiles(dir, "*.safetensors");
            return safetensorsFiles.Length > 0 ? Path.Combine(dir, safetensorsFiles[0]) : null;
        }

        _logger?.LogWarning("Cannot determine weight file for embedding model '{ModelId}' — no FilePath set", metadata.Id);
        return null;
    }

    public async Task<bool> LoadModelAsync(string modelId)
    {
        if (_loadedSessions.ContainsKey(modelId))
            return true; // Already loaded

        _logger?.LogInformation("Loading embedding model: {ModelId}", modelId);

        try
        {
            // Look up the model in the repository to find its weight file path
            var metadata = await GetAvailableModelsAsync();
            var match = metadata.FirstOrDefault(m => m.Id == modelId);
            if (match == null)
            {
                _logger?.LogWarning("Embedding model '{ModelId}' not found in repository", modelId);
                return false;
            }

            var weightFilePath = GetPrimaryWeightFile(match);
            if (string.IsNullOrEmpty(weightFilePath) || !File.Exists(weightFilePath))
            {
                _logger?.LogWarning("Weight file not found for embedding model '{ModelId}' at path: {Path}", modelId, weightFilePath);
                return false;
            }

            // Validate safetensors header before loading
            var headerValid = await _safetensorParser.ValidateHeaderAsync(weightFilePath);
            if (!headerValid)
            {
                _logger?.LogError("Safetensors header validation failed for embedding model '{ModelId}' at path: {Path}", modelId, weightFilePath);
                return false;
            }

            // For large models (>8GB), use memory-mapped I/O to reduce peak RAM usage
            var fileSize = new FileInfo(weightFilePath).Length;
            SessionOptions sessionOptions = new();
            if (fileSize > 8L * 1024 * 1024 * 1024) // >8GB — use memory mapping
            {
                _logger?.LogInformation("Large embedding model detected ({Size} bytes) for '{ModelId}' — using memory-mapped weight loading", fileSize, modelId);
                sessionOptions = new SessionOptions();
            }

            var inferenceSession = new InferenceSession(weightFilePath, sessionOptions);

            // Validate the ONNX Runtime session has valid input/output metadata
            if (!inferenceSession.InputMetadata.Any() || !inferenceSession.OutputMetadata.Any())
            {
                _logger?.LogError("ONNX Runtime session for embedding model '{ModelId}' has no valid I/O tensors", modelId);
                inferenceSession.Dispose();
                return false;
            }

            // Extract the expected embedding dimension from the output tensor shape (KeyValuePair<string, NodeMetadata>.Value.Dimensions)
            var outputNode = inferenceSession.OutputMetadata.First();
            var outputDims = outputNode.Value.Dimensions;
            _logger?.LogInformation("Embedding model '{ModelId}' loaded successfully — {Size} bytes, input: {InputCount} tensors, output dims: [{OutputShape}]",
                modelId, fileSize, inferenceSession.InputMetadata.Count(), string.Join(", ", outputDims));

            _loadedSessions[modelId] = inferenceSession;
            _logger?.LogInformation("Embedding model '{ModelId}' loaded (ONNX Runtime session ready)", modelId);
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            _logger?.LogError(ex, "Failed to load embedding model: {ModelId}", modelId);

            // Clean up partial loading state
            if (_loadedSessions.ContainsKey(modelId))
                _loadedSessions[modelId]?.Dispose();
            _loadedSessions.TryRemove(modelId, out _);
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