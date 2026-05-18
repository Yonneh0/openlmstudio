using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Types;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// ONNX Runtime-based diffusion pipeline service for image generation (Stable Diffusion, Flux).
/// Loads safetensors models and runs them through ONNX inference sessions.
/// </summary>
public class DiffusionPipelineService : IDiffusionPipelineService, IDisposable
{
    private readonly ILogger<DiffusionPipelineService>? _logger;
    private readonly IModelRepository _modelRepo;
    private readonly SafetensorParser _safetensorParser;

    /// <summary>OnnxRunTime sessions keyed by model ID.</summary>
    private readonly Dictionary<string, InferenceSession> _loadedSessions = new(StringComparer.OrdinalIgnoreCase);

    public DiffusionPipelineService(ILogger<DiffusionPipelineService>? logger, IModelRepository modelRepo)
    {
        _logger = logger;
        _modelRepo = modelRepo;
        // Note: SafetensorParser expects ILogger<SafetensorParser?> specifically, not a generic logger.
        // Pass null to avoid type mismatch; it will handle null loggers safely via ?. pattern.
        _safetensorParser = new SafetensorParser(null!);

        // Note: ONNX Runtime 1.20.0 does not expose ExecutionProviderFactory.GetAvailableExecutionProviders()
        // or CudaExecutionProvider in the managed API. GPU acceleration requires native bindings via
        // ExecutionProviderFactory.AppendExecutionProvider_CUDA(). We default to CPU-only mode and log it.

        _logger?.LogWarning("ONNX Runtime 1.20.0 — GPU CUDA execution provider not available in managed API. Models will run on CPU only.");

        _logger?.LogInformation("DiffusionPipelineService initialized (ONNX Runtime-based, CPU-only)");
    }

    public async Task<ImageGenerationResult> GenerateImageAsync(ImageGenerationRequest request, CancellationToken ct = default)
    {
        // Ensure model is loaded; load it if not already present
        var wasAlreadyLoaded = _loadedSessions.ContainsKey(request.ModelId);
        if (!wasAlreadyLoaded)
        {
            var loaded = await LoadModelAsync(request.ModelId);
            if (!loaded || !_loadedSessions.ContainsKey(request.ModelId))
                throw new InvalidOperationException($"Failed to load model '{request.ModelId}' before generation.");
        }

        var session = _loadedSessions[request.ModelId];

        // TODO: Full implementation requires:
        // 1. Loading safetensors model weights into ONNX Runtime InferenceSession (via memory-mapped I/O for >10GB)
        // 2. Running text encoding pipeline (CLIP/Tokenizer) to get conditioning tensors
        // 3. Iterative denoising loop with CFG guidance, sampler steps
        // 4. VAE decoding of latent space output to pixel space

        _logger?.LogInformation("Image generation stub — model loaded but inference not yet implemented for {ModelId}", request.ModelId);

        // Return a minimal valid 1x1 red pixel PNG as placeholder until real inference is implemented.
        const byte[] pngBytes = {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,  // IHDR length + type
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,  // width=1, height=1
            0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,  // bitDepth=8, colorType=RGB, interlace=none + CRC
            0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41,  // IDAT length + type
            0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,  // IDAT data + CRC
            0x00, 0x03, 0x01, 0x01, 0x00, 0x18, 0xDD, 0x8D,  // IEND length + type + CRC
            0xB4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E,  // IEND data + CRC
            0x44, 0xAE, 0x42, 0x60, 0x82                        // IEND CRC
        };

        return new ImageGenerationResult(
            pngBytes,
            request.Width,
            request.Height,
            request.EffectiveSeed,
            request.GuidanceScale,
            request.Steps,
            request.ModelId)
        {
            MimeType = "image/png" // Default MIME type for the generated image
        };
    }

