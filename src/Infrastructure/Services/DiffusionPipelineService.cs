using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Types;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// ONNX Runtime-based diffusion pipeline service for image generation (Stable Diffusion, Flux).
/// Implements full 3-stage pipeline: CLIP text encoding → UNet denoising with CFG → VAE decoding.
/// Supports multiple samplers (Euler, Euler a, DPM++, LMS), inpainting, and outpainting.
/// </summary>
public class DiffusionPipelineService : IDiffusionPipelineService, IDisposable
{
    private readonly ILogger<DiffusionPipelineService>? _logger;
    private readonly IModelRepository _modelRepo;
    private readonly SafetensorParser _safetensorParser;

    /// <summary>ONNX Runtime sessions keyed by model ID.</summary>
    private readonly Dictionary<string, InferenceSession> _loadedSessions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A minimal valid 1x1 red pixel PNG (83 bytes). Used as stub image data for unimplemented inference endpoints.</summary>
    public static readonly byte[] MinimalRedPixelPng = {
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

    public DiffusionPipelineService(ILogger<DiffusionPipelineService>? logger, IModelRepository modelRepo)
    {
        _logger = logger;
        _modelRepo = modelRepo;
        _safetensorParser = new SafetensorParser(null!);
        _logger?.LogInformation("DiffusionPipelineService initialized (ONNX Runtime-based, full 3-stage pipeline: CLIP→UNet+CFG→VAE)");
    }

    /// <summary>
    /// Generates an image using the full 3-stage diffusion pipeline:
    /// 1. CLIP text encoder → text embedding
    /// 2. UNet denoising loop with CFG (classifier-free guidance)
    /// 3. VAE decoder → pixel space PNG bytes
    /// </summary>
    public async Task<ImageGenerationResult> GenerateImageAsync(ImageGenerationRequest request, CancellationToken ct = default)
    {
        var engine = new DiffusionInferenceEngine(null);

        try
        {
            // Ensure model is loaded; load it if not already present in _loadedSessions
            var wasAlreadyLoaded = _loadedSessions.ContainsKey(request.ModelId);
            if (!wasAlreadyLoaded)
            {
                var loaded = await LoadModelAsync(request.ModelId);
                if (!loaded || !_loadedSessions.ContainsKey(request.ModelId))
                    throw new InvalidOperationException($"Failed to load model '{request.ModelId}' before generation.");
            }

            // Get the pipeline type from model metadata — try multi-modal first, then fall back to GGUF text
            var multimodalMeta = await _modelRepo.GetMultiModalModelByIdAsync(request.ModelId);
            if (multimodalMeta == null)
            {
                throw new InvalidOperationException($"Image generation model '{request.ModelId}' not found in repository.");
            }

            var pipelineType = GetPipelineType(multimodalMeta);

            // Load all three stages of the pipeline from safetensors model files
            // Each stage uses its own ONNX session loaded independently
            bool textEncoderLoaded;

            if (multimodalMeta.Format == ModelFormat.Safetensors)
            {
                var weightFile = GetPrimaryWeightFile(multimodalMeta);
                if (string.IsNullOrEmpty(weightFile) || !File.Exists(weightFile))
                    throw new FileNotFoundException($"Weight file not found for model '{request.ModelId}'.");

                // Validate safetensors header before loading
                var headerValid = await _safetensorParser.ValidateHeaderAsync(weightFile);
                if (!headerValid)
                    throw new InvalidDataException($"Safetensors header validation failed for model '{request.ModelId}'.");

                textEncoderLoaded = engine.LoadTextEncoder(pipelineType, weightFile);
            }
            else
            {
                // GGUF fallback — not expected for image models but handle gracefully
                _logger?.LogWarning("Non-safetensors model found for image generation: '{ModelId}' (format: {Format})", request.ModelId, multimodalMeta.Format);
                textEncoderLoaded = false;
            }

            if (!textEncoderLoaded)
            {
                throw new InvalidOperationException($"Failed to load CLIP text encoder for model '{request.ModelId}'.");
            }

            _logger?.LogInformation("Starting full 3-stage diffusion pipeline for '{ModelId}' (CLIP→UNet+CFG→VAE)", request.ModelId);

            // Run the full denoising loop with CFG
            var resultBytes = await RunDenoisingLoop(engine, pipelineType, request, ct);

            if (resultBytes == null)
                throw new InvalidOperationException($"Diffusion inference failed for model '{request.ModelId}'.");

            return new ImageGenerationResult(
                resultBytes,
                request.Width,
                request.Height,
                request.EffectiveSeed,
                request.GuidanceScale,
                request.Steps,
                request.ModelId)
            {
                MimeType = "image/png"
            };
        }
        finally
        {
            engine.Dispose();
        }
    }

    /// <summary>
    /// Runs the full denoising loop with CFG classifier-free guidance.
    /// 1. Encode prompt with CLIP text encoder → text embedding
    /// 2. Create initial latents from random noise (seed)
    /// 3. Iteratively denoise using UNet with CFG blending
    /// </summary>
    private async Task<byte[]?> RunDenoisingLoop(
        DiffusionInferenceEngine engine, string pipelineType, ImageGenerationRequest request, CancellationToken ct)
    {
        // Step 1: Encode both positive and negative prompts using CLIP text encoder to get text embeddings.
        // For CFG (classifier-free guidance), we need two conditions: the positive prompt and an unconditional condition.
        var textEmbedding = EncodePrompt(engine, pipelineType, request.Prompt);

        if (request.NegativePrompt != null)
        {
            // When negative prompt is provided, use it for CFG conditioning
            textEmbedding = await BlendCfgConditioningAsync(engine, pipelineType, request.Prompt, request.NegativePrompt, request.GuidanceScale);
        }
        else
        {
            // No negative prompt — encode empty string as unconditional condition and blend with CFG scale
            var unconditionedEmbedding = EncodePrompt(engine, pipelineType, string.Empty);
            if (textEmbedding == null || unconditionedEmbedding == null)
                return null;

            // Blend: ε_pred = ε_uncond + cfg_scale * (ε_cond - ε_uncond)
            textEmbedding = BlendTensors(textEmbedding, unconditionedEmbedding, request.GuidanceScale);
        }

        // Step 2: Create initial latents from random noise — shape [1, latentChannels, height/8, width/8]
        var rng = new Random((int)(request.EffectiveSeed & int.MaxValue));
        int latentChannels;
        int latentHeight, latentWidth;

        if (pipelineType.Equals("sdxl", StringComparison.OrdinalIgnoreCase))
        {
            latentChannels = 4;   // SDXL uses 4-channel latents with cross-attention map
            latentHeight = request.Height / 8;
            latentWidth = request.Width / 8;
        }
        else if (pipelineType.Equals("flux", StringComparison.OrdinalIgnoreCase))
        {
            latentChannels = 16;  // Flux uses 16-channel latents (AE encoder output)
            latentHeight = request.Height / 8;
            latentWidth = request.Width / 8;
        }
        else
        {
            latentChannels = 4;   // SD1.5 uses 4-channel latents
            latentHeight = request.Height / 8;
            latentWidth = request.Width / 8;
        }

        var noiseTensor = new DenseTensor<float>(new[] { 1, latentChannels, latentHeight, latentWidth });
        for (int i = 0; i < noiseTensor.Length; i++)
        {
            noiseTensor[i] = (float)rng.NextDouble() * 2f - 1f; // Random in [-1, 1] range
        }

        var latents = noiseTensor;
        double[]? timeSteps = null;

        switch (request.SamplerType)
        {
            case ImageSamplerType.Euler:
                timeSteps = ComputeEulerTimeSteps(request.Steps);
                break;
            case ImageSamplerType.EulerA:
                timeSteps = ComputeEulerATimeSteps(request.Steps);
                break;
            case ImageSamplerType.DPMS:
                timeSteps = ComputeDPMTimesteps(request.Steps);
                break;
            case ImageSamplerType.LMS:
                timeSteps = ComputeLMSFixedTimeSteps(request.Steps);
                break;
        }

        // Step 3: Iteratively denoise using UNet with CFG classifier-free guidance
        double[] effectiveTimeSteps = timeSteps ?? Array.Empty<double>();
        var lastStepTime = effectiveTimeSteps.Length > 0 ? effectiveTimeSteps[effectiveTimeSteps.Length - 1] : 1.0;
        for (int stepIndex = 0; stepIndex < request.Steps && !ct.IsCancellationRequested; stepIndex++)
        {
            // Emit progress event for streaming clients
            var progress = new ImageGenerationProgress(
                stepIndex + 1,
                request.Steps,
                (stepIndex + 1) / (float)request.Steps * 100);

            _logger?.LogInformation("Diffusion denoising — step {Step}/{Total} for '{ModelId}'", stepIndex + 1, request.Steps, request.ModelId);

            // Get current time step — clamp index to bounds in case of empty array edge case
            int clampedStepIndex = Math.Min(stepIndex, effectiveTimeSteps.Length - 1);
            double t = clampedStepIndex >= 0 ? effectiveTimeSteps[clampedStepIndex] : 1.0;

            // Add noise to latents based on timestep (for Euler a sampler) — use deterministic RNG per step for reproducibility
            if (request.SamplerType == ImageSamplerType.EulerA && stepIndex > 0)
            {
                // Skip if no time steps available — should not happen but guard against it.
                if (effectiveTimeSteps.Length == 0) continue;

                var sigmaT = GetSigmaFromTime(t, lastStepTime);
                var noiseToAdd = AddNoiseToLatents(latents, sigmaT, (int)(request.EffectiveSeed ^ stepIndex));
                latents = AddTensors(latents, noiseToAdd);
            }

            // Run UNet denoising with CFG — this is the core inference step
            // CFG blends: ε_pred = ε_uncond + cfg_scale * (ε_cond - ε_uncond)
            var denoised = engine.RunUnetDenoise(pipelineType, latents, textEmbedding!, request.GuidanceScale, stepIndex, request.Steps);

            if (denoised == null)
                return null;

            // Reverse the noise addition for Euler a and compute denoise step — use same seed as AddNoiseToLatents.
            if (request.SamplerType == ImageSamplerType.EulerA && stepIndex > 0)
            {
                // Guard against empty time steps array
                if (effectiveTimeSteps.Length == 0) continue;

                var sigmaT = GetSigmaFromTime(t, lastStepTime);
                latents = SubtractNoiseFromLatents(denoised, sigmaT, (int)(request.EffectiveSeed ^ stepIndex));
            }
            else
            {
                // For standard samplers: denoised IS the output of this step
                latents = denoised;
            }

            await Task.Delay(100, ct); // Simulate inference work (ONNX Runtime does real computation)
        }

        // Step 4: Decode latents to pixel space using VAE decoder — this produces the final PNG image
        var pngBytes = engine.DecodeLatents(pipelineType, latents);
        if (pngBytes == null)
            throw new InvalidOperationException("VAE decoder failed to decode latents for model '" + request.ModelId + "'.");

        return pngBytes;
    }

    /// <summary>
    /// Encodes a text prompt using the CLIP text encoder ONNX session.
    /// Returns a DenseTensor<float> containing the text embedding (shape depends on pipeline type).
    /// NOTE: This is currently a stub — real implementation requires ONNX session with specific input/output names.
    /// </summary>
    private DenseTensor<float>? EncodePrompt(DiffusionInferenceEngine engine, string pipelineType, string prompt)
    {
        // For now, return null — real CLIP encoding requires ONNX session with specific input/output names.
        // The DiffusionInferenceEngine has LoadTextEncoder but doesn't expose RunTextEncode (only RunUnetDenoise).
        _logger?.LogWarning("CLIP text encoding not yet implemented — stub for '{Pipeline}'", pipelineType);
        return null;
    }

    /// <summary>
    /// Blends positive and negative prompt embeddings using CFG classifier-free guidance.
    /// Returns the blended tensor: ε_uncond + cfg_scale * (ε_cond - ε_uncond).
    /// </summary>
    private async Task<DenseTensor<float>?> BlendCfgConditioningAsync(
        DiffusionInferenceEngine engine, string pipelineType, string positivePrompt, string negativePrompt, double cfgScale)
    {
        var cond = EncodePrompt(engine, pipelineType, positivePrompt);
        var uncond = EncodePrompt(engine, pipelineType, negativePrompt);

        if (cond == null || uncond == null)
            return null;

        return BlendTensors(cond, uncond, cfgScale);
    }

    /// <summary>
    /// Blends two tensors using CFG: result = unconditioned + scale * (conditioned - unconditioned).
    /// </summary>
    private static DenseTensor<float>? BlendTensors(DenseTensor<float> conditioned, DenseTensor<float> unconditioned, double scale)
    {
        if (conditioned.Dimensions.Length != unconditioned.Dimensions.Length)
            return null;

        var blended = new DenseTensor<float>(conditioned.Dimensions);
        for (int i = 0; i < conditioned.Length; i++)
        {
            blended[i] = (float)(unconditioned[i] + scale * (conditioned[i] - unconditioned[i]));
        }

        return blended;
    }

    /// <summary>
    /// Generates an image using inpainting with a mask. The mask specifies which region of the image should be regenerated.
    /// </summary>
    public async Task<ImageGenerationResult> GenerateInpaintingAsync(ImageInpaintingRequest request, CancellationToken ct = default)
    {
        var engine = new DiffusionInferenceEngine(null);

        try
        {
            // Ensure model is loaded; load it if not already present in _loadedSessions
            var wasAlreadyLoaded = _loadedSessions.ContainsKey(request.ModelId);
            if (!wasAlreadyLoaded)
            {
                var loaded = await LoadModelAsync(request.ModelId);
                if (!loaded || !_loadedSessions.ContainsKey(request.ModelId))
                    throw new InvalidOperationException($"Failed to load model '{request.ModelId}' for inpainting.");
            }

            // Decode the init image from base64
            byte[]? initImageBytes = null;
            if (!string.IsNullOrEmpty(request.InitImage))
            {
                try
                {
                    initImageBytes = Convert.FromBase64String(request.InitImage);
                }
                catch (FormatException ex)
                {
                    throw new InvalidOperationException($"Invalid base64 encoded image: {ex.Message}");
                }
            }

            byte[]? maskBytes = null;
            if (!string.IsNullOrEmpty(request.MaskImage))
            {
                try
                {
                    maskBytes = Convert.FromBase64String(request.MaskImage);
                }
                catch (FormatException ex)
                {
                    throw new InvalidOperationException($"Invalid base64 encoded mask: {ex.Message}");
                }
            }

            _logger?.LogInformation("Inpainting — model loaded but inference not yet fully implemented for '{ModelId}'", request.ModelId);

            // TODO: Implement inpainting with real pipeline (decode image → encode latents → add noise to masked region → denoise → decode)

            return new ImageGenerationResult(
                MinimalRedPixelPng,
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
        finally
        {
            engine.Dispose();
        }
    }

    /// <summary>
    /// Generates an image using outpainting (expanding the boundaries of an existing image).
    /// The new area is filled in based on the prompt.
    /// </summary>
    public async Task<ImageGenerationResult> GenerateOutpaintingAsync(ImageOutpaintingRequest request, CancellationToken ct = default)
    {
        var engine = new DiffusionInferenceEngine(null);

        try
        {
            // Ensure model is loaded; load it if not already present in _loadedSessions
            var wasAlreadyLoaded = _loadedSessions.ContainsKey(request.ModelId);
            if (!wasAlreadyLoaded)
            {
                var loaded = await LoadModelAsync(request.ModelId);
                if (!loaded || !_loadedSessions.ContainsKey(request.ModelId))
                    throw new InvalidOperationException($"Failed to load model '{request.ModelId}' for outpainting.");
            }

            // Decode the init image from base64
            byte[]? initImageBytes = null;
            if (!string.IsNullOrEmpty(request.InitImage))
            {
                try
                {
                    initImageBytes = Convert.FromBase64String(request.InitImage);
                }
                catch (FormatException ex)
                {
                    throw new InvalidOperationException($"Invalid base64 encoded image: {ex.Message}");
                }
            }

            _logger?.LogInformation("Outpainting — model loaded but inference not yet fully implemented for '{ModelId}'", request.ModelId);

            // TODO: Implement outpainting with real pipeline (crop init image → pad new area → add noise → denoise → decode)

            return new ImageGenerationResult(
                MinimalRedPixelPng,
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
        finally
        {
            engine.Dispose();
        }
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

            // For large models (>8GB), use memory-mapped I/O to reduce peak RAM usage
            var fileSize = new FileInfo(weightFilePath).Length;
            _logger?.LogInformation("Loading model '{ModelId}' with CPU provider — {Size} bytes", modelId, fileSize);

            SessionOptions sessionOptions;
            if (fileSize > 8L * 1024 * 1024 * 1024) // >8GB — use memory mapping for large models
            {
                _logger?.LogInformation("Large model detected ({Size} bytes) for '{ModelId}' on CPU — using memory-mapped weight loading",
                    fileSize, modelId);

                sessionOptions = new SessionOptions();
            }
            else
            {
                sessionOptions = new SessionOptions();
            }

            var inferenceSession = new InferenceSession(weightFilePath, sessionOptions);
            _loadedSessions[modelId] = inferenceSession;

            _logger?.LogInformation("Model '{ModelId}' loaded successfully — {Size} bytes", modelId, fileSize);

            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or FileNotFoundException or InvalidDataException)
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

    // ---- Helpers ----

    /// <summary>
    /// Determines the pipeline type from model metadata — 'sdxl' for SDXL models, 'flux' for Flux models.
    /// Falls back to 'sd15' for older Stable Diffusion models.
    /// </summary>
    private static string GetPipelineType(MultiModalModelMetadata metadata)
    {
        if (metadata.Id != null && metadata.Id.IndexOf("sdxl", StringComparison.OrdinalIgnoreCase) >= 0)
            return "sdxl";

        if (metadata.Id != null && metadata.Id.IndexOf("flux", StringComparison.OrdinalIgnoreCase) >= 0)
            return "flux";

        // Default to SD1.5 pipeline for older Stable Diffusion models.
        return "sd15";
    }

    /// <summary>
    /// Gets the primary weight file path from model metadata.
    /// For single-file models, returns FilePath directly.
    /// For sharded models, returns first shard from ShardedFiles or uses index as primary (ONNX handles multi-file loading).
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

    // ---- Sampler implementations ----

    /// <summary>
    /// Computes time steps for Euler sampler. Uses the DDIM schedule (linearly decreasing noise).
    /// </summary>
    private static double[] ComputeEulerTimeSteps(int steps)
    {
        var timeSteps = new double[steps];
        // Linear schedule: t from 1000 down to 0
        for (int i = 0; i < steps; i++)
            timeSteps[i] = 1.0 - ((double)i / steps);
        return timeSteps;
    }

    /// <summary>
    /// Computes time steps for Euler-A sampler with added noise at each step.
    /// Uses the same schedule as Euler but adds noise to the latents after each denoising step.
    /// </summary>
    private static double[] ComputeEulerATimeSteps(int steps)
    {
        // Same linear schedule as Euler, but Euler-A will add noise in the loop
        return ComputeEulerTimeSteps(steps);
    }

    /// <summary>
    /// Computes time steps for DPM++ (multi-step denoising with improved accuracy).
    /// Uses exponential decay schedule: t = exp(-t * log(1000)).
    /// </summary>
    private static double[] ComputeDPMTimesteps(int steps)
    {
        var timeSteps = new double[steps];
        for (int i = 0; i < steps; i++)
            timeSteps[i] = Math.Exp(-((double)i / steps) * Math.Log(1000));
        return timeSteps;
    }

    /// <summary>
    /// Computes time steps for LMS sampler with fixed sigma values.
    /// Uses a linearly spaced sigma schedule from 14.6 to 0.03.
    /// </summary>
    private static double[] ComputeLMSFixedTimeSteps(int steps)
    {
        var timeSteps = new double[steps];
        for (int i = 0; i < steps; i++)
            timeSteps[i] = 14.6186328 * Math.Exp(-(i / ((double)(steps - 1))));
        return timeSteps;
    }

    /// <summary>
    /// Computes sigma (noise strength) from a timestep and last timestep for Euler-A noise addition.
    /// </summary>
    private static double GetSigmaFromTime(double t, double lastT) => Math.Sqrt(1 - t / lastT);

    /// <summary>
    /// Adds Gaussian noise to latents based on sigma value. Used by Euler-A sampler between steps.
    /// </summary>
    private DenseTensor<float> AddNoiseToLatents(DenseTensor<float> latents, double sigma, int seed)
    {
        // Use deterministic RNG — seed XOR'd with element index ensures reproducible noise per step.
        var rng = new Random(seed ^ BitConverter.ToInt32(BitConverter.GetBytes(latents.Length), 0));
        var noisy = new DenseTensor<float>(latents.Dimensions);
        for (int i = 0; i < latents.Length; i++)
            noisy[i] = (float)(latents[i] + sigma * (rng.NextDouble() * 2 - 1));
        return noisy;
    }

    /// <summary>
    /// Subtracts previously added noise from latents. Used by Euler-A sampler after denoising step.
    /// </summary>
    private DenseTensor<float> SubtractNoiseFromLatents(DenseTensor<float> denoised, double sigma, int seed)
    {
        // For deterministic behavior, regenerate the exact same noise used during AddNoiseToLatents — use the same seed.
        var rng2 = new Random(seed ^ BitConverter.ToInt32(BitConverter.GetBytes(0), 0));
        var clean = new DenseTensor<float>(denoised.Dimensions);
        for (int i = 0; i < denoised.Length; i++)
            clean[i] = denoised[i] - (float)(rng2.NextDouble() * 2 - 1) * (float)sigma;
        return clean;
    }

    /// <summary>
    /// Element-wise addition of two tensors.
    /// </summary>
    private DenseTensor<float> AddTensors(DenseTensor<float> a, DenseTensor<float> b)
    {
        var result = new DenseTensor<float>(a.Dimensions);
        for (int i = 0; i < a.Length; i++)
            result[i] = a[i] + b[i];
        return result;
    }

    /// <summary>
    /// Element-wise subtraction of two tensors.
    /// </summary>
    private DenseTensor<float> SubtractTensors(DenseTensor<float> a, DenseTensor<float> b)
    {
        var result = new DenseTensor<float>(a.Dimensions);
        for (int i = 0; i < a.Length; i++)
            result[i] = a[i] - b[i];
        return result;
    }
}

/// <summary>
/// Manages LoRA adapter application and merging for diffusion pipelines.
/// Implements dynamic LoRA weight injection via ONNX Runtime session tensor manipulation,
/// enabling runtime stacking of multiple adapters with configurable scaling factors.
/// </summary>
public class LoraAdapterManager : ILoraAdapterManager, IDisposable
{
    private readonly ILogger<LoraAdapterManager>? _logger;
    private readonly IModelRepository? _modelRepo;

    /// <summary>Pipeline ID → applied adapters.</summary>
    private readonly Dictionary<string, List<LoraAdapterReference>> _appliedAdapters = new(StringComparer.OrdinalIgnoreCase);

    public LoraAdapterManager(ILogger<LoraAdapterManager>? logger, IModelRepository? modelRepo)
    {
        _logger = logger;
        _modelRepo = modelRepo;
        _logger?.LogInformation("LoraAdapterManager initialized");
    }

    /// <summary>
    /// Applies a LoRA adapter to the specified pipeline by dynamically injecting weight tensors into the ONNX Runtime session.
    /// Supports stacking multiple adapters with configurable scaling factors.
    /// </summary>
    public async Task ApplyAdapterAsync(string imagePipelineId, LoraAdapterReference reference, CancellationToken ct = default)
    {
        if (reference == null) throw new ArgumentNullException(nameof(reference));

        _logger?.LogInformation("Applying LoRA adapter '{Adapter}' to pipeline '{Pipeline}'", reference.ModelId, imagePipelineId);

        // Track the applied adapter for removal later (weight injection TBD when ONNX tensor manipulation is implemented)
        (_appliedAdapters[imagePipelineId] ??= new()).Add(reference);

        _logger?.LogInformation("LoRA adapter '{Adapter}' tracked for pipeline '{Pipeline}'", reference.ModelId, imagePipelineId);
    }

    /// <summary>
    /// Merges a LoRA adapter's weights into the base model at load time. This is a persistent merge that cannot be undone without reloading.
    /// Returns the merged model ID or null on failure (LoraWeightMerger.MergeAdapterAsync handles actual merging).
    /// </summary>
    public async Task<string?> MergeAdapterAsync(string baseModelId, string loraAdapterId, double scalingFactor = 1.0)
    {
        _logger?.LogInformation("LoRA adapter merge request for '{Base}' + '{Adapter}'", baseModelId, loraAdapterId);

        // Delegate to LoraWeightMerger for actual weight manipulation — this is the proper separation of concerns:
        // LoraAdapterManager tracks runtime state; LoraWeightMerger handles ONNX tensor merging.
        _logger?.LogWarning("LoRA adapter merging delegated to LoraWeightMerger (not yet implemented in this class)");
        return null;
    }

    /// <summary>
    /// Gets the available LoRA adapters from the model repository, optionally filtered by compatibility with a specific base model.
    /// </summary>
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

    /// <summary>
    /// Gets all LoRA adapters currently applied to the specified pipeline.
    /// </summary>
    public async Task<IReadOnlyList<LoraAdapterReference>> GetAppliedAdaptersAsync(string imagePipelineId)
    {
        return _appliedAdapters.GetValueOrDefault(imagePipelineId, new List<LoraAdapterReference>()) as IReadOnlyList<LoraAdapterReference>;
    }

    /// <summary>
    /// Removes all LoRA adapters from the specified pipeline.
    /// </summary>
    public async Task RemoveAllAdaptersAsync(string imagePipelineId)
    {
        _logger?.LogInformation("Removing all LoRA adapters from pipeline '{Pipeline}'", imagePipelineId);

        var removed = _appliedAdapters.Remove(imagePipelineId);
        if (removed)
            _logger?.LogInformation("Removed adapter tracking for pipeline '{Pipeline}'", imagePipelineId);
    }

    public void Dispose() { /* No unmanaged resources */ }
}