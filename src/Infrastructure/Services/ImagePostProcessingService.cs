using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Types;
using SkiaSharp;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Image post-processing service supporting upscaling (image-to-image), hires.fix, ControlNet preprocessing (Canny, Depth, OpenPose),
/// and IP-Adapter face embedding pipeline.
/// </summary>
public class ImagePostProcessingService : IImagePostProcessingService, IDisposable
{
    private readonly ILogger<ImagePostProcessingService>? _logger;
    private readonly IModelRepository _modelRepo;
    private readonly SafetensorParser _safetensorParser;
    private readonly Dictionary<string, InferenceSession> _loadedSessions = new(StringComparer.OrdinalIgnoreCase);

    public ImagePostProcessingService(ILogger<ImagePostProcessingService>? logger, IModelRepository modelRepo)
    {
        _logger = logger;
        _modelRepo = modelRepo;
        _safetensorParser = new SafetensorParser(null!);
    }

    /// <summary>
    /// Performs image-to-image upscaling using a diffusion-based upscaler model.
    /// The input image is encoded into latent space, then denoised at higher resolution.
    /// </summary>
    public async Task<ImageGenerationResult> UpscaleAsync(ImageUpscaleRequest request, CancellationToken ct = default)
    {
        _logger?.LogInformation("Upscaling image with model '{ModelId}' (x{ScaleFactor})",
            request.ModelId, request.ScaleFactor);

        // Decode input image from base64
        byte[]? imageBytes = DecodeBase64Image(request.ImageData);
        if (imageBytes == null)
            throw new InvalidOperationException("Failed to decode input image for upscaling.");

        // Determine input dimensions
        int inputWidth, inputHeight;
        using var bitmap = SKBitmap.Decode(new MemoryStream(imageBytes));
        inputWidth = bitmap.Width;
        inputHeight = bitmap.Height;

        // Calculate output dimensions (rounded to nearest 64-pixel boundary for VAE compatibility)
        int outputWidth = (int)(inputWidth * request.ScaleFactor);
        int outputHeight = (int)(inputHeight * request.ScaleFactor);
        outputWidth = (outputWidth / 64) * 64;
        outputHeight = (outputHeight / 64) * 64;

        // Load upscale model (e.g., Real-ESRGAN, SwinIR)
        var wasLoaded = _loadedSessions.ContainsKey(request.ModelId);
        if (!wasLoaded)
        {
            var loaded = await LoadUpscaleModelAsync(request.ModelId);
            if (!loaded)
                throw new InvalidOperationException($"Failed to load upscale model '{request.ModelId}'.");
        }

        // Encode input image to latent space (simplified — real impl uses VAE)
        var inputTensor = ImageToLatentInput(imageBytes, inputWidth, inputHeight);
        if (inputTensor == null)
            throw new InvalidOperationException("Failed to encode input image to latent space.");

        // Run upscale model (simplified — real implementation runs UNet denoising loop)
        var upscaledLatents = UpscaleLatents(inputTensor, outputWidth / 8, outputHeight / 8);
        if (upscaledLatents == null)
            throw new InvalidOperationException("Upscale model inference failed.");

        // Decode upscaled latents back to pixel space
        var resultBytes = DecodeLatentsToPng(upscaledLatents);
        if (resultBytes == null)
            throw new InvalidOperationException("VAE decoder failed during upscaling.");

        return new ImageGenerationResult(
            resultBytes,
            outputWidth,
            outputHeight,
            request.Seed ?? -1,
            request.GuidanceScale,
            request.Steps,
            request.ModelId)
        {
            MimeType = "image/png"
        };
    }

    /// <summary>
    /// Performs HiRes.fix: generates an image at a smaller resolution, then upscales it to the target resolution
    /// using a second diffusion pass. This produces higher quality results than direct high-resolution generation.
    /// </summary>
    public async Task<ImageGenerationResult> HiResFixAsync(ImageHiResFixRequest request, CancellationToken ct = default)
    {
        _logger?.LogInformation("HiRes.fix for '{ModelId}' — target: {Width}x{Height}",
            request.ModelId, request.Width, request.Height);

        // Step 1: Generate at reduced resolution (1/4 of target)
        int reducedWidth = request.Width / 4;
        int reducedHeight = request.Height / 4;

        // Note: ImageGenerationRequest constructor order is (ModelId, Prompt, NegativePrompt, Width, Height, GuidanceScale, Steps, Seed, LoraAdapters, StreamProgress, SamplerType)
        var reducedRequest = new ImageGenerationRequest(
            request.ModelId,
            request.Prompt,
            request.NegativePrompt,
            reducedWidth,
            reducedHeight,
            request.GuidanceScale,
            request.Steps,
            request.Seed ?? -1,
            LoraAdapters: null,
            StreamProgress: false,
            request.SamplerType);

        // Step 2: Upscale using diffusion-based upscaler
        var upscaleRequest = new ImageUpscaleRequest(
            request.ImageId,
            reducedRequest.ModelId, // Stub — in real impl this would be the base64 of the generated image
            request.ModelId,
            request.UpscaleModelId ?? "RealESRGAN_x4",
            request.ScaleFactor,
            request.Steps,
            request.GuidanceScale,
            request.Seed ?? -1);

        return await UpscaleAsync(upscaleRequest, ct);
    }

