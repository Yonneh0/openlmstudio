using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
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

    // Default embedding dimension — will be determined from actual safetensors models when implemented.
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
                // Double-check after acquiring the lock (another thread may have resolved it).
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
                        // Ignore resolution errors — GetAvailableModelsAsync handles null gracefully.
                    }
                }
            }
            return _resolvedRepo;
        }
    }

    public async Task<float[]> GenerateAsync(string modelId, string inputText, CancellationToken ct = default)
    {
        // Ensure model is loaded.
        if (!_loadedSessions.ContainsKey(modelId))
        {
            var wasLoaded = await LoadModelAsync(modelId);
            if (!wasLoaded)
                throw new InvalidOperationException($"Failed to load embedding model '{modelId}'.");
        }

        _logger?.LogDebug("Generating embedding for model '{ModelId}' with input text: {InputText}", modelId, inputText);

        var session = _loadedSessions[modelId];
        if (session == null)
        {
            _logger?.LogError("ONNX Runtime session is null for model '{ModelId}'", modelId);
            throw new InvalidOperationException($"ONNX Runtime session not available for embedding model '{modelId}'.");
        }

        // Tokenize input text into token IDs.
        int[] tokenIds = TokenizeInput(inputText, session.InputMetadata.Keys.First());

        if (tokenIds.Length == 0)
        {
            _logger?.LogWarning("Tokenized input is empty for model '{ModelId}'", modelId);
            return new float[DefaultEmbeddingDimension];
        }

        // Create input tensor [1, seq_length] with float values of token IDs.
        float[] tokenValues = new float[tokenIds.Length];
        for (int i = 0; i < tokenIds.Length; i++)
            tokenValues[i] = tokenIds[i];

        var dims2d = new int[] { 1, tokenIds.Length };
        var inputTensor = new DenseTensor<float>(tokenValues, dims2d);

        // Build input values from ONNX Runtime session metadata.
        var inputNames = session.InputMetadata.Keys.ToList();
        var outputNames = session.OutputMetadata.Keys.ToList();

        if (inputNames.Count == 0 || outputNames.Count == 0)
        {
            _logger?.LogError("ONNX Runtime session for embedding model '{ModelId}' has no valid I/O tensors", modelId);
            throw new InvalidOperationException($"ONNX Runtime session for embedding model '{modelId}' has no valid I/O tensors.");
        }

        var inputValues = new List<NamedOnnxValue>();
        inputValues.Add(NamedOnnxValue.CreateFromTensor(inputNames[0], inputTensor));

        // Also add attention mask if the model expects it.
        bool hasAttentionMask = session.InputMetadata.Keys.Any(k => k.Contains("attention", StringComparison.OrdinalIgnoreCase) || k.Contains("mask", StringComparison.OrdinalIgnoreCase));
        if (hasAttentionMask)
        {
            var maskNames = session.InputMetadata.Keys.Where(k => k.Contains("attention", StringComparison.OrdinalIgnoreCase) || k.Contains("mask", StringComparison.OrdinalIgnoreCase)).ToList();
            float[] attentionMaskValues = new float[tokenIds.Length];
            for (int i = 0; i < tokenIds.Length; i++)
                attentionMaskValues[i] = 1f; // All tokens are part of the sequence.

            int[] dims3d = new[] { 1, tokenIds.Length };
            inputValues.Add(NamedOnnxValue.CreateFromTensor(maskNames[0], new DenseTensor<float>(attentionMaskValues, dims3d)));
        }

        // Also add position IDs if the model expects them.
        bool hasPositionIds = session.InputMetadata.Keys.Any(k => k.Contains("position", StringComparison.OrdinalIgnoreCase));
        if (hasPositionIds)
        {
            var posNames = session.InputMetadata.Keys.Where(k => k.Contains("position", StringComparison.OrdinalIgnoreCase)).ToList();
            float[] positions = new float[tokenIds.Length];
            for (int i = 0; i < tokenIds.Length; i++)
                positions[i] = i;

            int[] dims4d = new[] { 1, tokenIds.Length };
            inputValues.Add(NamedOnnxValue.CreateFromTensor(posNames[0], new DenseTensor<float>(positions, dims4d)));
        }

        // Run ONNX inference.
        var results = session.Run(inputValues.ToArray(), outputNames);

        using var result = results.First(r => r.Name == outputNames[outputNames.Count - 1]); // Use last output (typically the embedding).
        float[] embeddingData = result.AsEnumerable<float>().ToArray();

        // Extract the actual embedding dimension from the output tensor shape.
        var outputNode = session.OutputMetadata[outputNames.Last()];
        int[] dims3d2 = outputNode.Dimensions.Cast<int>().ToArray();
        int actualDimension;

        if (dims3d2.Length == 2)
            // [batch, embedding_dim] — take first batch item.
            actualDimension = dims3d2[1];
        else if (dims3d2.Length == 3)
            // [batch, seq_len, embedding_dim] — take mean over sequence dimension.
            actualDimension = dims3d2[2];
        else
            // Unknown shape — use default or the last dimension.
            actualDimension = outputNode.Dimensions.Length > 0 ? outputNode.Dimensions.Last() : DefaultEmbeddingDimension;

        if (embeddingData.Length != actualDimension && dims3d2.Length == 2)
        {
            _logger?.LogWarning("ONNX Runtime embedding output size ({OutputSize}) doesn't match dimension ({Expected}), using default", actualDimension, embeddingData.Length);
            return embeddingData; // Return whatever we got.
        }

        if (dims3d2.Length == 3)
        {
            // Mean-pool over the sequence dimension: [1, seq_len, dim] → [dim].
            var pooled = new float[actualDimension];
            for (int i = 0; i < actualDimension && i < embeddingData.Length / dims3d2[1]; i++)
                pooled[i] = embeddingData.Skip(i).Take(embeddingData.Length / dims3d2[1]).Sum() / dims3d2[1];

            // Normalize the vector to unit length.
            var magnitude = Math.Sqrt(pooled.Sum(v => v * v));
            if (magnitude > 0)
                for (int i = 0; i < actualDimension; i++)
                    pooled[i] /= (float)magnitude;

            return pooled;
        }

        // Normalize the vector to unit length.
        var magnitude2 = Math.Sqrt(embeddingData.Sum(v => v * v));
        if (magnitude2 > 0)
            for (int i = 0; i < embeddingData.Length; i++)
                embeddingData[i] /= (float)magnitude2;

        _logger?.LogDebug("Generated {Dimension}-dimensional embedding for model '{ModelId}'", actualDimension, modelId);
        return embeddingData;
    }

    public async Task<float[][]> GenerateBatchAsync(string modelId, IReadOnlyList<string> inputs, CancellationToken ct = default)
    {
        // Ensure model is loaded.
        if (!_loadedSessions.ContainsKey(modelId))
        {
            var wasLoaded = await LoadModelAsync(modelId);
            if (!wasLoaded)
                throw new InvalidOperationException($"Failed to load embedding model '{modelId}'.");
        }

        _logger?.LogDebug("Generating batch embeddings for model '{ModelId}' with {Count} inputs", modelId, inputs.Count);

        var session = _loadedSessions[modelId];
        if (session == null)
            throw new InvalidOperationException($"ONNX Runtime session not available for embedding model '{modelId}'.");

        // Build batch input tensor [batch_size, seq_length] with float values of token IDs.
        var allTokenIds = inputs.Select(input => TokenizeInput(input, session.InputMetadata.Keys.First())).ToList();
        int maxSeqLength = allTokenIds.Max(ids => ids.Length);

        if (maxSeqLength == 0)
            return Enumerable.Repeat(new float[DefaultEmbeddingDimension], inputs.Count).ToArray();

        // Pad each token array to the same length for batch tensor creation.
        var paddedTokens = new List<int[]>();
        foreach (var ids in allTokenIds)
        {
            if (ids.Length == maxSeqLength)
                paddedTokens.Add(ids);
            else
            {
                int[] padded = new int[maxSeqLength];
                for (int i = 0; i < ids.Length; i++)
                    padded[i] = ids[i];
                // Pad with zeros (padding token).
                paddedTokens.Add(padded);
            }
        }

        float[] batchTokenValues = new float[inputs.Count * maxSeqLength];
        for (int b = 0; b < inputs.Count; b++)
        {
            int offset = b * maxSeqLength;
            for (int i = 0; i < maxSeqLength; i++)
                batchTokenValues[offset + i] = paddedTokens[b][i];
        }

        var dims2d = new int[] { inputs.Count, maxSeqLength };
        var inputTensor = new DenseTensor<float>(batchTokenValues, dims2d);

        // Build input values from ONNX Runtime session metadata.
        var inputNames = session.InputMetadata.Keys.ToList();
        var outputNames = session.OutputMetadata.Keys.ToList();

        if (inputNames.Count == 0 || outputNames.Count == 0)
            throw new InvalidOperationException($"ONNX Runtime session for embedding model '{modelId}' has no valid I/O tensors.");

        var inputValues = new List<NamedOnnxValue>();
        inputValues.Add(NamedOnnxValue.CreateFromTensor(inputNames[0], inputTensor));

        // Also add attention mask if the model expects it.
        bool hasAttentionMask = session.InputMetadata.Keys.Any(k => k.Contains("attention", StringComparison.OrdinalIgnoreCase) || k.Contains("mask", StringComparison.OrdinalIgnoreCase));
        if (hasAttentionMask)
        {
            var maskNames = session.InputMetadata.Keys.Where(k => k.Contains("attention", StringComparison.OrdinalIgnoreCase) || k.Contains("mask", StringComparison.OrdinalIgnoreCase)).ToList();
            float[] attentionMaskValues = new float[inputs.Count * maxSeqLength];
            for (int b = 0; b < inputs.Count; b++)
            {
                int offset = b * maxSeqLength;
                for (int i = 0; i < allTokenIds[b].Length; i++)
                    attentionMaskValues[offset + i] = 1f; // Real tokens have mask=1, padding has mask=0.
            }

            int[] dims3d = new[] { inputs.Count, maxSeqLength };
            inputValues.Add(NamedOnnxValue.CreateFromTensor(maskNames[0], new DenseTensor<float>(attentionMaskValues, dims3d)));
        }

        // Also add position IDs if the model expects them.
        bool hasPositionIds = session.InputMetadata.Keys.Any(k => k.Contains("position", StringComparison.OrdinalIgnoreCase));
        if (hasPositionIds)
        {
            var posNames = session.InputMetadata.Keys.Where(k => k.Contains("position", StringComparison.OrdinalIgnoreCase)).ToList();
            float[] positions = new float[inputs.Count * maxSeqLength];
            for (int b = 0; b < inputs.Count; b++)
            {
                int offset = b * maxSeqLength;
                for (int i = 0; i < maxSeqLength; i++)
                    positions[offset + i] = i;
            }

            int[] dims4d = new[] { inputs.Count, maxSeqLength };
            inputValues.Add(NamedOnnxValue.CreateFromTensor(posNames[0], new DenseTensor<float>(positions, dims4d)));
        }

        // Run ONNX inference.
        var results = session.Run(inputValues.ToArray(), outputNames);

        using var result = results.First(r => r.Name == outputNames[outputNames.Count - 1]);
        float[] embeddingData = result.AsEnumerable<float>().ToArray();

        // Extract the actual embedding dimension from the output tensor shape.
        var outputNode = session.OutputMetadata[outputNames.Last()];
        int[] dims3d2 = outputNode.Dimensions.Cast<int>().ToArray();
        int actualDimension;

        if (dims3d2.Length == 2)
            // [batch_size, embedding_dim]
            actualDimension = dims3d2[1];
        else if (dims3d2.Length == 3)
            // [batch_size, seq_len, embedding_dim] — mean-pool over sequence dimension.
            actualDimension = dims3d2[2];
        else
            // Unknown shape — use default or the last dimension.
            actualDimension = outputNode.Dimensions.Length > 0 ? outputNode.Dimensions.Last() : DefaultEmbeddingDimension;

        var batchResults = new float[inputs.Count][];

        if (dims3d2.Length == 3)
        {
            for (int b = 0; b < inputs.Count; b++)
            {
                int offset = b * maxSeqLength * actualDimension;
                // Mean-pool over the sequence dimension.
                var pooled = new float[actualDimension];
                for (int i = 0; i < actualDimension && i < embeddingData.Length / inputs.Count / maxSeqLength; i++)
                    pooled[i] = embeddingData.Skip(offset + i).Take(embeddingData.Length / inputs.Count / maxSeqLength).Sum() / maxSeqLength;

                // Normalize the vector to unit length.
                var magnitude = Math.Sqrt(pooled.Sum(v => v * v));
                if (magnitude > 0)
                    for (int j = 0; j < actualDimension; j++)
                        pooled[j] /= (float)magnitude;

                batchResults[b] = pooled;
            }
        }
        else
        {
            for (int b = 0; b < inputs.Count; b++)
            {
                int offset = b * actualDimension;
                var vector = new float[actualDimension];
                for (int i = 0; i < actualDimension && offset + i < embeddingData.Length; i++)
                    vector[i] = embeddingData[offset + i];

                // Normalize the vector to unit length.
                var magnitude2 = Math.Sqrt(vector.Sum(v => v * v));
                if (magnitude2 > 0)
                    for (int j = 0; j < actualDimension; j++)
                        vector[j] /= (float)magnitude2;

                batchResults[b] = vector;
            }
        }

        _logger?.LogDebug("Generated {Count} embeddings of dimension {Dim}", inputs.Count, actualDimension);
        return batchResults;
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

        // Try sharded index for multi-file models.
        var indexPath = $"{metadata.FilePath}.index.json";
        if (!string.IsNullOrEmpty(metadata.FilePath) && File.Exists(indexPath))
        {
            try
            {
                var indexJson = File.ReadAllText(indexPath);
                var indexDoc = System.Text.Json.JsonDocument.Parse(indexJson);
                var weightMap = indexDoc.RootElement.GetProperty("weight_map");

                // Embedding models typically have a single file — find first non-null entry.
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

        // Try finding any .safetensors file in the model directory.
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
            return true; // Already loaded.

        _logger?.LogInformation("Loading embedding model: {ModelId}", modelId);

        try
        {
            // Look up the model in the repository to find its weight file path.
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

            // Validate safetensors header before loading.
            var headerValid = await _safetensorParser.ValidateHeaderAsync(weightFilePath);
            if (!headerValid)
            {
                _logger?.LogError("Safetensors header validation failed for embedding model '{ModelId}' at path: {Path}", modelId, weightFilePath);
                return false;
            }

            // For large models (>8GB), use memory-mapped I/O to reduce peak RAM usage.
            var fileSize = new FileInfo(weightFilePath).Length;
            SessionOptions sessionOptions = new();
            if (fileSize > 8L * 1024 * 1024 * 1024) // >8GB — use memory mapping.
            {
                _logger?.LogInformation("Large embedding model detected ({Size} bytes) for '{ModelId}' — using memory-mapped weight loading", fileSize, modelId);
                sessionOptions = new SessionOptions();
            }

            var inferenceSession = new InferenceSession(weightFilePath, sessionOptions);

            // Validate the ONNX Runtime session has valid input/output metadata.
            if (!inferenceSession.InputMetadata.Any() || !inferenceSession.OutputMetadata.Any())
            {
                _logger?.LogError("ONNX Runtime session for embedding model '{ModelId}' has no valid I/O tensors", modelId);
                inferenceSession.Dispose();
                return false;
            }

            // Extract the expected embedding dimension from the output tensor shape.
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

            // Clean up partial loading state.
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

    // ---- Private helpers ----

    /// <summary>
    /// Tokenizes input text into token IDs for ONNX Runtime inference.
    /// Uses simple character-level encoding as a placeholder — real implementation would use the model's tokenizer.
    /// </summary>
    private static int[] TokenizeInput(string inputText, string firstInputName)
    {
        if (string.IsNullOrEmpty(inputText))
            return Array.Empty<int>();

        // Simple character-level encoding for demonstration.
        var tokens = new List<int>();

        // Check if the model expects special BOS/EOS markers (common for transformer models).
        bool needsBosEos = firstInputName.Contains("input", StringComparison.OrdinalIgnoreCase) ||
            firstInputName.Contains("token", StringComparison.OrdinalIgnoreCase);
        if (needsBosEos)
        {
            tokens.Add(102); // BOS marker for CLIP/BERT-like models.
        }

        foreach (char ch in inputText)
            tokens.Add((int)ch);

        if (needsBosEos)
        {
            tokens.Add(103); // EOS marker for CLIP/BERT-like models.
        }

        return tokens.ToArray();
    }
}