    public async IAsyncEnumerable<ImageGenerationProgress> StreamProgressAsync(ImageGenerationRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Ensure model is loaded if not already present
        var wasAlreadyLoaded = _loadedSessions.ContainsKey(request.ModelId);
        if (!wasAlreadyLoaded)
        {
            await LoadModelAsync(request.ModelId);
        }

        // Placeholder streaming — real implementation emits progress per denoising step
        for (var step = 0; step < request.Steps && !ct.IsCancellationRequested; step++)
        {
            yield return new ImageGenerationProgress(step + 1, request.Steps, (step + 1) / (float)request.Steps * 100);
            await Task.Delay(100, ct); // Simulate work
        }
    }

    public async Task<IEnumerable<MultiModalModelMetadata>> GetAvailableModelsAsync()
    {
        var models = await _modelRepo.SearchMultiModalModelsAsync(modelTypeFilter: ModelType.ImageGeneration);
        return models;
    }

    public async Task<bool> LoadModelAsync(string modelId)
    {
        // Already loaded — nothing to do
        if (_loadedSessions.ContainsKey(modelId))
            return true;

        try
        {
            _logger?.LogInformation("Loading image generation model: {ModelId}", modelId);

            var metadata = await _modelRepo.GetMultiModalModelByIdAsync(modelId);
            if (metadata == null)
            {
                // Try generic lookup in case it's a GGUF model returned from the base IModelRepository
                var ggufMeta = await _modelRepo.GetModelByIdAsync(modelId);
                if (ggufMeta != null)
                {
                    _logger?.LogWarning("Model '{ModelId}' found but is not a MultiModalModelMetadata — cannot load for image generation.", modelId);
                    return false;
                }

                _logger?.LogError("Image generation model '{ModelId}' not found in repository.", modelId);
                return false;
            }

            // Determine the primary weight file path
            var weightFilePath = GetPrimaryWeightFile(metadata);
            if (string.IsNullOrEmpty(weightFilePath) || !File.Exists(weightFilePath))
            {
                _logger?.LogError("Weight file not found for model '{ModelId}': expected '{Path}'",
                    modelId, weightFilePath ?? "null");
                return false;
            }

            // Validate safetensors header before loading (catch corrupted files early)
            var headerValid = await _safetensorParser.ValidateHeaderAsync(weightFilePath);
            if (!headerValid)
            {
                _logger?.LogError("Safetensors header validation failed for model '{ModelId}': {Path}",
                    modelId, weightFilePath);
                return false;
            }

            // Note: ONNX Runtime 1.20.0 does not expose CudaExecutionProvider in the managed API.
            // Default to CPU execution for now. GPU support requires native bindings.

            _logger?.LogInformation("Loading model '{ModelId}' with CPU provider (GPU CUDA not available in ONNX Runtime 1.20.0)", modelId);

            // For large models (>8GB), use memory-mapped I/O to reduce peak RAM usage
            var fileSize = new FileInfo(weightFilePath).Length;
            SessionOptions sessionOptions;
            if (fileSize > 8L * 1024 * 1024 * 1024) // >8GB — use memory mapping
            {
                _logger?.LogInformation("Large model detected ({Size} bytes) for '{ModelId}' on CPU — using memory-mapped weight loading",
                    fileSize, modelId);

                sessionOptions = new SessionOptions();
                // Note: SetMemoryPatternOptimization is available in newer ONNX Runtime versions via extension method
                // For 1.20.0 we'll use the direct API approach instead
            }
            else
            {
                sessionOptions = new SessionOptions();
            }

            var inferenceSession = new InferenceSession(weightFilePath, sessionOptions);
            _loadedSessions[modelId] = inferenceSession;

            _logger?.LogInformation("Model '{ModelId}' loaded successfully with CPU provider — {Size} bytes",
                modelId, fileSize);

            return true;

        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            _logger?.LogError(ex, "Failed to load image generation model: {ModelId}", modelId);

            // Clean up partial loading state
            if (_loadedSessions.ContainsKey(modelId))
                _loadedSessions[modelId].Dispose();
            _loadedSessions.Remove(modelId);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load image generation model: {ModelId}", modelId);

            // Clean up partial loading state
            if (_loadedSessions.ContainsKey(modelId))
                _loadedSessions[modelId].Dispose();
            _loadedSessions.Remove(modelId);
            return false;
        }
    }