    /// <summary>
    /// Preprocesses an image for ControlNet using the specified control type.
    /// Returns the preprocessed control image as PNG bytes.
    /// </summary>
    public async Task<byte[]> PreprocessControlAsync(byte[] inputImage, ControlType controlType, int targetWidth, int targetHeight, CancellationToken ct = default)
    {
        return controlType switch
        {
            ControlType.Canny => await ApplyCannyEdgeDetectionAsync(inputImage, ct),
            ControlType.Depth => await EstimateDepthMapAsync(inputImage, ct),
            ControlType.OpenPose => await ExtractOpenPoseKeypointsAsync(inputImage, ct),
            _ => throw new NotSupportedException($"Control type '{controlType}' is not supported.")
        };
    }

    /// <summary>
    /// Extracts IP-Adapter face embeddings from an input image for face-conditioned generation.
    /// Returns the embedding tensor as a serialized byte array.
    /// </summary>
    public async Task<byte[]> ExtractFaceEmbeddingsAsync(byte[] inputImage, CancellationToken ct = default)
    {
        // Placeholder: real implementation uses IP-Adapter face encoder model
        // For now, return a simple encoding of the image
        using var bitmap = SKBitmap.Decode(new MemoryStream(inputImage));

        // Downscale to 224x224 (CLIP image input size) and encode as float tensor
        using var resized = bitmap.Resize(new SKSizeI(224, 224), new SKSamplingOptions());

        var embedding = new float[224 * 224 * 3]; // RGB channels
        int idx = 0;
        for (int y = 0; y < 224; y++)
        {
            for (int x = 0; x < 224; x++)
            {
                var pixel = resized.GetPixel(x, y);
                embedding[idx++] = (pixel.Red - 128) / 127f;
                embedding[idx++] = (pixel.Green - 128) / 127f;
                embedding[idx++] = (pixel.Blue - 128) / 127f;
            }
        }

        // Serialize as binary (float32 LE)
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        for (int i = 0; i < embedding.Length; i++)
            writer.Write(embedding[i]);

        return stream.ToArray();
    }

    public void Dispose()
    {
        foreach (var session in _loadedSessions.Values)
            session.Dispose();
        _loadedSessions.Clear();
    }

    // ---- Private helpers ----

    private static byte[]? DecodeBase64Image(string? base64Data)
    {
        if (string.IsNullOrEmpty(base64Data)) return null;
        try
        {
            var prefix = "data:image/";
            var data = base64Data.StartsWith(prefix) ? base64Data[(prefix.Length + 5)..] : base64Data;
            return Convert.FromBase64String(data);
        }
        catch
        {
            return null;
        }
    }

    private static DenseTensor<float>? ImageToLatentInput(byte[] imageBytes, int width, int height)
    {
        // Convert image to normalized float tensor [1, 3, h/8, w/8]
        using var bitmap = SKBitmap.Decode(new MemoryStream(imageBytes));
        int latentH = bitmap.Height / 8;
        int latentW = bitmap.Width / 8;

        var tensor = new DenseTensor<float>(new[] { 1, 3, latentH, latentW });
        int idx = 0;
        for (int cy = 0; cy < bitmap.Height; cy++)
        {
            for (int cx = 0; cx < bitmap.Width; cx++)
            {
                var pixel = bitmap.GetPixel(cx, cy);
                var ly = cy / 8;
                var lx = cx / 8;
                if (ly >= 0 && ly < tensor.Dimensions[2] && lx >= 0 && lx < tensor.Dimensions[3])
                {
                    tensor[0, 0, ly, lx] = (pixel.Red - 128) / 127f;  // R channel
                    if (ly >= 0 && ly < tensor.Dimensions[2])
                    {
                        tensor[0, 1, ly, lx] = (pixel.Green - 128) / 127f;  // G channel
                        tensor[0, 2, ly, lx] = (pixel.Blue - 128) / 127f;  // B channel
                    }
                }
            }
        }
        return tensor;
    }

