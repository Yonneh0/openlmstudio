using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// JSON-based repository for discovering and indexing model files (GGUF + Safetensors) on disk.
/// Maintains an index file for fast lookups without rescanning on every access.
/// Supports multi-model types: text generation (GGUF), image generation/diffusion/VAE/LoRA/embedding (Safetensors).
/// </summary>
public class JsonModelRepository : IModelRepository, IDisposable
{
    private const string IndexFileName = "model-index.json";
    private readonly ILogger<JsonModelRepository> _logger;
    private readonly GgufParser _ggufParser;
    private readonly SafetensorParser _safetensorParser;
    private readonly string _indexDirectory;
    private readonly List<string> _modelSearchPaths;

    // Cached index of models (in-memory for performance)
    private Dictionary<string, ModelMetadata>? _indexedGgufModels;
    private Dictionary<string, MultiModalModelMetadata>? _indexedMultiModalModels;

    /// <summary>
    /// Initializes a new instance of the JsonModelRepository.
    /// </summary>
    public JsonModelRepository(
        ILogger<JsonModelRepository> logger,
        GgufParser ggufParser,
        string? modelSearchPath = null)
    {
        _logger = logger;
        _ggufParser = ggufParser;
        // Note: Cannot pass ILogger<JsonModelRepository> to SafetensorParser (different generic type).
        // Pass null - SafetensorParser handles null loggers safely via ?. pattern internally.
        _safetensorParser = new SafetensorParser(null!);

        // Default to user's local app data folder for models (per cross-platform spec 10.X.2: model storage is NOT in AppData — separate from contexts/metadata)
        _indexDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenLMStudio", "models");

        // Search paths for GGUF models (text generation) and safetensors models (multi-modal)
        var defaultPaths = new List<string> { Path.Combine(_indexDirectory, "gguf") };

        if (!string.IsNullOrEmpty(modelSearchPath))
            defaultPaths.Add(modelSearchPath);

        _modelSearchPaths = defaultPaths;

        // Ensure directories exist
        Directory.CreateDirectory(_indexDirectory);
        foreach (var path in _modelSearchPaths)
            Directory.CreateDirectory(path);
    }

    /// <summary>
    /// Scans all configured model directories for both GGUF and Safetensors models, indexing them.
    /// Loads cached index first, then validates each entry and scans for new files.
    /// </summary>
    public async Task<IEnumerable<ModelMetadata>> DiscoverModelsAsync()
    {
        _logger.LogInformation("Discovering models in: {Paths}", string.Join(", ", _modelSearchPaths));

        // Load existing indices if available (separate for GGUF and multi-modal)
        _indexedGgufModels ??= LoadIndex();
        _indexedMultiModalModels ??= LoadMultiModalIndex();

        // Validate existing entries (remove missing files)
        var toRemove = new List<string>();
        foreach (var modelId in _indexedGgufModels.Keys)
        {
            if (!File.Exists(_indexedGgufModels[modelId].FilePath))
                toRemove.Add(modelId);
        }
        foreach (var id in toRemove)
            _indexedGgufModels.Remove(id);

        toRemove.Clear();
        foreach (var modelId in _indexedMultiModalModels.Keys)
        {
            if (!File.Exists(_indexedMultiModalModels[modelId].FilePath))
                toRemove.Add(modelId);
        }
        foreach (var id in toRemove)
            _indexedMultiModalModels.Remove(id);

        // Scan for GGUF files not yet indexed
        var existingGgufIds = new HashSet<string>(_indexedGgufModels.Keys, StringComparer.OrdinalIgnoreCase);

        foreach (var searchPath in _modelSearchPaths)
        {
            if (!Directory.Exists(searchPath)) continue;

            var ggufFiles = Directory.GetFiles(searchPath, "*.gguf", SearchOption.AllDirectories);

            foreach (var file in ggufFiles)
            {
                var fileId = Path.GetFileNameWithoutExtension(file);
                if (!existingGgufIds.Contains(fileId))
                    await IndexSingleFileAsync(file);
            }

            // Scan for safetensors models not yet indexed
            var existingMultiModalIds = new HashSet<string>(_indexedMultiModalModels.Keys, StringComparer.OrdinalIgnoreCase);

            foreach (var file in Directory.GetFiles(searchPath, "*.safetensors", SearchOption.AllDirectories))
            {
                var fileId = Path.GetFileNameWithoutExtension(file);
                if (!existingMultiModalIds.Contains(fileId))
                    await IndexSingleSafetensorFileAsync(file);
            }

            // Scan for sharded models (index files) not yet indexed
            foreach (var indexFile in Directory.GetFiles(searchPath, "*.safetensors.index.json", SearchOption.AllDirectories))
            {
                var baseName = Path.GetFileNameWithoutExtension(indexFile).Replace(".index", "");
                if (!existingMultiModalIds.Contains(baseName))
                    await IndexShardedModelAsync(indexFile);
            }
        }

        SaveIndex();
        return GetAllModels();
    }

