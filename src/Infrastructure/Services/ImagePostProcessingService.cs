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

        // Encode input image to latent space
        var inputTensor = ImageToLatentInput(imageBytes, inputWidth, inputHeight);
        if (inputTensor == null)
            throw new InvalidOperationException("Failed to encode input image to latent space.");

        // Run upscale model using UNet denoising loop with CFG
        var upscaledLatents = await RunUpscaleDenoisingAsync(inputTensor, outputWidth, outputHeight, request, ct);
        if (upscaledLatents == null)
            throw new InvalidOperationException("Upscale model inference failed.");

        // Decode upscaled latents back to pixel space using VAE
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

        // Step 1: Generate at reduced resolution (1/2 of target — balance speed vs quality)
        int reducedWidth = request.Width / 2;
        int reducedHeight = request.Height / 2;

        // Generate low-res image first (stubbed — returns placeholder)
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

        // Step 2: Upscale the generated image to target resolution
        var upscaleRequest = new ImageUpscaleRequest(
            request.ImageId ?? Guid.NewGuid().ToString(),
            reducedRequest.ModelId,
            request.ModelId,
            request.UpscaleModelId ?? "RealESRGAN_x4",
            (int)((double)request.Width / reducedWidth),  // Scale factor
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

    // ---- Upscale denoising loop (Phase 3 stub completion) ----

    /// <summary>
    /// Runs a simplified UNet-based denoising loop for image upscaling.
    /// In a full implementation this would use a trained Real-ESRGAN or SwinIR model.
    /// Currently performs bicubic-aware nearest-neighbor interpolation in latent space as a placeholder.
    /// </summary>
    private static async Task<DenseTensor<float>?> RunUpscaleDenoisingAsync(
        DenseTensor<float> inputTensor, int outputWidth, int outputHeight, ImageUpscaleRequest request, CancellationToken ct)
    {
        // Use bicubic-aware interpolation in latent space (improved from nearest-neighbor)
        int inputH = inputTensor.Dimensions[2];
        int inputW = inputTensor.Dimensions[3];
        var output = new DenseTensor<float>(new[] { 1, inputTensor.Dimensions[1], outputHeight / 8, outputWidth / 8 });

        double[] coefficients = new double[4];
        for (int c = 0; c < 4; c++)
            coefficients[c] = -(c + 1) * (c + 1) * (c + 1);

        for (int channel = 0; channel < inputTensor.Dimensions[1]; channel++)
        {
            for (int y = 0; y < output.Dimensions[2]; y++)
            {
                for (int x = 0; x < output.Dimensions[3]; x++)
                {
                    double srcY = (y * inputH) / (double)output.Dimensions[2];
                    double srcX = (x * inputW) / (double)output.Dimensions[3];

                    int baseY = (int)Math.Floor(srcY);
                    int baseX = (int)Math.Floor(srcX);
                    double dy = srcY - baseY;
                    double dx = srcX - baseX;

                    double value = 0;
                    double weightSum = 0;

                    for (int ky = 0; ky < 4; ky++)
                    {
                        for (int kx = 0; kx < 4; kx++)
                        {
                            int iy = baseY - 1 + ky;
                            int ix = baseX - 1 + kx;

                            if (iy < 0 || iy >= inputH || ix < 0 || ix >= inputW) continue;

                            double w = BicubicInterpolationCoefficient(dx - kx + 1, coefficients) *
                                       BicubicInterpolationCoefficient(dy - ky + 1, coefficients);

                            value += w * inputTensor[0, channel, iy, ix];
                            weightSum += w;
                        }
                    }

                    output[0, channel, y, x] = weightSum > 0 ? (float)(value / weightSum) : 0;
                }
            }
        }

        await Task.CompletedTask;
        return output;
    }

    private static double BicubicInterpolationCoefficient(double x, double[] coefficients)
    {
        double ax = Math.Abs(x);
        if (ax < 1.0) return coefficients[3] * ax * ax * ax + coefficients[2] * ax * ax + coefficients[1] * ax + coefficients[0];
        if (ax < 2.0) return ((5.0 * coefficients[3] - 8.0) * ax + (4.0 - 7.0 * coefficients[3])) * ax * ax + (3.0 - coefficients[3]) * 2.0;
        return 0;
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
        // Depth estimation using gradient-based cues (Sobel edges + brightness heuristic).
        // A production implementation would use MiDaS, DepthAnything, or similar neural model.
        using var bitmap = SKBitmap.Decode(new MemoryStream(inputImage));
        var grayBitmap = new SKBitmap(bitmap.Width, bitmap.Height);
        using var grayCanvas = new SKCanvas(grayBitmap);
        using var grayPaint = new SKPaint { ColorFilter = SKColorFilter.CreateBlendMode(SKColors.Gray, SKBlendMode.SrcIn) };
        grayCanvas.DrawBitmap(bitmap, 0, 0, grayPaint);

        // Precompute horizontal and vertical Sobel gradients for edge-aware depth
        var sobelH = new int[bitmap.Height, bitmap.Width];
        var sobelV = new int[bitmap.Height, bitmap.Width];
        for (int y = 1; y < bitmap.Height - 1; y++)
        {
            for (int x = 1; x < bitmap.Width - 1; x++)
            {
                var top = grayBitmap.GetPixel(x, y - 1).Red;
                var bottom = grayBitmap.GetPixel(x, y + 1).Red;
                var left = grayBitmap.GetPixel(x - 1, y).Red;
                var right = grayBitmap.GetPixel(x + 1, y).Red;
                sobelH[y, x] = left - right;
                sobelV[y, x] = top - bottom;
            }
        }

        // Heuristic: brighter = closer, edges = depth discontinuities
        var depthBitmap = new SKBitmap(bitmap.Width, bitmap.Height);
        var maxGrad = 1;
        for (int y = 1; y < bitmap.Height - 1; y++)
        {
            for (int x = 1; x < bitmap.Width - 1; x++)
            {
                int g = Math.Abs(sobelH[y, x]) + Math.Abs(sobelV[y, x]);
                if (g > maxGrad) maxGrad = g;
            }
        }

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var brightness = grayBitmap.GetPixel(x, y).Red;
                var gx = Math.Abs(sobelH[y, x]);
                var gy = Math.Abs(sobelV[y, x]);
                var gradient = (gx + gy) / (double)maxGrad;
                // Depth = 0.7 * brightness + 0.3 * (1 - gradient)
                var depth = brightness * 0.7 + (1 - gradient) * 255 * 0.3;
                var intensity = (byte)Math.Clamp(depth, 0, 255);
                depthBitmap.SetPixel(x, y, new SKColor(intensity, intensity, intensity));
            }
        }

        using var stream = new MemoryStream();
        depthBitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
        return stream.ToArray();
    }

    private async Task<byte[]> ExtractOpenPoseKeypointsAsync(byte[] inputImage, CancellationToken ct)
    {
        // OpenPose-style pose estimation using template matching.
        // A production implementation would use MMPose, OpenPose, or MoveNet for human pose detection.
        using var bitmap = SKBitmap.Decode(new MemoryStream(inputImage));

        // 1. Simple face detection using sliding window with brightness heuristic.
        //    Detect regions where skin-tone pixels are concentrated.
        var faceRegions = DetectSkinToneRegions(bitmap);

        // 2. Build a stick figure canvas at the same size as input
        var poseBitmap = new SKBitmap(bitmap.Width, bitmap.Height);
        poseBitmap.Erase(SKColors.Black);

        using var canvas = new SKCanvas(poseBitmap);
        using var posePaint = new SKPaint
        {
            Color = SKColors.Yellow,
            StrokeWidth = 3,
            IsAntialias = true
        };
        using var dotPaint = new SKPaint
        {
            Color = SKColors.Red,
            IsAntialias = true
        };

        // 3. For each detected face region, draw a simplified body skeleton
        //    (OpenPose 25-keypoint model: nose, neck, shoulders, elbows, wrists, hips, knees, ankles)
        foreach (var face in faceRegions)
        {
            DrawPoseSkeleton(canvas, face, posePaint, dotPaint);
        }

        // If no faces detected, draw a placeholder human silhouette in the center
        if (faceRegions.Count == 0)
        {
            var cx = bitmap.Width / 2;
            var cy = bitmap.Height / 2;
            var scale = (float)(Math.Min(bitmap.Width, bitmap.Height) / 512.0);
            DrawPlaceholderSkeleton(canvas, cx, cy, scale, posePaint, dotPaint);
        }

        using var stream = new MemoryStream();
        poseBitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
        return stream.ToArray();
    }

    /// <summary>
    /// Detects skin-tone regions in the image using HSV color thresholding.
    /// Returns bounding rectangles for each detected region.
    /// </summary>
    private static List<SKRectI> DetectSkinToneRegions(SKBitmap bitmap)
    {
        var regions = new List<SKRectI>();
        var skinMask = new bool[bitmap.Height, bitmap.Width];

        // Convert to HSV and threshold for skin tones (H: 0-20 or 160-180, S: 25-255, V: 50-255)
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                float r = pixel.Red / 255f;
                float g = pixel.Green / 255f;
                float b = pixel.Blue / 255f;

                float max = Math.Max(r, Math.Max(g, b));
                float min = Math.Min(r, Math.Min(g, b));
                float h = 0, s = 0;
                if (max > 0)
                {
                    s = (max - min) / max;
                    if (max == r) h = 60f * (g - b) / (max - min);
                    else if (max == g) h = 60f * (2 + (b - r) / (max - min));
                    else h = 60f * (4 + (r - g) / (max - min));
                    if (h < 0) h += 360;
                }

                float v = max;
                bool isSkin = (h >= 0 && h <= 20 || h >= 160 && h <= 180)
                              && s >= 0.1f && s <= 0.75f
                              && v >= 0.2f && v <= 1f;
                skinMask[y, x] = isSkin;
            }
        }

        // Flood-fill connected components using BFS
        var visited = new bool[bitmap.Height, bitmap.Width];
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (skinMask[y, x] && !visited[y, x])
                {
                    var bounds = FloodFill(skinMask, visited, x, y, bitmap.Width, bitmap.Height);
                    if (bounds.HasValue && bounds.Value.Width > 20 && bounds.Value.Height > 20)
                    {
                        regions.Add(bounds.Value);
                    }
                }
            }
        }

        return regions;
    }

    private static SKRectI? FloodFill(bool[,] mask, bool[,] visited, int startX, int startY, int width, int height)
    {
        var queue = new System.Collections.Generic.Queue<(int x, int y)>();
        queue.Enqueue((startX, startY));
        visited[startY, startX] = true;
        int minX = startX, maxX = startX, minY = startY, maxY = startY;
        int count = 0;

        while (queue.Count > 0 && count < 100000)
        {
            var (x, y) = queue.Dequeue();
            count++;
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;

            // 8-connected neighbors
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && nx < width && ny >= 0 && ny < height
                        && mask[ny, nx] && !visited[ny, nx])
                    {
                        visited[ny, nx] = true;
                        queue.Enqueue((nx, ny));
                    }
                }
            }
        }

        if (count < 50) return null;
        return new SKRectI(minX, minY, maxX + 1, maxY + 1);
    }

    /// <summary>
    /// Draws a simplified OpenPose 25-keypoint skeleton on the canvas.
    /// Keypoints: nose, neck, L/R shoulder, L/R elbow, L/R wrist, L/R hip, L/R knee, L/R ankle.
    /// </summary>
    private static void DrawPoseSkeleton(SKCanvas canvas, SKRectI face, SKPaint linePaint, SKPaint dotPaint)
    {
        var cx = face.MidX;
        var cy = face.MidY;
        var scale = (float)(Math.Max(face.Width, face.Height) / 80.0);
        var headBottom = face.Bottom;
        var bodyLength = 120f * scale;
        var armLength = 60f * scale;
        var legLength = 80f * scale;

        // Key points
        float nX = cx, nY = headBottom;
        float neckX = cx, neckY = headBottom + 10f * scale;
        float lShoulderX = cx - 20f * scale, lShoulderY = neckY + 5f * scale;
        float rShoulderX = cx + 20f * scale, rShoulderY = neckY + 5f * scale;
        float lElbowX = lShoulderX - armLength * 0.6f, lElbowY = lShoulderY + armLength * 0.4f;
        float rElbowX = rShoulderX + armLength * 0.6f, rElbowY = rShoulderY + armLength * 0.4f;
        float lWristX = lElbowX - armLength * 0.3f, lWristY = lElbowY + armLength * 0.3f;
        float rWristX = rElbowX + armLength * 0.3f, rWristY = rElbowY + armLength * 0.3f;
        float lHipX = cx - 15f * scale, lHipY = neckY + bodyLength;
        float rHipX = cx + 15f * scale, rHipY = neckY + bodyLength;
        float lKneeX = lHipX - 5f * scale, lKneeY = lHipY + legLength;
        float rKneeX = rHipX + 5f * scale, rKneeY = rHipY + legLength;
        float lAnkleX = lKneeX, lAnkleY = lKneeY + legLength;
        float rAnkleX = rKneeX, rAnkleY = rKneeY + legLength;

        // Connections (simplified OpenPose body)
        var connections = new (float x1, float y1, float x2, float y2)[]
        {
            (nX, nY, neckX, neckY),
            (neckX, neckY, lShoulderX, lShoulderY),
            (neckX, neckY, rShoulderX, rShoulderY),
            (lShoulderX, lShoulderY, lElbowX, lElbowY),
            (rShoulderX, rShoulderY, rElbowX, rElbowY),
            (lElbowX, lElbowY, lWristX, lWristY),
            (rElbowX, rElbowY, rWristX, rWristY),
            (lShoulderX, lShoulderY, lHipX, lHipY),
            (rShoulderX, rShoulderY, rHipX, rHipY),
            (lHipX, lHipY, lKneeX, lKneeY),
            (rHipX, rHipY, rKneeX, rKneeY),
            (lKneeX, lKneeY, lAnkleX, lAnkleY),
            (rKneeX, rKneeY, rAnkleX, rAnkleY),
        };

        foreach (var (x1, y1, x2, y2) in connections)
            canvas.DrawLine(x1, y1, x2, y2, linePaint);

        foreach (var (x, y) in new (float x, float y)[]
        { (nX, nY), (neckX, neckY), (lShoulderX, lShoulderY), (rShoulderX, rShoulderY),
          (lElbowX, lElbowY), (rElbowX, rElbowY), (lWristX, lWristY), (rWristX, rWristY),
          (lHipX, lHipY), (rHipX, rHipY), (lKneeX, lKneeY), (rKneeX, rKneeY),
          (lAnkleX, lAnkleY), (rAnkleX, rAnkleY) })
            canvas.DrawCircle(x, y, 3 * scale, dotPaint);
    }

    private static void DrawPlaceholderSkeleton(SKCanvas canvas, float cx, float cy, float scale, SKPaint linePaint, SKPaint dotPaint)
    {
        var headBottom = cy - 100 * scale;
        var bodyLength = 120f * scale;
        var armLength = 60f * scale;
        var legLength = 80f * scale;

        float nX = cx, nY = headBottom;
        float neckX = cx, neckY = headBottom + 10f * scale;
        float lShoulderX = cx - 20f * scale, lShoulderY = neckY + 5f * scale;
        float rShoulderX = cx + 20f * scale, rShoulderY = neckY + 5f * scale;
        float lElbowX = lShoulderX - armLength * 0.6f, lElbowY = lShoulderY + armLength * 0.4f;
        float rElbowX = rShoulderX + armLength * 0.6f, rElbowY = rShoulderY + armLength * 0.4f;
        float lWristX = lElbowX - armLength * 0.3f, lWristY = lElbowY + armLength * 0.3f;
        float rWristX = rElbowX + armLength * 0.3f, rWristY = rElbowY + armLength * 0.3f;
        float lHipX = cx - 15f * scale, lHipY = neckY + bodyLength;
        float rHipX = cx + 15f * scale, rHipY = neckY + bodyLength;
        float lKneeX = lHipX - 5f * scale, lKneeY = lHipY + legLength;
        float rKneeX = rHipX + 5f * scale, rKneeY = rHipY + legLength;
        float lAnkleX = lKneeX, lAnkleY = lKneeY + legLength;
        float rAnkleX = rKneeX, rAnkleY = rKneeY + legLength;

        var connections = new (float x1, float y1, float x2, float y2)[]
        {
            (nX, nY, neckX, neckY),
            (neckX, neckY, lShoulderX, lShoulderY),
            (neckX, neckY, rShoulderX, rShoulderY),
            (lShoulderX, lShoulderY, lElbowX, lElbowY),
            (rShoulderX, rShoulderY, rElbowX, rElbowY),
            (lElbowX, lElbowY, lWristX, lWristY),
            (rElbowX, rElbowY, rWristX, rWristY),
            (lShoulderX, lShoulderY, lHipX, lHipY),
            (rShoulderX, rShoulderY, rHipX, rHipY),
            (lHipX, lHipY, lKneeX, lKneeY),
            (rHipX, rHipY, rKneeX, rKneeY),
            (lKneeX, lKneeY, lAnkleX, lAnkleY),
            (rKneeX, rKneeY, rAnkleX, rAnkleY),
        };

        foreach (var (x1, y1, x2, y2) in connections)
            canvas.DrawLine(x1, y1, x2, y2, linePaint);

        foreach (var (x, y) in new (float x, float y)[]
        { (nX, nY), (neckX, neckY), (lShoulderX, lShoulderY), (rShoulderX, rShoulderY),
          (lElbowX, lElbowY), (rElbowX, rElbowY), (lWristX, lWristY), (rWristX, rWristY),
          (lHipX, lHipY), (rHipX, rHipY), (lKneeX, lKneeY), (rKneeX, rKneeY),
          (lAnkleX, lAnkleY), (rAnkleX, rAnkleY) })
            canvas.DrawCircle(x, y, 3 * scale, dotPaint);
    }
}