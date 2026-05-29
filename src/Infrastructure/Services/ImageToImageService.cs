using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Types;
using OpenLMStudio.Domain.Models;
using SkiaSharp;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Image-to-image service: encodes an input image via VAE, applies noise, and runs denoising.
/// </summary>
public class ImageToImageService : IImageToImageService
{
    private readonly ILogger<ImageToImageService>? _logger;
    private readonly IModelRepository _modelRepo;
    private readonly IVAEPipelineService _vaeService;
    private readonly DiffusionPipelineService _pipeline;
    private readonly ILoraAdapterManager? _loraManager;

    public ImageToImageService(
        ILogger<ImageToImageService>? logger,
        IModelRepository modelRepo,
        IVAEPipelineService vaeService,
        DiffusionPipelineService pipeline,
        ILoraAdapterManager? loraManager = null)
    {
        _logger = logger;
        _modelRepo = modelRepo;
        _vaeService = vaeService;
        _pipeline = pipeline;
        _loraManager = loraManager;
    }

    public async Task<ImageToImageResult> EncodeAndDenoiseAsync(ImageToImageRequest request, CancellationToken ct = default)
    {
        var modelMeta = await _modelRepo.GetMultiModalModelByIdAsync(request.ModelId);
        if (modelMeta == null)
            throw new InvalidOperationException($"Image generation model '{request.ModelId}' not found.");

        var pipelineType = GetPipelineType(modelMeta);
        var engine = new DiffusionInferenceEngine(_logger == null ? null : (ILogger<DiffusionInferenceEngine>?)_logger);

        var weightFile = GetPrimaryWeightFile(modelMeta);
        if (string.IsNullOrEmpty(weightFile) || !File.Exists(weightFile))
            throw new FileNotFoundException($"Weight file not found for model '{request.ModelId}'.");

        engine.LoadTextEncoder(pipelineType, weightFile);
        engine.LoadUnet(pipelineType, weightFile);
        engine.LoadVaeDecoder(pipelineType, weightFile);

        // Step 1: Encode input image to latent space
        var latents = await EncodeImageToLatents(request.InputImage, pipelineType, modelMeta);
        if (latents == null)
            throw new InvalidOperationException("VAE encoding failed for input image.");

        // Step 2: Add noise based on denoise strength
        var noisyLatents = AddNoiseToLatents(latents, request.DenoiseStrength, (int)(request.Seed & int.MaxValue));

        // Step 3: Encode prompt
        var textEmbedding = EncodePrompt(engine, pipelineType, request.Prompt);
        if (request.NegativePrompt != null)
            textEmbedding = BlendCfgConditioning(engine, pipelineType, request.Prompt, request.NegativePrompt, request.GuidanceScale);
        else
        {
            var unconditioned = EncodePrompt(engine, pipelineType, string.Empty);
            if (textEmbedding != null && unconditioned != null)
                textEmbedding = BlendTensors(textEmbedding, unconditioned, request.GuidanceScale);
        }

        // Step 4: Denoising loop
        var timeSteps = request.SamplerType switch
        {
            ImageSamplerType.Euler => ComputeEulerTimeSteps(request.Steps),
            ImageSamplerType.EulerA => ComputeEulerATimeSteps(request.Steps),
            ImageSamplerType.DPMS => ComputeDPMTimesteps(request.Steps),
            ImageSamplerType.LMS => ComputeLMSFixedTimeSteps(request.Steps),
            _ => ComputeEulerTimeSteps(request.Steps),
        };

        var effectiveTimeSteps = timeSteps ?? Array.Empty<double>();
        var lastStepTime = effectiveTimeSteps.Length > 0 ? effectiveTimeSteps[effectiveTimeSteps.Length - 1] : 1.0;

        var currentLatents = noisyLatents;
        for (int stepIndex = 0; stepIndex < request.Steps && !ct.IsCancellationRequested; stepIndex++)
        {
            int clamped = Math.Min(stepIndex, effectiveTimeSteps.Length - 1);
            double t = clamped >= 0 ? effectiveTimeSteps[clamped] : 1.0;

            var denoised = engine.RunUnetDenoise(pipelineType, currentLatents, textEmbedding!, request.GuidanceScale, stepIndex, request.Steps);
            if (denoised == null)
                throw new InvalidOperationException("UNet denoising failed during image-to-image.");
            currentLatents = denoised;

            if (request.SamplerType == ImageSamplerType.EulerA && stepIndex > 0 && effectiveTimeSteps.Length > 0)
            {
                var sigmaT = GetSigmaFromTime(t, lastStepTime);
                currentLatents = SubtractNoiseFromLatents(denoised, sigmaT, (int)(request.Seed ^ stepIndex));
            }

            await Task.Delay(100, ct);
        }

        // Step 5: Decode latents to PNG
        var pngBytes = engine.DecodeLatents(pipelineType, currentLatents);
        if (pngBytes == null)
            throw new InvalidOperationException("VAE decoder failed during image-to-image.");

        engine.Dispose();

        return new ImageToImageResult(
            pngBytes,
            request.Width,
            request.Height,
            request.Seed,
            request.GuidanceScale,
            request.Steps,
            request.ModelId);
    }