    /// <summary>
    /// Gets metadata for a specific model by its ID (filename without extension).
    /// Searches both GGUF and multi-modal indices.
    /// </summary>
    public async Task<ModelMetadata?> GetModelByIdAsync(string modelId)
    {
        // First check GGUF models
        if (_indexedGgufModels != null && _indexedGgufModels.TryGetValue(modelId, out var ggufModel))
            return ggufModel;

        // Then check multi-modal models (convert to ModelMetadata for compatibility)
        if (_indexedMultiModalModels != null && _indexedMultiModalModels.TryGetValue(modelId, out var multimodalModel))
        {
            var modelMeta = ConvertToModelMetadata(multimodalModel);
            return modelMeta;
        }

        return null;
    }

    /// <summary>
    /// Gets multi-modal (safetensors-based) model metadata for a specific model by its ID.
    /// Returns null if not found in the multi-modal index or if it's a GGUF model.
    /// </summary>
    public async Task<MultiModalModelMetadata?> GetMultiModalModelByIdAsync(string modelId)
    {
        // Check multi-modal models directly (do NOT return GGUF models as MultiModalModelMetadata)
        if (_indexedMultiModalModels != null && _indexedMultiModalModels.TryGetValue(modelId, out var multimodalModel))
            return multimodalModel;

        return null;
    }

    /// <summary>
    /// Searches for models (both GGUF and multi-modal) matching the given filter criteria.
    /// Supports searching by name, architecture/model type, and format.
    /// </summary>
    public async Task<IEnumerable<ModelMetadata>> SearchModelsAsync(string searchTerm, string? architecture = null)
    {
        var allGgufModels = DiscoverGgufModelsOnly();

        // Build multi-modal model list from index (convert to ModelMetadata wrapper for compatibility)
        var allMultiModalModels = _indexedMultiModalModels != null
            ? _indexedMultiModalModels.Values.Select(ConvertToModelMetadata).ToList()
            : new List<ModelMetadata>();

        // Combine both lists and search across them
        var allResults = (allGgufModels.Concat(allMultiModalModels) as IEnumerable<ModelMetadata>) ?? Enumerable.Empty<ModelMetadata>();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLowerInvariant();
            allResults = allResults.Where(m =>
                m.Name.ToLowerInvariant().Contains(term) ||
                m.Architecture.ToLowerInvariant().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(architecture))
        {
            // Match against architecture for GGUF models, or model type for multi-modal models (stored in Architecture field as alias)
            allResults = allResults.Where(m =>
                m.Architecture.Equals(architecture, StringComparison.OrdinalIgnoreCase) ||
                m.Type.Equals("multimodal", StringComparison.OrdinalIgnoreCase));
        }

        return allResults.ToList();
    }

