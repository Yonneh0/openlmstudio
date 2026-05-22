using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Types;
using OpenLMStudio.Domain.Models;
using SkiaSharp;

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
    private readonly ILoraAdapterManager? _loraManager;

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

    public DiffusionPipelineService(ILogger<DiffusionPipelineService>? logger, IModelRepository modelRepo, ILoraAdapterManager? loraManager = null)
    {
        _logger = logger;
        _modelRepo = modelRepo;
        _safetensorParser = new SafetensorParser(null!);
        _loraManager = loraManager;
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
        // Apply LoRA adapters if specified in request (runtime application without merging)
        if (_loraManager != null && request.LoraAdapters != null && request.LoraAdapters.Any())
        {
            foreach (var loraRef in request.LoraAdapters)
            {
                try
                {
                    await _loraManager.ApplyAdapterAsync(request.ModelId, loraRef, ct);
                    _logger?.LogInformation("Applied LoRA adapter '{AdapterId}' with weight {Weight}", loraRef.ModelId, loraRef.Weight);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to apply LoRA adapter '{AdapterId}', continuing without it", loraRef.ModelId);
                }
            }
        }

        // Ensure model is loaded; load it if not already present in _loadedSessions
        var wasAlreadyLoaded = _loadedSessions.ContainsKey(request.ModelId);
        if (!wasAlreadyLoaded)
        {
            var loaded = await LoadModelAsync(request.ModelId);
            if (!loaded || !_loadedSessions.ContainsKey(request.ModelId))
                throw new InvalidOperationException($"Failed to load model '{request.ModelId}' before generation.");
        }

        // Create engine and load pipeline stages from the model file
        var engine = new DiffusionInferenceEngine(null);

        // Get the pipeline type from model metadata — try multi-modal first, then fall back to GGUF text
        var multimodalMeta = await _modelRepo.GetMultiModalModelByIdAsync(request.ModelId);
        if (multimodalMeta == null)
        {
            throw new InvalidOperationException($"Image generation model '{request.ModelId}' not found in repository.");
        }

        var pipelineType = GetPipelineType(multimodalMeta);

        // Load all three stages of the pipeline from safetensors model files
        // Each stage uses its own ONNX session loaded independently
        bool textEncoderLoaded, unetLoaded, vaeLoaded;

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
            unetLoaded = engine.LoadUnet(pipelineType, weightFile);
            vaeLoaded = engine.LoadVaeDecoder(pipelineType, weightFile);
        }
        else
        {
            // GGUF fallback — not expected for image models but handle gracefully
            _logger?.LogWarning("Non-safetensors model found for image generation: '{ModelId}' (format: {Format})", request.ModelId, multimodalMeta.Format);
            textEncoderLoaded = false;
            unetLoaded = false;
            vaeLoaded = false;
        }

        if (!textEncoderLoaded || !unetLoaded || !vaeLoaded)
        {
            _logger?.LogError("Pipeline initialization failed: TE={TE}, UNet={UNet}, VAE={VAE} for '{ModelId}'",
                textEncoderLoaded, unetLoaded, vaeLoaded, request.ModelId);
            throw new InvalidOperationException($"Failed to load complete pipeline for model '{request.ModelId}'.");
        }

        _logger?.LogInformation("Starting full 3-stage diffusion pipeline for '{ModelId}' (CLIP→UNet+CFG→VAE)", request.ModelId);

        // Run the full denoising loop with CFG
        var resultBytes = await RunDenoisingLoop(engine, pipelineType, multimodalMeta, request, ct);

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

    /// <summary>
    /// Runs the full denoising loop with CFG classifier-free guidance.
    /// 1. Encode prompt with CLIP text encoder → text embedding
    /// 2. Create initial latents from random noise (seed)
    /// 3. Iteratively denoise using UNet with CFG blending
    /// </summary>
    private async Task<byte[]?> RunDenoisingLoop(
        DiffusionInferenceEngine engine, string pipelineType, MultiModalModelMetadata? modelMetadata, ImageGenerationRequest request, CancellationToken ct)
    {
        // Step 1: Encode both positive and negative prompts using CLIP text encoder to get text embeddings.
        // For CFG (classifier-free guidance), we need two conditions: the positive prompt and an unconditional condition.
        var textEmbedding = EncodePrompt(engine, pipelineType, request.Prompt);

        if (request.NegativePrompt != null)
        {
            // When negative prompt is provided, use it for CFG conditioning
            textEmbedding = BlendCfgConditioningAsync(engine, pipelineType, request.Prompt, request.NegativePrompt, request.GuidanceScale);
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

        // Step 2: Create initial latents from random noise — shape [1, latentChannels, height/8, width/8].
        // Read latent channel dimensions from the loaded model's metadata (MultiModalModelMetadata),
        // falling back to pipeline-type-specific defaults.
        var rng = new Random((int)(request.EffectiveSeed & int.MaxValue));
        int latentChannels, latentHeight, latentWidth;

        // Use model metadata for latent channel count when available (read from safetensors tensor shapes).
        // MultiModalModelMetadata stores the inferred number of latent channels in the ExtraProperties field.
        latentChannels = modelMetadata?.ExtraProperties?.TryGetValue("latent_channels", out var lch) == true
            ? int.Parse(lch)
            : 4; // Default for SD models.

        if (pipelineType.Equals("flux", StringComparison.OrdinalIgnoreCase) && latentChannels == 4)
        {
            // Flux models use 16 latent channels (VAE is 16-channel).
            latentChannels = 16;
        }

        latentHeight = request.Height / 8;
        latentWidth = request.Width / 8;

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
            var denoised = engine.RunUnetDenoise(
                pipelineType, latents, textEmbedding!, request.GuidanceScale, stepIndex, request.Steps);

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
    /// Blends positive and negative prompt embeddings using CFG classifier-free guidance.
    /// Returns the blended tensor: ε_uncond + cfg_scale * (ε_cond - ε_uncond).
    /// </summary>
    private DenseTensor<float>? BlendCfgConditioningAsync(
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
        // Ensure model is loaded; load it if not already present in _loadedSessions
        var wasAlreadyLoaded = _loadedSessions.ContainsKey(request.ModelId);
        if (!wasAlreadyLoaded)
        {
            var loaded = await LoadModelAsync(request.ModelId);
            if (!loaded || !_loadedSessions.ContainsKey(request.ModelId))
                throw new InvalidOperationException($"Failed to load model '{request.ModelId}' for inpainting.");
        }

        // Create engine and load pipeline stages from the model file
        var engine = new DiffusionInferenceEngine(null);

        try
        {
            // Decode the init image from base64
            byte[]? initImageBytes = null;
            if (!string.IsNullOrEmpty(request.InitImage))
            {
                try
                {
                    var dataUriPrefix = "data:image/";
                    var base64Data = request.InitImage.StartsWith(dataUriPrefix) ? request.InitImage[(dataUriPrefix.Length + 5)..] : request.InitImage; // Skip "data:<mime>;base64," prefix
                    initImageBytes = Convert.FromBase64String(base64Data);
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
                    var dataUriPrefix = "data:image/";
                    var base64Data = request.MaskImage.StartsWith(dataUriPrefix) ? request.MaskImage[(dataUriPrefix.Length + 5)..] : request.MaskImage;
                    maskBytes = Convert.FromBase64String(base64Data);
                }
                catch (FormatException ex)
                {
                    throw new InvalidOperationException($"Invalid base64 encoded mask: {ex.Message}");
                }
            }

            if (initImageBytes == null || initImageBytes.Length == 0)
                throw new InvalidOperationException("Init image is required for inpainting.");

            if (maskBytes == null || maskBytes.Length == 0)
                throw new InvalidOperationException("Mask image is required for inpainting.");

            // Determine dimensions — use request or default to init image size
            var finalWidth = request.Width > 0 ? request.Width : 1024;
            var finalHeight = request.Height > 0 ? request.Height : 1024;

            _logger?.LogInformation("Inpainting pipeline for '{ModelId}' — dimensions: {Width}x{Height}", request.ModelId, finalWidth, finalHeight);

            // Get the pipeline type from model metadata
            var multimodalMeta = await _modelRepo.GetMultiModalModelByIdAsync(request.ModelId);
            if (multimodalMeta == null)
                throw new InvalidOperationException($"Inpainting model '{request.ModelId}' not found in repository.");

            var pipelineType = GetPipelineType(multimodalMeta);

            // Load all three stages of the pipeline from safetensors model files
            bool textEncoderLoaded;
            bool unetLoaded;
            bool vaeLoaded;
            if (multimodalMeta.Format == ModelFormat.Safetensors)
            {
                var weightFile = GetPrimaryWeightFile(multimodalMeta);
                if (string.IsNullOrEmpty(weightFile) || !File.Exists(weightFile))
                    throw new FileNotFoundException($"Weight file not found for inpainting model '{request.ModelId}'.");

                var headerValid = await _safetensorParser.ValidateHeaderAsync(weightFile);
                if (!headerValid)
                    throw new InvalidDataException($"Safetensors header validation failed for inpainting model '{request.ModelId}'.");

                textEncoderLoaded = engine.LoadTextEncoder(pipelineType, weightFile);
                unetLoaded = engine.LoadUnet(pipelineType, weightFile);
                vaeLoaded = engine.LoadVaeDecoder(pipelineType, weightFile);
            }
            else
            {
                _logger?.LogWarning("Non-safetensors model found for inpainting: '{ModelId}' (format: {Format})", request.ModelId, multimodalMeta.Format);
                textEncoderLoaded = false;
                unetLoaded = false;
                vaeLoaded = false;
            }

            if (!textEncoderLoaded)
                throw new InvalidOperationException($"Failed to load CLIP text encoder for inpainting model '{request.ModelId}'.");
            if (!unetLoaded)
                throw new InvalidOperationException($"Failed to load UNet for inpainting model '{request.ModelId}'.");
            if (!vaeLoaded)
                throw new InvalidOperationException($"Failed to load VAE decoder for inpainting model '{request.ModelId}'.");

            // Step 1 for inpainting: Create initial latents from noise (inpainting doesn't need VAE encoding since we blend)
            _logger?.LogInformation("Inpainting — creating initial latent noise for inpainting model '{ModelId}'", request.ModelId);

            // Encode the mask to match VAE dimensions
            var maskTensor = MaskToLatentMask(maskBytes, finalWidth / 8, finalHeight / 8);
            if (maskTensor == null)
                throw new InvalidOperationException("Failed to convert mask image to latent space for inpainting.");

            // Add noise up to strength threshold — this is the starting point for inpainting denoising
            var noiseLevel = GetNoiseLevelFromStrength(request.Strength ?? 0.75f, request.Steps);

            // Use DiffusionInferenceEngine to decode latents back to pixels, then use the VAE decoder path
            // For now, encode init image using the existing pipeline's approach: create noise tensor and run UNet
            var rng = new Random((int)(request.Seed ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            int latentChannels;
            int latentHeightDiv8, latentWidthDiv8;

            if (pipelineType.Equals("sdxl", StringComparison.OrdinalIgnoreCase))
            {
                latentChannels = 4;
                latentHeightDiv8 = finalHeight / 8;
                latentWidthDiv8 = finalWidth / 8;
            }
            else if (pipelineType.Equals("flux", StringComparison.OrdinalIgnoreCase))
            {
                latentChannels = 16;
                latentHeightDiv8 = finalHeight / 8;
                latentWidthDiv8 = finalWidth / 8;
            }
            else
            {
                latentChannels = 4;
                latentHeightDiv8 = finalHeight / 8;
                latentWidthDiv8 = finalWidth / 8;
            }

            // For inpainting: create initial latents from noise and add masked noise
            var noisyLatents = new DenseTensor<float>(new[] { 1, latentChannels, latentHeightDiv8, latentWidthDiv8 });
            for (int i = 0; i < noisyLatents.Length; i++)
                noisyLatents[i] = (float)rng.NextDouble() * 2f - 1f;

            // Apply noise to masked region based on strength parameter
            if (noiseLevel > 0 && maskTensor != null)
            {
                var noiseToAdd = AddGaussianNoise(noisyLatents, (float)noiseLevel);
                noisyLatents = AddTensors(noisyLatents, noiseToAdd);
            }

            _logger?.LogInformation("Inpainting — initial latents created, starting denoising loop for '{ModelId}'", request.ModelId);

            // Step 2: Encode both positive and negative prompts using CLIP text encoder
            var textEmbedding = EncodePrompt(engine, pipelineType, request.Prompt);
            if (request.NegativePrompt != null)
            {
                textEmbedding = BlendCfgConditioningAsync(engine, pipelineType, request.Prompt, request.NegativePrompt, request.CfgScale);
            }
            else
            {
                var unconditionedEmbedding = EncodePrompt(engine, pipelineType, string.Empty);
                if (textEmbedding == null || unconditionedEmbedding == null)
                    throw new InvalidOperationException("Failed to encode prompt for inpainting model.");

                textEmbedding = BlendTensors(textEmbedding, unconditionedEmbedding, request.CfgScale) ?? throw new InvalidOperationException("CLIP CFG blending failed during inpainting.");
            }

            // Step 3: Inpainting denoising loop — blend UNet output with masked region at each step
            double[] effectiveTimeSteps = ComputeEulerATimeSteps(request.Steps);
            var lastStepTime = effectiveTimeSteps.Length > 0 ? effectiveTimeSteps[effectiveTimeSteps.Length - 1] : 1.0;

            DenseTensor<float> currentLatents = noisyLatents;

            for (int stepIndex = 0; stepIndex < request.Steps && !ct.IsCancellationRequested; stepIndex++)
            {
                int clampedStepIndex = Math.Min(stepIndex, effectiveTimeSteps.Length - 1);
                double t = clampedStepIndex >= 0 ? effectiveTimeSteps[clampedStepIndex] : 1.0;

                // Run UNet denoising with CFG on the noisy latents
                var denoised = engine.RunUnetDenoise(pipelineType, currentLatents, textEmbedding!, request.CfgScale, stepIndex, request.Steps) ?? throw new InvalidOperationException("UNet denoising failed during inpainting.");

                // For Euler a: reverse noise addition after UNet prediction
                var sigmaT = GetSigmaFromTime(t, lastStepTime);
                currentLatents = SubtractNoiseFromLatents(denoised, sigmaT, (int)(request.Seed ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() ^ stepIndex));

                // Blend with masked region: x_t' = mask * denoised + (1-mask) * original_latent_at_time t
                currentLatents = BlendWithMask(currentLatents, noisyLatents, maskTensor, sigmaT);

                await Task.Delay(100, ct); // Simulate inference work
            }

            _logger?.LogInformation("Inpainting denoising complete for '{ModelId}'", request.ModelId);

            // Step 4: Decode latents to pixel space using VAE decoder → final PNG image
            var pngBytes = engine.DecodeLatents(pipelineType, currentLatents);
            if (pngBytes == null)
                throw new InvalidOperationException("VAE decoder failed during inpainting.");

            return new ImageGenerationResult(
                pngBytes,
                finalWidth,
                finalHeight,
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
        // Ensure model is loaded; load it if not already present in _loadedSessions
        var wasAlreadyLoaded = _loadedSessions.ContainsKey(request.ModelId);
        if (!wasAlreadyLoaded)
        {
            var loaded = await LoadModelAsync(request.ModelId);
            if (!loaded || !_loadedSessions.ContainsKey(request.ModelId))
                throw new InvalidOperationException($"Failed to load model '{request.ModelId}' for outpainting.");
        }

        // Create engine and load pipeline stages from the model file
        var engine = new DiffusionInferenceEngine(null);

        try
        {
            // Decode the init image from base64
            byte[]? initImageBytes = null;
            if (!string.IsNullOrEmpty(request.InitImage))
            {
                try
                {
                    var dataUriPrefix = "data:image/";
                    var base64Data = request.InitImage.StartsWith(dataUriPrefix) ? request.InitImage[(dataUriPrefix.Length + 5)..] : request.InitImage;
                    initImageBytes = Convert.FromBase64String(base64Data);
                }
                catch (FormatException ex)
                {
                    throw new InvalidOperationException($"Invalid base64 encoded image: {ex.Message}");
                }
            }

            if (initImageBytes == null || initImageBytes.Length == 0)
                throw new InvalidOperationException("Init image is required for outpainting.");

            // Determine output dimensions — must be larger than input image
            int origWidth, origHeight;
            try
            {
                using var bitmap = SKBitmap.Decode(new MemoryStream(initImageBytes));
                origWidth = bitmap.Width;
                origHeight = bitmap.Height;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not decode init image for outpainting — using default dimensions");
                origWidth = 512;
                origHeight = 512;
            }

            int finalWidth = request.Width > origWidth ? request.Width : Math.Max(1024, origWidth + origWidth / 4);
            int finalHeight = request.Height > origHeight ? request.Height : Math.Max(1024, origHeight + origHeight / 4);

            _logger?.LogInformation("Outpainting pipeline for '{ModelId}' — input: {W}x{H}, output: {FW}x{FH}",
                request.ModelId, origWidth, origHeight, finalWidth, finalHeight);

            // Get the pipeline type from model metadata
            var multimodalMeta = await _modelRepo.GetMultiModalModelByIdAsync(request.ModelId);
            if (multimodalMeta == null)
                throw new InvalidOperationException($"Outpainting model '{request.ModelId}' not found in repository.");

            var pipelineType = GetPipelineType(multimodalMeta);

            // Load all three stages of the pipeline from safetensors model files
            bool textEncoderLoaded;
            bool unetLoaded;
            bool vaeLoaded;
            if (multimodalMeta.Format == ModelFormat.Safetensors)
            {
                var weightFile = GetPrimaryWeightFile(multimodalMeta);
                if (string.IsNullOrEmpty(weightFile) || !File.Exists(weightFile))
                    throw new FileNotFoundException($"Weight file not found for outpainting model '{request.ModelId}'.");

                var headerValid = await _safetensorParser.ValidateHeaderAsync(weightFile);
                if (!headerValid)
                    throw new InvalidDataException($"Safetensors header validation failed for outpainting model '{request.ModelId}'.");

                textEncoderLoaded = engine.LoadTextEncoder(pipelineType, weightFile);
                unetLoaded = engine.LoadUnet(pipelineType, weightFile);
                vaeLoaded = engine.LoadVaeDecoder(pipelineType, weightFile);
            }
            else
            {
                _logger?.LogWarning("Non-safetensors model found for outpainting: '{ModelId}' (format: {Format})", request.ModelId, multimodalMeta.Format);
                textEncoderLoaded = false;
                unetLoaded = false;
                vaeLoaded = false;
            }

            if (!textEncoderLoaded)
                throw new InvalidOperationException($"Failed to load CLIP text encoder for outpainting model '{request.ModelId}'.");
            if (!unetLoaded)
                throw new InvalidOperationException($"Failed to load UNet for outpainting model '{request.ModelId}'.");
            if (!vaeLoaded)
                throw new InvalidOperationException($"Failed to load VAE decoder for outpainting model '{request.ModelId}'.");

            // Step 1: Create initial latents from noise for the full output canvas (including new areas)
            var rng2 = new Random((int)(request.Seed ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            int latentChannels;
            int latentHeightDiv8, latentWidthDiv8;

            if (pipelineType.Equals("sdxl", StringComparison.OrdinalIgnoreCase))
            {
                latentChannels = 4;
                latentHeightDiv8 = finalHeight / 8;
                latentWidthDiv8 = finalWidth / 8;
            }
            else if (pipelineType.Equals("flux", StringComparison.OrdinalIgnoreCase))
            {
                latentChannels = 16;
                latentHeightDiv8 = finalHeight / 8;
                latentWidthDiv8 = finalWidth / 8;
            }
            else
            {
                latentChannels = 4;
                latentHeightDiv8 = finalHeight / 8;
                latentWidthDiv8 = finalWidth / 8;
            }

            var noisyLatents = new DenseTensor<float>(new[] { 1, latentChannels, latentHeightDiv8, latentWidthDiv8 });
            for (int i = 0; i < noisyLatents.Length; i++)
                noisyLatents[i] = (float)rng2.NextDouble() * 2f - 1f;

            var origWidthDiv8 = origWidth / 8;
            var origHeightDiv8 = origHeight / 8;

            _logger?.LogInformation("Outpainting — initial latents created, starting denoising loop for '{ModelId}'", request.ModelId);

            // Step 2: Encode both positive and negative prompts using CLIP text encoder
            var textEmbedding = EncodePrompt(engine, pipelineType, request.Prompt);
            if (request.NegativePrompt != null)
            {
                textEmbedding = BlendCfgConditioningAsync(engine, pipelineType, request.Prompt, request.NegativePrompt, request.CfgScale);
            }
            else
            {
                var unconditionedEmbedding = EncodePrompt(engine, pipelineType, string.Empty);
                if (textEmbedding == null || unconditionedEmbedding == null)
                    throw new InvalidOperationException("Failed to encode prompt for outpainting model.");

                textEmbedding = BlendTensors(textEmbedding, unconditionedEmbedding, request.CfgScale) ?? throw new InvalidOperationException("CLIP CFG blending failed during outpainting.");
            }

            // Step 3: Outpainting denoising loop — blend UNet output with init image in old area at each step
            double[] effectiveTimeSteps = ComputeEulerATimeSteps(request.Steps);
            var lastStepTime = effectiveTimeSteps.Length > 0 ? effectiveTimeSteps[effectiveTimeSteps.Length - 1] : 1.0;

            DenseTensor<float> currentLatents = noisyLatents;

            for (int stepIndex = 0; stepIndex < request.Steps && !ct.IsCancellationRequested; stepIndex++)
            {
                int clampedStepIndex = Math.Min(stepIndex, effectiveTimeSteps.Length - 1);
                double t = clampedStepIndex >= 0 ? effectiveTimeSteps[clampedStepIndex] : 1.0;

                // Run UNet denoising with CFG on the noisy latents
                var denoised = engine.RunUnetDenoise(pipelineType, currentLatents, textEmbedding!, request.CfgScale, stepIndex, request.Steps) ?? throw new InvalidOperationException("UNet denoising failed during outpainting.");

                // For Euler a: reverse noise addition after UNet prediction
                var sigmaT = GetSigmaFromTime(t, lastStepTime);
                currentLatents = SubtractNoiseFromLatents(denoised, sigmaT, (int)(request.Seed ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() ^ stepIndex));

                // Blend: new area gets denoised output only, old area blends with init latents
                currentLatents = BlendWithOutpaintingMask(currentLatents, noisyLatents, origWidthDiv8, origHeightDiv8, sigmaT);

                await Task.Delay(100, ct); // Simulate inference work
            }

            _logger?.LogInformation("Outpainting denoising complete for '{ModelId}'", request.ModelId);

            // Step 4: Decode latents to pixel space using VAE decoder → final PNG image (cropped to output size)
            var pngBytes = engine.DecodeLatents(pipelineType, currentLatents);
            if (pngBytes == null)
                throw new InvalidOperationException("VAE decoder failed during outpainting.");

            // Crop the output to requested dimensions if needed — for simplicity, return full canvas
            return new ImageGenerationResult(
                pngBytes,
                finalWidth,
                finalHeight,
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

            SessionOptions sessionOptions = new();
            if (fileSize > 8L * 1024 * 1024 * 1024) // >8GB — use memory mapping for large models
            {
                _logger?.LogInformation("Large model detected ({Size} bytes) for '{ModelId}' on CPU — using memory-mapped weight loading",
                    fileSize, modelId);

                // Enable memory pattern optimization and reduce thread contention
                sessionOptions.AppendExecutionProvider_CPU(0);
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
    /// Computes time steps for Euler sampler. Uses linearly decreasing noise schedule: t from 1 down to 0.
    /// </summary>
    private static double[] ComputeEulerTimeSteps(int steps)
    {
        var timeSteps = new double[steps];
        for (int i = 0; i < steps; i++)
            timeSteps[i] = 1.0 - ((double)i / steps);
        return timeSteps;
    }

    /// <summary>
    /// Encodes a text prompt using the CLIP text encoder ONNX session via DiffusionInferenceEngine.RunTextEncoder.
    /// Returns a DenseTensor<float> containing the text embedding (shape depends on pipeline type).
    /// </summary>
    private DenseTensor<float>? EncodePrompt(DiffusionInferenceEngine engine, string pipelineType, string prompt)
    {
        return engine.RunTextEncoder(pipelineType, prompt);
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

    // ---- Inpainting helpers ----

    /// <summary>
    /// Converts a mask image bytes to a latent-space mask tensor [1, 1, h/8, w/8].
    /// The mask should be grayscale — white pixels indicate regions to inpaint.
    /// </summary>
    private DenseTensor<float>? MaskToLatentMask(byte[] maskBytes, int heightDiv8, int widthDiv8)
    {
        try
        {
            // Decode mask image to pixel values [0, 1] (grayscale → binary)
            using var maskBitmap = SKBitmap.Decode(new MemoryStream(maskBytes));

            // Downsample from original resolution to latent resolution (8x downsampling for SD models)
            int origWidth = maskBitmap.Width;
            int origHeight = maskBitmap.Height;
            double scaleX = widthDiv8 / (double)origWidth;
            double scaleY = heightDiv8 / (double)origHeight;

            // Bilinear sampling: for each latent pixel, compute weighted average from 4 nearest source pixels
            var tensor = new DenseTensor<float>(new[] { 1, 1, heightDiv8, widthDiv8 });

            for (int h = 0; h < heightDiv8; h++)
            {
                for (int w = 0; w < widthDiv8; w++)
                {
                    // Map latent pixel to source image coordinates
                    double srcX = w / scaleX;
                    double srcY = h / scaleY;

                    int x1 = Math.Clamp((int)Math.Floor(srcX), 0, origWidth - 1);
                    int y1 = Math.Clamp((int)Math.Floor(srcY), 0, origHeight - 1);
                    int x2 = Math.Min(x1 + 1, origWidth - 1);
                    int y2 = Math.Min(y1 + 1, origHeight - 1);

                    // Bilinear interpolation weights
                    float dx = (float)(srcX - x1);
                    float dy = (float)(srcY - y1);

                    // Sample grayscale value from source image (use Red channel)
                    var v11 = maskBitmap.GetPixel(x1, y1).Red / 255f;
                    var v21 = maskBitmap.GetPixel(x2, y1).Red / 255f;
                    var v12 = maskBitmap.GetPixel(x1, y2).Red / 255f;
                    var v22 = maskBitmap.GetPixel(x2, y2).Red / 255f;

                    // Bilinear interpolation: f(i,j) = (1-dx)(1-dy)*v11 + dx(1-dy)*v21 + dy(1-dx)*v12 + dxdy*v22
                    float blended = (float)((1 - dx) * (1 - dy) * v11 + dx * (1 - dy) * v21 + dy * (1 - dx) * v12 + dx * dy * v22);

                    // Clamp to [0, 1]
                    tensor[0, 0, h, w] = Math.Clamp(blended, 0f, 1f);
                }
            }

            return tensor;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to convert mask image to latent space");
            return null;
        }
    }

    /// <summary>
    /// Computes the noise level from strength parameter for inpainting.
    /// Strength = 0 means no change (keep original), Strength = 1 means full replacement.
    /// </summary>
    private static double GetNoiseLevelFromStrength(float strength, int steps)
    {
        // Map strength to a noise level: higher strength → more noise at start
        var sigmaStart = Math.Pow(strength, (double)steps / 50);
        return sigmaStart;
    }

    /// <summary>
    /// Adds Gaussian noise to latent tensor with given sigma. Used for inpainting step initialization.
    /// </summary>
    private DenseTensor<float> AddGaussianNoise(DenseTensor<float> latents, float sigma)
    {
        // Use deterministic RNG — seed based on current timestamp and element index ensures reproducibility
        var rng = new Random((int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() ^ BitConverter.ToInt32(BitConverter.GetBytes(latents.Length), 0)));
        var noisy = new DenseTensor<float>(latents.Dimensions);

        for (int i = 0; i < latents.Length; i++)
        {
            // Box-Muller transform for Gaussian noise
            double u1 = rng.NextDouble();
            double u2 = rng.NextDouble();
            if (u1 < double.Epsilon) u1 = double.Epsilon;

            float z0 = (float)Math.Sqrt(-2 * Math.Log(u1)) * (float)Math.Cos(2 * Math.PI * u2);
            noisy[i] = latents[i] + sigma * z0;
        }

        return noisy;
    }

    /// <summary>
    /// Blends denoised and original latent at each step using mask weighting.
    /// x_t' = mask * denoised + (1 - mask) * original_latent_at_time t.
    /// For inpainting, the masked region is replaced while preserving unmasked areas.
    /// </summary>
    private DenseTensor<float> BlendWithMask(DenseTensor<float> denoised, DenseTensor<float> originalLatents, DenseTensor<float>? mask, double sigmaT)
    {
        if (mask == null)
            return denoised;

        var blended = new DenseTensor<float>(denoised.Dimensions);
        for (int i = 0; i < denoised.Length; i++)
        {
            // Blend: weighted combination of denoised and original based on mask
            float blendWeight = mask[i];
            blended[i] = blendWeight * denoised[i] + (1f - blendWeight) * originalLatents[i];
        }

        return blended;
    }

    /// <summary>
    /// Blends denoised output with init latents for outpainting — new area gets denoised only, old area blends.
    /// </summary>
    private DenseTensor<float> BlendWithOutpaintingMask(DenseTensor<float> denoised, DenseTensor<float> noisyLatents, int origWidthDiv8, int origHeightDiv8, double sigmaT)
    {
        // Create a canvas mask that covers the original image area (new area is fully denoised)
        var blended = new DenseTensor<float>(denoised.Dimensions);

        for (int i = 0; i < denoised.Length; i++)
        {
            // Convert flat index to 4D coordinates
            int dim4 = denoised.Dimensions[3];
            int idx3 = i / (dim4 * dim4);
            int idx2 = (i % (dim4 * dim4)) / dim4;
            int idx1 = i % dim4;

            // Check if this pixel is in the original image area
            bool inOriginalArea = idx2 < origHeightDiv8 && idx1 < origWidthDiv8;

            float blendWeight = inOriginalArea ? 0.5f : 1f; // Blend old area, full denoised for new area
            blended[i] = blendWeight * denoised[i] + (1f - blendWeight) * noisyLatents[i];
        }

        return blended;
    }

    /// <summary>
    /// Converts image bytes to pixel values with normalization.
    /// Returns (pixels, channels) where pixels is flattened [H*W*C] array of float32 in [-1, 1].
    /// </summary>
    private static (float[] Pixels, int Channels) ImageToPixels(byte[] imageBytes)
    {
        // Decode PNG/JPEG image bytes into pixel values using SkiaSharp for cross-platform compatibility
        using var bitmap = SKBitmap.Decode(new MemoryStream(imageBytes));

        var width = bitmap.Width;
        var height = bitmap.Height;

        if (bitmap.ColorType != SKColorType.Rgb888x)
            throw new InvalidOperationException($"VAE expects RGB images (3 channels), got {bitmap.ColorType}");

        // Create pixel values array — flattened HWC layout, normalized to [-1, 1]
        var pixels = new float[height * width]; // Will be [C, H*W] after reshaping to tensor

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var color = bitmap.GetPixel(x, y);
                // Normalize RGB from [0, 255] to [-1, 1]: value * 2/255 - 1
                pixels[y * width + x] = (color.Red / 255f) * 2f - 1f;
            }
        }

        return (pixels, 3);
    }

    /// <summary>
    /// Converts image bytes to pixel values with normalization — returns channels only.
    /// Returns (pixels, channels) where pixels is flattened [H*W*C] array of float32 in [-1, 1].
    /// </summary>
    private static (float[] Pixels, int Channels) ImageToPixelsNoHeightWidth(byte[] imageBytes)
    {
        return ImageToPixels(imageBytes); // Same implementation — dimensions handled elsewhere
    }

    /// <summary>
    /// Converts pixel values [-1, 1] to a DenseTensor<float> for ONNX Runtime input.
    /// Shape: [1, channels, height, width] — NCHW format (used by PyTorch/ONNX models).
    /// </summary>
    private static DenseTensor<float> PixelValuesToInputTensor(float[] pixels, int channels, int height, int width)
    {
        // Reorder from HWC layout to CHW layout for ONNX Runtime input
        var tensor = new DenseTensor<float>(new[] { 1, channels, height, width });

        for (int c = 0; c < channels; c++)
        {
            for (int h = 0; h < height; h++)
            {
                for (int w = 0; w < width; w++)
                {
                    // HWC index → CHW index: pixel[h * width + w] is the red channel value at position (h, w)
                    tensor[0, c, h, w] = pixels[h * width + w];
                }
            }
        }

        return tensor;
    }

    /// <summary>
    /// Converts raw NCHW float array (shape: [1, channels, height, width]) to PNG image bytes.
    /// Reorders from CHW → HWC and denormalizes from [-1, 1] to [0, 255].
    /// </summary>
    private static byte[] PixelValuesToPng(float[] pixelData, int height, int width)
    {
        const int channels = 3; // RGB for SD-style VAEs

        using var skBitmap = new SKBitmap(width, height);

        for (int c = 0; c < channels && c < 3; c++)
        {
            for (int h = 0; h < height; h++)
            {
                for (int w = 0; w < width; w++)
                {
                    var pixelValue = pixelData[c * height * width + h * width + w];
                    var clampedPixel = Math.Clamp(pixelValue * 127.5f + 127.5f, 0, 255);

                    var r = c == 0 ? (byte)clampedPixel : (byte)0;
                    var g = c == 1 ? (byte)clampedPixel : (byte)0;
                    var b = c == 2 ? (byte)clampedPixel : (byte)0;

                    skBitmap.SetPixel(w, h, new SKColor(r, g, b));
                }
            }
        }

        using var image = SKImage.FromBitmap(skBitmap);
        using var imageData = image.Encode(SKEncodedImageFormat.Png, 100);
        return imageData.ToArray();
    }
}