    public async Task<ImageToImageResult> ImageVariationAsync(ImageVariationRequest request, CancellationToken ct = default)
    {
        var variationRequest = new ImageToImageRequest(
            ModelId: request.ModelId,
            Prompt: "variation",
            InputImage: request.InputImage,
            DenoiseStrength: request.DenoiseStrength,
            Width: request.Width,
            Height: request.Height,
            GuidanceScale: request.GuidanceScale,
            Steps: request.Steps,
            Seed: request.Seed,
            SamplerType: request.SamplerType);

        return await EncodeAndDenoiseAsync(variationRequest, ct);
    }

    public async Task<ImageToImageResult> InpaintAsync(InpaintRequest request, CancellationToken ct = default)
    {
        var result = await _pipeline.GenerateInpaintingAsync(new ImageInpaintingRequest
        {
            ModelId = request.ModelId,
            Prompt = request.Prompt,
            NegativePrompt = request.NegativePrompt,
            InitImage = $"data:image/png;base64,{Convert.ToBase64String(request.InitImage)}",
            MaskImage = $"data:image/png;base64,{Convert.ToBase64String(request.MaskImage)}",
            Width = request.Width,
            Height = request.Height,
            CfgScale = (float)request.GuidanceScale,
            Steps = request.Steps,
            Seed = request.Seed,
        }, ct);
        return new ImageToImageResult(
            result.ImageBytes,
            result.Width,
            result.Height,
            result.Seed,
            result.GuidanceScale,
            result.Steps,
            result.ModelId);
    }

    public async Task<ImageToImageResult> OutpaintAsync(OutpaintRequest request, CancellationToken ct = default)
    {
        var result = await _pipeline.GenerateOutpaintingAsync(new ImageOutpaintingRequest
        {
            ModelId = request.ModelId,
            Prompt = request.Prompt,
            NegativePrompt = request.NegativePrompt,
            InitImage = $"data:image/png;base64,{Convert.ToBase64String(request.InitImage)}",
            Direction = request.Direction,
            Width = request.Width,
            Height = request.Height,
            CfgScale = (float)request.GuidanceScale,
            Steps = request.Steps,
            Seed = request.Seed,
        }, ct);
        return new ImageToImageResult(
            result.ImageBytes,
            result.Width,
            result.Height,
            result.Seed,
            result.GuidanceScale,
            result.Steps,
            result.ModelId);
    }

    public void Dispose() { }

    private static string GetPipelineType(MultiModalModelMetadata metadata)
    {
        if (metadata.Id != null && metadata.Id.IndexOf("sdxl", StringComparison.OrdinalIgnoreCase) >= 0)
            return "sdxl";
        if (metadata.Id != null && metadata.Id.IndexOf("flux", StringComparison.OrdinalIgnoreCase) >= 0)
            return "flux";
        return "sd15";
    }