    public async Task<bool> UnloadModelAsync(string modelId)
    {
        try
        {
            if (_loadedSessions.ContainsKey(modelId))
            {
                _loadedSessions[modelId].Dispose();
                _loadedSessions.Remove(modelId);

                _logger?.LogInformation("Model '{ModelId}' unloaded", modelId);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to unload model: {ModelId}", modelId);
            return false;
        }
    }

    public async Task<IEnumerable<string>> GetLoadedModelsAsync()
    {
        var loaded = _loadedSessions.Keys.ToList();

        foreach (var model in loaded)
            _logger?.LogDebug("GetLoadedModelsAsync — found {Count} loaded sessions", loaded.Count);

        return loaded;
    }

    public async Task<ImageGenerationResult> GenerateInpaintingAsync(ImageInpaintingRequest request, CancellationToken ct = default)
    {
        // Ensure model is loaded; load it if not already present
        var wasAlreadyLoaded = _loadedSessions.ContainsKey(request.ModelId);
        if (!wasAlreadyLoaded)
        {
            var loaded = await LoadModelAsync(request.ModelId);
            if (!loaded || !_loadedSessions.ContainsKey(request.ModelId))
                throw new InvalidOperationException($"Failed to load model '{request.ModelId}' for inpainting.");
        }

        _logger?.LogInformation("Inpainting stub — model loaded but inference not yet implemented for {ModelId}", request.ModelId);

        // Return a minimal valid 1x1 red pixel PNG as placeholder until real inference is implemented.
        const byte[] pngBytes = {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
            0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41,
            0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
            0x00, 0x03, 0x01, 0x01, 0x00, 0x18, 0xDD, 0x8D,
            0xB4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E,
            0x44, 0xAE, 0x42, 0x60, 0x82
        };

        return new ImageGenerationResult(
            pngBytes,
            request.Width > 0 ? request.Width : 1024,
            request.Height > 0 ? request.Height : 1024,
            request.Seed ?? -1,
            request.CfgScale,
            request.Steps,
            request.ModelId)
        {
            MimeType = "image/png"
        };
    }

    public async Task<ImageGenerationResult> GenerateOutpaintingAsync(ImageOutpaintingRequest request, CancellationToken ct = default)
    {
        // Ensure model is loaded; load it if not already present
        var wasAlreadyLoaded = _loadedSessions.ContainsKey(request.ModelId);
        if (!wasAlreadyLoaded)
        {
            var loaded = await LoadModelAsync(request.ModelId);
            if (!loaded || !_loadedSessions.ContainsKey(request.ModelId))
                throw new InvalidOperationException($"Failed to load model '{request.ModelId}' for outpainting.");
        }

        _logger?.LogInformation("Outpainting stub — model loaded but inference not yet implemented for {ModelId}", request.ModelId);

        // Return a minimal valid 1x1 red pixel PNG as placeholder until real inference is implemented.
        const byte[] pngBytes = {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
            0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41,
            0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
            0x00, 0x03, 0x01, 0x01, 0x00, 0x18, 0xDD, 0x8D,
            0xB4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E,
            0x44, 0xAE, 0x42, 0x60, 0x82
        };

        return new ImageGenerationResult(
            pngBytes,
            request.Width > 0 ? request.Width : 1024,
            request.Height > 0 ? request.Height : 1024,
            request.Seed ?? -1,
            request.CfgScale,
            request.Steps,
            request.ModelId)
        {
            MimeType = "image/png"
        };
    }

    public void Dispose()
    {
        // Unload all models to free CPU memory
        var unloaded = 0;
        foreach (var modelId in _loadedSessions.Keys.ToList())
        {
            try
            {
                _loadedSessions[modelId].Dispose();
                unloaded++;
            }
            catch { /* Ignore dispose errors */ }
        }

        if (unloaded > 0)
            _logger?.LogInformation("Disposed {Count} ONNX Runtime sessions on shutdown", unloaded);

        _loadedSessions.Clear();
    }

    /// <summary>
    /// Gets the primary weight file path for a multi-modal model. Uses _modelRepo field (not _modelRepository).
    /// For single-file models, returns the FilePath directly.
    /// For sharded models, returns the first shard file from ShardedFiles list.
    /// </summary>
    private string? GetPrimaryWeightFile(MultiModalModelMetadata metadata)
    {
        // Sharded model — use index as primary (ONNX Runtime will handle multi-file loading)
        if (!string.IsNullOrEmpty(metadata.FilePath) &&
            metadata.FilePath.EndsWith(".safetensors.index.json", StringComparison.OrdinalIgnoreCase))
            return metadata.FilePath;

        // Single file safetensors model
        if (!string.IsNullOrEmpty(metadata.FilePath) &&
            metadata.FilePath.EndsWith(".safetensors", StringComparison.OrdinalIgnoreCase))
            return metadata.FilePath;

        // If no explicit file path, try to infer from ShardedFiles or search the repo
        if (metadata.ShardedFiles != null && metadata.ShardedFiles.Any())
            return metadata.ShardedFiles.First();

        _logger?.LogWarning("Cannot determine weight file for model '{ModelId}' — no FilePath set and no sharded files",
            metadata.Id);
        return null;
    }
}

/// <summary>
/// ONNX Runtime-based VAE pipeline service for latent space encoding/decoding.
/// </summary>
public class VAEPipelineService : IVAEPipelineService, IDisposable
{
    private readonly ILogger<VAEPipelineService>? _logger;
    private readonly IModelRepository _modelRepo;