    /// <summary>
    /// Searches for multi-modal (safetensors-based) models matching the given criteria.
    /// Filters by model type, search term, and pipeline type (e.g., "sdxl", "sd15").
    /// </summary>
    public async Task<IEnumerable<MultiModalModelMetadata>> SearchMultiModalModelsAsync(
        string? searchTerm = null,
        ModelType? modelTypeFilter = null,
        string? pipelineTypeFilter = null)
    {
        if (_indexedMultiModalModels == null)
            _indexedMultiModalModels = LoadMultiModalIndex();

        var results = _indexedMultiModalModels.Values.AsEnumerable();

        // Apply search term filter (matches against model name or ID)
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLowerInvariant();
            results = results.Where(m =>
                m.Name.ToLowerInvariant().Contains(term) ||
                m.Id.ToLowerInvariant().Contains(term));
        }

        // Apply model type filter
        if (modelTypeFilter.HasValue)
            results = results.Where(m => m.ModelType == modelTypeFilter.Value);

        // Apply pipeline type filter (e.g., "sdxl" for SDXL models, "sd15" for SD 1.5)
        if (!string.IsNullOrWhiteSpace(pipelineTypeFilter))
        {
            var pt = pipelineTypeFilter.ToLowerInvariant();
            results = results.Where(m =>
                m.PipelineType?.ToLowerInvariant().Contains(pt) == true ||
                m.CompatibleBaseModel?.ToLowerInvariant() == pt);
        }