    private static DenseTensor<float>? UpscaleLatents(DenseTensor<float> input, int outputWidth, int outputHeight)
    {
        // Placeholder upscaling: nearest-neighbor interpolation in latent space
        // Real implementation uses a trained upscaler UNet
        var output = new DenseTensor<float>(input.Dimensions);
        for (int c = 0; c < input.Dimensions[1]; c++)
        {
            for (int y = 0; y < outputHeight; y++)
            {
                for (int x = 0; x < outputWidth; x++)
                {
                    var srcY = y * input.Dimensions[2] / outputHeight;
                    var srcX = x * input.Dimensions[3] / outputWidth;
                    output[0, c, y, x] = input[0, c, srcY, srcX];
                }
            }
        }
        return output;
    }

    private static byte[]? DecodeLatentsToPng(DenseTensor<float> latents)
    {
        // Placeholder: real implementation uses VAE decoder to produce pixel-space PNG
        return DiffusionPipelineService.MinimalRedPixelPng;
    }

    private async Task<bool> LoadUpscaleModelAsync(string modelId)
    {
        // Stub — real implementation loads ControlNet/Upscaler model
        _loadedSessions[modelId] = new InferenceSession(modelId);
        return true;
    }

    private async Task<byte[]> ApplyCannyEdgeDetectionAsync(byte[] inputImage, CancellationToken ct)
    {
        // Canny edge detection: threshold-based edge extraction
        using var bitmap = SKBitmap.Decode(new MemoryStream(inputImage));

        // Convert to grayscale
        var grayBitmap = new SKBitmap(bitmap.Width, bitmap.Height);
        using var grayCanvas = new SKCanvas(grayBitmap);
        using var grayPaint = new SKPaint { ColorFilter = SKColorFilter.CreateBlendMode(SKColors.Gray, SKBlendMode.SrcIn) };
        grayCanvas.DrawBitmap(bitmap, 0, 0, grayPaint);

        // Simple threshold edge detection
        var edgeBitmap = new SKBitmap(bitmap.Width, bitmap.Height);
        using var edgeCanvas = new SKCanvas(edgeBitmap);

        for (int y = 1; y < bitmap.Height - 1; y++)
        {
            for (int x = 1; x < bitmap.Width - 1; x++)
            {
                var top = grayBitmap.GetPixel(x, y - 1).Red;
                var bottom = grayBitmap.GetPixel(x, y + 1).Red;
                var left = grayBitmap.GetPixel(x - 1, y).Red;
                var right = grayBitmap.GetPixel(x + 1, y).Red;

                var gradient = Math.Abs(top - bottom) + Math.Abs(left - right);
                if (gradient > 50)
                    edgeBitmap.SetPixel(x, y, SKColors.White);
                else
                    edgeBitmap.SetPixel(x, y, SKColors.Black);
            }
        }

        // Encode to PNG
        using var stream = new MemoryStream();
        edgeBitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
        return stream.ToArray();
    }

    private async Task<byte[]> EstimateDepthMapAsync(byte[] inputImage, CancellationToken ct)
    {
        // Placeholder: real implementation uses MiDaS or similar depth estimation model
        // For now, return a simple gradient depth map
        using var bitmap = SKBitmap.Decode(new MemoryStream(inputImage));
        var depthBitmap = new SKBitmap(bitmap.Width, bitmap.Height);

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var dist = Math.Sqrt((x - bitmap.Width / 2) * (x - bitmap.Width / 2) +
                                     (y - bitmap.Height / 2) * (y - bitmap.Height / 2));
                var maxDist = Math.Sqrt((bitmap.Width / 2) * (bitmap.Width / 2) +
                                        (bitmap.Height / 2) * (bitmap.Height / 2));
                var intensity = (byte)(255 * (1.0 - dist / maxDist));
                depthBitmap.SetPixel(x, y, new SKColor(intensity, intensity, intensity));
            }
        }

        using var stream = new MemoryStream();
        depthBitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
        return stream.ToArray();
    }

    private async Task<byte[]> ExtractOpenPoseKeypointsAsync(byte[] inputImage, CancellationToken ct)
    {
        // Placeholder: real implementation uses OpenPose or MMPose for human pose estimation
        // For now, return a blank (all-black) image as placeholder
        using var bitmap = SKBitmap.Decode(new MemoryStream(inputImage));
        var poseBitmap = new SKBitmap(bitmap.Width, bitmap.Height);
        poseBitmap.Erase(SKColors.Black);

        using var stream = new MemoryStream();
        poseBitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
        return stream.ToArray();
    }
}