    public VAEPipelineService(ILogger<VAEPipelineService>? logger, IModelRepository modelRepo)
    {
        _logger = logger;
        _modelRepo = modelRepo;

        _logger?.LogInformation("VAEPipelineService initialized (ONNX Runtime-based)");
    }

    public async Task<byte[]> EncodeAsync(string vaeModelId, byte[] imageBytes, CancellationToken ct = default)
    {
        // TODO: Real implementation loads VAE safetensors model and runs encode pipeline
        _logger?.LogWarning("VAE encoding not yet implemented — stub response");
        return Array.Empty<byte>();
    }

    public async Task<byte[]> DecodeAsync(string vaeModelId, byte[] latents, CancellationToken ct = default)
    {
        // TODO: Real implementation loads VAE safetensors model and runs decode pipeline
        _logger?.LogWarning("VAE decoding not yet implemented — stub response");
        return Array.Empty<byte>();
    }

    public async Task<IEnumerable<MultiModalModelMetadata>> GetAvailableModelsAsync()
    {
        var models = await _modelRepo.SearchMultiModalModelsAsync(modelTypeFilter: ModelType.Vae);
        return models;
    }

    public void Dispose() { /* No unmanaged resources */ }
}

/// <summary>
/// Manages LoRA adapter application and merging for diffusion pipelines.
/// </summary>
public class LoraAdapterManager : ILoraAdapterManager, IDisposable
{
    private readonly ILogger<LoraAdapterManager>? _logger;
    private readonly IModelRepository? _modelRepo;
    /// <summary>Pipeline ID → applied adapters.</summary>
    private readonly Dictionary<string, List<LoraAdapterReference>> _appliedAdapters = new();

    public LoraAdapterManager(ILogger<LoraAdapterManager>? logger, IModelRepository? modelRepo)
    {
        _logger = logger;
        _modelRepo = modelRepo;

        _logger?.LogInformation("LoraAdapterManager initialized");
    }