    private static string? GetPrimaryWeightFile(MultiModalModelMetadata metadata)
    {
        if (!string.IsNullOrEmpty(metadata.FilePath) && metadata.FilePath.EndsWith(".safetensors", StringComparison.OrdinalIgnoreCase))
            return metadata.FilePath;
        if (metadata.ShardedFiles != null && metadata.ShardedFiles.Any())
            return metadata.ShardedFiles.First();
        return null;
    }

    private async Task<DenseTensor<float>?> EncodeImageToLatents(byte[] imageBytes, string pipelineType, MultiModalModelMetadata modelMeta)
    {
        using var bitmap = SKBitmap.Decode(new MemoryStream(imageBytes));
        var width = bitmap.Width;
        var height = bitmap.Height;

        // Encode to [-1, 1] pixel values
        var pixels = new float[height * width];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var color = bitmap.GetPixel(x, y);
                pixels[y * width + x] = (color.Red / 255f) * 2f - 1f;
            }
        }

        // Use VAE encoder
        var vaeBytes = await _vaeService.EncodeAsync(modelMeta.Id, imageBytes);
        if (vaeBytes == null || vaeBytes.Length == 0)
            return null;

        // Parse the VAE-encoded latents (simplified: return as DenseTensor)
        var tensor = new DenseTensor<float>(new[] { 1, 4, height / 8, width / 8 });
        for (int i = 0; i < tensor.Length && i < vaeBytes.Length / 4; i++)
            tensor[i] = BitConverter.ToSingle(vaeBytes, i * 4);

        return tensor;
    }

    private DenseTensor<float> AddNoiseToLatents(DenseTensor<float> latents, double denoiseStrength, int seed)
    {
        var rng = new Random(seed);
        var noisy = new DenseTensor<float>(latents.Dimensions);
        for (int i = 0; i < latents.Length; i++)
            noisy[i] = (float)(latents[i] * (1 - denoiseStrength) + (rng.NextDouble() * 2 - 1) * denoiseStrength);
        return noisy;
    }

    private DenseTensor<float>? EncodePrompt(DiffusionInferenceEngine engine, string pipelineType, string prompt)
        => engine.RunTextEncoder(pipelineType, prompt);

    private DenseTensor<float>? BlendCfgConditioning(DiffusionInferenceEngine engine, string pipelineType, string positive, string negative, double cfg)
    {
        var cond = EncodePrompt(engine, pipelineType, positive);
        var uncond = EncodePrompt(engine, pipelineType, negative);
        return cond != null && uncond != null ? BlendTensors(cond, uncond, cfg) : null;
    }

    private static DenseTensor<float> BlendTensors(DenseTensor<float> a, DenseTensor<float> b, double scale)
    {
        var result = new DenseTensor<float>(a.Dimensions);
        for (int i = 0; i < a.Length; i++)
            result[i] = (float)(b[i] + scale * (a[i] - b[i]));
        return result;
    }

    private static double GetSigmaFromTime(double t, double lastT) => Math.Sqrt(1 - t / lastT);

    private static DenseTensor<float> SubtractNoiseFromLatents(DenseTensor<float> denoised, double sigma, int seed)
    {
        var rng = new Random(seed);
        var clean = new DenseTensor<float>(denoised.Dimensions);
        for (int i = 0; i < denoised.Length; i++)
            clean[i] = denoised[i] - (float)(rng.NextDouble() * 2 - 1) * (float)sigma;
        return clean;
    }

    private static double[] ComputeEulerTimeSteps(int steps)
    {
        var ts = new double[steps];
        for (int i = 0; i < steps; i++) ts[i] = 1.0 - ((double)i / steps);
        return ts;
    }

    private static double[] ComputeEulerATimeSteps(int steps) => ComputeEulerTimeSteps(steps);

    private static double[] ComputeDPMTimesteps(int steps)
    {
        var ts = new double[steps];
        for (int i = 0; i < steps; i++) ts[i] = Math.Exp(-((double)i / steps) * Math.Log(1000));
        return ts;
    }

    private static double[] ComputeLMSFixedTimeSteps(int steps)
    {
        var ts = new double[steps];
        for (int i = 0; i < steps; i++) ts[i] = 14.6186328 * Math.Exp(-(i / ((double)(steps - 1))));
        return ts;
    }
}