        return results.ToList();
    }

    /// <summary>
    /// Gets all GGUF models filtered by architecture type.
    /// </summary>
    public async Task<IEnumerable<ModelMetadata>> GetModelsByArchitectureAsync(string architecture)
    {
        var allGgufModels = DiscoverGgufModelsOnly();
        return allGgufModels.Where(m => m.Architecture.Equals(architecture, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Updates or adds GGUF model metadata to the index.
    /// For multi-modal models, use SaveMultiModalModelMetadataAsync instead.
    /// </summary>
    public async Task SaveModelMetadataAsync(ModelMetadata metadata)
    {
        if (_indexedGgufModels == null || _indexedMultiModalModels == null)
            await DiscoverModelsAsync(); // Ensure indices are loaded

        // Determine which type of model this is and update the appropriate index
        var isMultiModal = metadata.Type.Equals("multimodal", StringComparison.OrdinalIgnoreCase);

        if (isMultiModal)
        {
            if (_indexedMultiModalModels == null) _indexedMultiModalModels = new Dictionary<string, MultiModalModelMetadata>();

            // Note: ModelMetadata and MultiModalModelMetadata are unrelated types — cannot use 'as' cast.
            // Callers should use SaveMultiModalModelMetadataAsync for multimodal models instead of SaveModelMetadataAsync.
            _logger.LogWarning("SaveModelMetadata called with Type='multimodal' but model is not a MultiModalModelMetadata type. " +
                "Use SaveMultiModalModelMetadataAsync to save multi-modal metadata properly.");
        }
        else
        {
            if (_indexedGgufModels == null) _indexedGgufModels = new Dictionary<string, ModelMetadata>();
            _indexedGgufModels[metadata.Id] = metadata;
        }

        SaveIndex();
    }

    /// <summary>
    /// Saves or updates a multi-modal model's metadata in the index.
    /// </summary>
    public async Task SaveMultiModalModelMetadataAsync(MultiModalModelMetadata metadata)
    {
        if (_indexedMultiModalModels == null) _indexedMultiModalModels = new Dictionary<string, MultiModalModelMetadata>();

        _indexedMultiModalModels[metadata.Id] = metadata;
        SaveIndex();
    }

    /// <summary>
    /// Removes a model from the index without deleting the file.
    /// </summary>
    public async Task RemoveFromIndexAsync(string modelId)
    {
        if (_indexedGgufModels == null) _indexedGgufModels = LoadIndex();

        // Try to remove from GGUF index first
        _indexedGgufModels.Remove(modelId);

        // If not found, try multi-modal index
        if (!_indexedGgufModels.ContainsKey(modelId))
            _indexedMultiModalModels?.Remove(modelId);

        SaveIndex();
    }

    // ---- Convenience methods (interface aliases) ----

    public async Task<ModelMetadata?> GetMetadataAsync(string id) => await GetModelByIdAsync(id);

    public async Task SaveMetadataAsync(ModelMetadata metadata) => await SaveModelMetadataAsync(metadata);

    public async Task RemoveMetadataAsync(string id) => await RemoveFromIndexAsync(id);

    public async Task<IEnumerable<ModelMetadata>> GetModelsByQuantizationAsync(string quantization)
    {
        var allModels = DiscoverGgufModelsOnly();
        return allModels.Where(m => m.Quantization.Equals(quantization, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<ModelMetadata?> GetByFilePathAsync(string filePath)
    {
        if (_indexedGgufModels == null) _indexedGgufModels = LoadIndex();

        foreach (var model in _indexedGgufModels.Values)
        {
            if (string.Equals(model.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                return model;
        }

        if (_indexedMultiModalModels != null)
        {
            foreach (var model in _indexedMultiModalModels.Values)
            {
                if (string.Equals(model.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                    return ConvertToModelMetadata(model);
            }
        }

        return null;
    }

    /// <summary>
    /// Gets a list of all unique architectures (GGUF) found in the indexed models.
    /// Multi-modal pipeline types are returned separately via GetAvailablePipelineTypesAsync.
    /// </summary>
    public async Task<IEnumerable<string>> GetAvailableArchitecturesAsync()
    {
        var allGguf = DiscoverGgufModelsOnly();
        return allGguf.Select(m => m.Architecture).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets a list of available pipeline types for multi-modal models (e.g., "sdxl", "sd15").
    /// </summary>
    public async Task<IEnumerable<string>> GetAvailablePipelineTypesAsync()
    {
        if (_indexedMultiModalModels == null) _indexedMultiModalModels = LoadMultiModalIndex();

        return _indexedMultiModalModels.Values
            .Where(m => !string.IsNullOrEmpty(m.PipelineType))
            .Select(m => m.PipelineType!)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets a list of all models (alias for DiscoverModelsAsync).
    /// </summary>
    public async Task<IReadOnlyList<ModelMetadata>> ListModelsAsync()
    {
        var allModels = await DiscoverModelsAsync();
        return new List<ModelMetadata>(allModels);
    }

    /// <summary>
    /// Gets a list of all models, optionally from a specific path.
    /// </summary>
    public async Task<IReadOnlyList<ModelMetadata>> ListModelsAsync(string? path)
    {
        if (path != null && path != _modelSearchPaths[0])
        {
            // If a specific path is requested, temporarily update search paths
            var originalPaths = _modelSearchPaths.ToList();
            try
            {
                _modelSearchPaths.Clear();
                _modelSearchPaths.Add(path);
                var allModels = await DiscoverModelsAsync();
                return new List<ModelMetadata>(allModels);
            }
            finally
            {
                _modelSearchPaths.Clear();
                _modelSearchPaths.AddRange(originalPaths);
            }
        }
        return await ListModelsAsync();
    }

    /// <summary>
    /// Discovers models from a specific path.
    /// </summary>
    public async Task<IEnumerable<ModelMetadata>> DiscoverModelsAsync(string path)
    {
        // Temporarily set the search path
        var originalPaths = _modelSearchPaths.ToList();
        try
        {
            _modelSearchPaths.Clear();
            _modelSearchPaths.Add(path);
            return await DiscoverModelsAsync();
        }
        finally
        {
            _modelSearchPaths.Clear();
            _modelSearchPaths.AddRange(originalPaths);
        }
    }

    /// <summary>
    /// Gets a list of all multi-modal models (safetensors-based).
    /// </summary>
    public async Task<IReadOnlyList<MultiModalModelMetadata>> ListMultiModalModelsAsync()
    {
        if (_indexedMultiModalModels == null) _indexedMultiModalModels = LoadMultiModalIndex();
        return new List<MultiModalModelMetadata>(_indexedMultiModalModels.Values);
    }

    /// <summary>
    /// Gets the index directory path (contains model-index.json).
    /// </summary>
    public string GetIndexDirectory() => _indexDirectory;

    // ---- Internal Methods (GGUF) ----

    /// <summary>Loads the GGUF model index from disk.</summary>
    private Dictionary<string, ModelMetadata> LoadIndex()
    {
        var indexPath = Path.Combine(_indexDirectory, IndexFileName);

        if (!File.Exists(indexPath))
            return new Dictionary<string, ModelMetadata>();

        try
        {
            var json = File.ReadAllText(indexPath);
            var models = JsonSerializer.Deserialize<Dictionary<string, ModelMetadata>>(json);

            // Validate entries still exist on disk
            if (models != null)
            {
                var validModels = new Dictionary<string, ModelMetadata>();
                foreach (var kvp in models)
                {
                    if (File.Exists(kvp.Value.FilePath))
                        validModels[kvp.Key] = kvp.Value;
                    else
                        _logger.LogDebug("Removed stale index entry: {FilePath}", kvp.Value.FilePath);
                }
                return validModels;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load model index");
        }

        return new Dictionary<string, ModelMetadata>();
    }

    /// <summary>Loads the multi-modal model index from disk.</summary>
    private Dictionary<string, MultiModalModelMetadata> LoadMultiModalIndex()
    {
        var mmIndexPath = Path.Combine(_indexDirectory, "model-index-multimodal.json");

        if (!File.Exists(mmIndexPath))
            return new Dictionary<string, MultiModalModelMetadata>();

        try
        {
            var json = File.ReadAllText(mmIndexPath);
            var models = JsonSerializer.Deserialize<Dictionary<string, MultiModalModelMetadata>>(json);

            // Validate entries still exist on disk
            if (models != null)
            {
                var validModels = new Dictionary<string, MultiModalModelMetadata>();
                foreach (var kvp in models)
                {
                    if (File.Exists(kvp.Value.FilePath))
                        validModels[kvp.Key] = kvp.Value;
                    else
                        _logger.LogDebug("Removed stale multi-modal index entry: {FilePath}", kvp.Value.FilePath);
                }
                return validModels;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _logger.LogError(ex, "Failed to load multi-modal model index");
        }

        return new Dictionary<string, MultiModalModelMetadata>();
    }

    /// <summary>Saves both GGUF and multi-modal indices to disk.</summary>
    private void SaveIndex()
    {
        try
        {
            var indexPath = Path.Combine(_indexDirectory, IndexFileName);

            // Save GGUF models as the primary index (backward compatible)
            if (_indexedGgufModels != null && _indexedGgufModels.Count > 0)
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                File.WriteAllText(indexPath, JsonSerializer.Serialize(_indexedGgufModels, options));
            }

            // Also save multi-modal index separately for efficient lookup
            if (_indexedMultiModalModels != null && _indexedMultiModalModels.Count > 0)
            {
                var mmIndexPath = Path.Combine(_indexDirectory, "model-index-multimodal.json");
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                File.WriteAllText(mmIndexPath, JsonSerializer.Serialize(_indexedMultiModalModels, options));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save model index");
        }
    }

    private IReadOnlyList<ModelMetadata> GetAllModels()
    {
        var results = new List<ModelMetadata>();

        if (_indexedGgufModels != null)
            results.AddRange(_indexedGgufModels.Values);

        // Convert multi-modal models to ModelMetadata wrapper for compatibility
        if (_indexedMultiModalModels != null)
        {
            foreach (var multimodal in _indexedMultiModalModels.Values)
            {
                var modelMeta = ConvertToModelMetadata(multimodal);
                results.Add(modelMeta);
            }
        }

        return results;
    }

    private IReadOnlyList<ModelMetadata> DiscoverGgufModelsOnly()
    {
        if (_indexedGgufModels == null) _indexedGgufModels = LoadIndex();

        var results = new List<ModelMetadata>();
        if (_indexedGgufModels != null)
            results.AddRange(_indexedGgufModels.Values);

        return results;
    }

    private ModelMetadata ConvertToModelMetadata(MultiModalModelMetadata multimodal)
    {
        return new ModelMetadata
        {
            Id = multimodal.Id,
            Name = multimodal.Name,
            FilePath = multimodal.FilePath,
            Architecture = multimodal.ModelType.ToString(), // Use model type as architecture for compatibility
            Quantization = "unknown",
            Type = "multimodal",
            FileSizeBytes = multimodal.FileSizeBytes,
            LastModified = multimodal.LastModified,
        };
    }

    private async Task IndexShardedModelAsync(string indexPath)
    {
        var indexInfo = await _safetensorParser.ParseIndexAsync(indexPath);
        if (indexInfo == null || !indexInfo.IsComplete)
        {
            _logger?.LogWarning("Sharded model incomplete: {FilePath}", indexPath);
            return;
        }

        // Use the base name of the index file as the model ID
        var baseName = Path.GetFileNameWithoutExtension(indexPath).Replace(".index", "");
        var id = Path.GetFileNameWithoutExtension(baseName);

        if (string.IsNullOrEmpty(id)) return;

        try
        {
            _indexedMultiModalModels ??= LoadMultiModalIndex();

            // Create a MultiModalModelMetadata from the index info
            var metadata = new MultiModalModelMetadata
            {
                Id = id,
                Name = id,
                FilePath = indexPath,
                ModelType = ModelType.ImageGeneration, // Default type — can be refined by parsing the weight_map for LoRA/VAE/etc.
                Format = ModelFormat.Safetensors,
                ShardedFiles = indexInfo.ResolvedShardFiles.ToList(),
                FileSizeBytes = (long)indexInfo.ResolvedShardFiles.Sum(f => new FileInfo(f).Length),
                LastModified = File.GetLastWriteTimeUtc(indexPath)
            };

            // Infer model type from the weight_map content (e.g., LoRA adapters typically have "lora" in tensor names)
            var hasLoraWeights = indexInfo.ShardFileNames.Any(f => f.Contains("adapter", StringComparison.OrdinalIgnoreCase));
            if (hasLoraWeights)
                metadata.ModelType = ModelType.Lora;

            _indexedMultiModalModels[id] = metadata;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to index sharded model: {FilePath}", indexPath);
        }
    }

    /// <summary>Indexes a single GGUF file and adds it to the GGUF index.</summary>
    private async Task IndexSingleFileAsync(string filePath)
    {
        var ggufHeaderInfo = await _ggufParser.ParseHeaderAsync(filePath);
        if (ggufHeaderInfo != null && !string.IsNullOrEmpty(ggufHeaderInfo.Architecture))
        {
            var metadata = ggufHeaderInfo.ToModelMetadata();

            // Ensure Id matches filename without extension
            var fileId = Path.GetFileNameWithoutExtension(filePath);
            if (string.IsNullOrEmpty(fileId)) return;

            metadata.Id = fileId;
            _indexedGgufModels ??= LoadIndex();
            _indexedGgufModels[fileId] = metadata;
            _logger.LogDebug("Indexed GGUF model: {Id} ({Architecture})", metadata.Id, metadata.Architecture);
        }
    }

    /// <summary>Indexes a single safetensors file and adds it to the multi-modal index.</summary>
    private async Task IndexSingleSafetensorFileAsync(string filePath)
    {
        var headerInfo = await _safetensorParser.ParseHeaderAsync(filePath);
        if (headerInfo == null || headerInfo.TotalTensorCount == 0) return;

        var fileId = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrEmpty(fileId)) return;

        try
        {
            _indexedMultiModalModels ??= LoadMultiModalIndex();

            // Create a ModelMetadata wrapper for compatibility with existing GGUF code paths
            var modelMeta = new ModelMetadata
            {
                Id = fileId,
                Name = headerInfo.GlobalMetadata.GetValueOrDefault("model_name", fileId),
                FilePath = filePath,
                Architecture = "safetensors", // Use format as architecture for backward compat
                Quantization = "unknown",
                Type = "multimodal",
                FileSizeBytes = headerInfo.FileSizeBytes,
                LastModified = File.GetLastWriteTimeUtc(filePath),
            };

            // Also create the full MultiModalModelMetadata for multi-modal specific operations
            var multimodalMeta = new MultiModalModelMetadata
            {
                Id = fileId,
                Name = modelMeta.Name,
                FilePath = filePath,
                ModelType = headerInfo.GlobalMetadata.GetValueOrDefault("model_type", "imagegeneration") switch
                {
                    "lora" or "loralike" => ModelType.Lora,
                    "vae" => ModelType.Vae,
                    "embedding" => ModelType.Embedding,
                    _ => ModelType.ImageGeneration // Default to image generation (SDXL/Flux etc.)
                },
                Format = ModelFormat.Safetensors,
                TensorShapes = headerInfo.TensorsMetadata.ToDictionary(t => t.Key, t => t.Value.Shape),
                TensorDtypes = headerInfo.TensorsMetadata.ToDictionary(t => t.Key, t => t.Value.Dtype),
                FileSizeBytes = headerInfo.FileSizeBytes,
                LastModified = modelMeta.LastModified,
                IsActive = false,
            };

            // Extract LoRA-specific metadata if applicable
            var hasLoraWeights = headerInfo.TensorsMetadata.Any(t =>
                t.Key.Contains("lora", StringComparison.OrdinalIgnoreCase) ||
                t.Value.Shape.Length == 2 && t.Value.Shape[1] < t.Value.Shape[0]); // Likely rank matrix

            if (hasLoraWeights)
            {
                multimodalMeta.ModelType = ModelType.Lora;

                // Detect LoRA variant format from tensor names
                var hasHadamardTransforms = headerInfo.TensorsMetadata.Any(t => t.Key.Contains("_hadamard", StringComparison.OrdinalIgnoreCase));
                if (hasHadamardTransforms)
                    multimodalMeta.LoraFormatVariant = LoraFormatVariant.LoHa;
                else if (headerInfo.TensorsMetadata.Any(t => t.Key.Contains("kron", StringComparison.OrdinalIgnoreCase)))
                    multimodalMeta.LoraFormatVariant = LoraFormatVariant.LoKr;

                // Extract rank from shape
                foreach (var tensor in headerInfo.TensorsMetadata.Values.Where(t => t.Shape.Length == 2 && t.Shape[1] < t.Shape[0]))
                {
                    multimodalMeta.Rank = (int)tensor.Shape[1];

                    // Calculate scaling factor as alpha/rank if lora_alpha is present
                    if (headerInfo.GlobalMetadata.TryGetValue("lora_alpha", out var alphaStr))
                    {
                        if (double.TryParse(alphaStr, out var alphaValue) && alphaValue > 0)
                            multimodalMeta.ScalingFactor = alphaValue / (multimodalMeta.Rank ?? 1);
                    }

                    break; // Use first rank found
                }
            }

            // Extract image generation parameters if applicable
            if (!hasLoraWeights && headerInfo.GlobalMetadata.TryGetValue("pipeline_type", out var pipelineType))
            {
                multimodalMeta.PipelineType = pipelineType;

                // Try to extract resolution from tensor shapes (first dimension of diffusion UNet)
                foreach (var tensor in headerInfo.TensorsMetadata.Values.Where(t =>
                    t.Shape.Length == 4 && t.Shape[0] == 1)) // Likely a convolutional layer with batch dim
                {
                    multimodalMeta.DefaultResolution = Math.Max((int)Math.Sqrt(tensor.Shape[2] * tensor.Shape[3]), 512);
                    break;
                }

                if (headerInfo.GlobalMetadata.TryGetValue("training_steps", out var stepsStr))
                {
                    int.TryParse(stepsStr, out var steps);
                    multimodalMeta.TrainingSteps = steps > 0 ? steps : null;
                }
            }

            _indexedMultiModalModels[fileId] = multimodalMeta;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index safetensors file: {FilePath}", filePath);
        }
    }

    public void Dispose()
    {
        // Save any pending index changes on disposal
        if ((_indexedGgufModels != null && _indexedGgufModels.Count > 0) ||
            (_indexedMultiModalModels != null && _indexedMultiModalModels.Count > 0))
            SaveIndex();
    }
}