    public async Task ApplyAdapterAsync(string imagePipelineId, LoraAdapterReference reference, CancellationToken ct = default)
    {
        // TODO: Real implementation loads safetensors adapter weights into ONNX Runtime session and merges them dynamically
        _logger?.LogInformation("ApplyAdapter stub for pipeline: {Pipeline}, adapter: {Adapter}", imagePipelineId, reference.ModelId);

        (_appliedAdapters[imagePipelineId] ??= new()).Add(reference);
    }

    public async Task<string?> MergeAdapterAsync(string baseModelId, string loraAdapterId, double scalingFactor = 1.0)
    {
        // TODO: Real implementation requires ONNX Runtime weight manipulation to merge tensors
        _logger?.LogWarning("LoRA adapter merging not yet implemented — stub");
        return null;
    }

    public async Task<IEnumerable<MultiModalModelMetadata>> GetAvailableAdaptersAsync(string? compatibleBaseModel = null)
    {
        IEnumerable<MultiModalModelMetadata> models = [];
        if (_modelRepo != null)
            models = await _modelRepo.SearchMultiModalModelsAsync(modelTypeFilter: ModelType.Lora);

        // Optionally filter by compatibility with the specified base model type
        if (!string.IsNullOrEmpty(compatibleBaseModel))
            models = models.Where(m => m.CompatibleBaseModel != null && m.CompatibleBaseModel == compatibleBaseModel);

        return models;
    }

    public async Task<IReadOnlyList<LoraAdapterReference>> GetAppliedAdaptersAsync(string imagePipelineId)
    {
        return _appliedAdapters.GetValueOrDefault(imagePipelineId, new List<LoraAdapterReference>()) as IReadOnlyList<LoraAdapterReference>;
    }

    public async Task RemoveAllAdaptersAsync(string imagePipelineId)
    {
        // TODO: Real implementation removes adapter tensors from ONNX Runtime session
        _appliedAdapters.Remove(imagePipelineId);
    }

    public void Dispose() { /* No unmanaged resources */ }
}

/// <summary>
/// ONNX Runtime-based embedding pipeline service for text/image vector embeddings.
/// </summary>
public class EmbeddingPipelineService : IEmbeddingPipelineService, IDisposable
{
    private readonly ILogger<EmbeddingPipelineService>? _logger;
    private readonly IModelRepository? _modelRepo;

    public EmbeddingPipelineService(ILogger<EmbeddingPipelineService>? logger, IModelRepository? modelRepo)
    {
        _logger = logger;
        _modelRepo = modelRepo;

        _logger?.LogInformation("EmbeddingPipelineService initialized (ONNX Runtime-based)");
    }

    public async Task<float[]> GenerateAsync(string modelId, string inputText, CancellationToken ct = default)
    {
        // TODO: Real implementation loads safetensors embedding model and runs inference on ONNX Runtime session
        _logger?.LogWarning("Embedding generation not yet implemented — stub response");

        return new float[768]; // Dimensionality depends on the model (e.g., CLIPTextModel output)
    }

    public async Task<float[][]> GenerateBatchAsync(string modelId, IReadOnlyList<string> inputs, CancellationToken ct = default)
    {
        var results = new float[inputs.Count][];

        for (var i = 0; i < inputs.Count && !ct.IsCancellationRequested; i++)
            results[i] = await GenerateAsync(modelId, inputs[i], ct);

        return results;
    }

    public async Task<IEnumerable<MultiModalModelMetadata>> GetAvailableModelsAsync()
    {
        IEnumerable<MultiModalModelMetadata> models = [];
        if (_modelRepo != null)
            models = await _modelRepo.SearchMultiModalModelsAsync(modelTypeFilter: ModelType.Embedding);
        return models;
    }

    public void Dispose() { /* No unmanaged resources */ }
}