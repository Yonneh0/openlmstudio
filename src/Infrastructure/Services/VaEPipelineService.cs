using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;
using SkiaSharp;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// ONNX Runtime-based VAE pipeline service for latent space encoding/decoding.
/// Loads safetensors VAE models and runs inference to encode/decode pixel-space images.
/// </summary>
public class VAEPipelineService : IVAEPipelineService, IDisposable
{
    private readonly ILogger<VAEPipelineService>? _logger;
    private readonly IModelRepository _modelRepo;

    /// <summary>ONNX Runtime sessions keyed by model ID.</summary>
    private readonly Dictionary<string, (InferenceSession Session, VaeTensorMap Tensors)> _loadedSessions = new(StringComparer.OrdinalIgnoreCase);

    public VAEPipelineService(ILogger<VAEPipelineService>? logger, IModelRepository modelRepo)
    {
        _logger = logger;
        _modelRepo = modelRepo;
        _logger?.LogInformation("VAEPipelineService initialized (ONNX Runtime-based)");
    }

    public async Task<byte[]> EncodeAsync(string vaeModelId, byte[] imageBytes, CancellationToken ct = default)
    {
        // Ensure VAE model is loaded
        var wasAlreadyLoaded = _loadedSessions.ContainsKey(vaeModelId);
        if (!wasAlreadyLoaded)
        {
            var loaded = await LoadModelAsync(vaeModelId);
            if (!loaded || !_loadedSessions.ContainsKey(vaeModelId))
                throw new InvalidOperationException($"Failed to load VAE model '{vaeModelId}' before encoding.");
        }

        var (session, tensors) = _loadedSessions[vaeModelId];

        // Decode PNG/JPEG image bytes into pixel values [1, channels, height, width]
        var (pixels, channels, height, width) = ImageToPixels(imageBytes);
        if (channels != 3 && channels != 4)
            throw new InvalidOperationException($"VAE expects RGB (3 channels) or RGBA (4 channels) images, got {channels}");

        // Normalize to [-1, 1] for SD-style VAEs and create input tensor
        var inputTensor = PixelValuesToInputTensor(pixels, channels, height, width);

        // Run encoder inference using ONNX Runtime session.Run with named inputs/outputs
        var encoderInputValues = new NamedOnnxValue[] { NamedOnnxValue.CreateFromTensor(tensors.EncoderInputName, inputTensor) };
        var outputNames = new string[] { tensors.LatentOutputName };
        var results = session.Run(encoderInputValues, outputNames);

        // Extract latent output tensor [1, embeddingDim, h/8, w/8] and convert to bytes
        using var latentResult = results.First(r => r.Name == tensors.LatentOutputName);
        float[] latentData = latentResult.AsEnumerable<float>().ToArray();
        return DenseTensorToBytes(latentData);
    }

    public async Task<byte[]> DecodeAsync(string vaeModelId, byte[] latents, CancellationToken ct = default)
    {
        // Ensure VAE model is loaded
        var wasAlreadyLoaded = _loadedSessions.ContainsKey(vaeModelId);
        if (!wasAlreadyLoaded)
        {
            var loaded = await LoadModelAsync(vaeModelId);
            if (!loaded || !_loadedSessions.ContainsKey(vaeModelId))
                throw new InvalidOperationException($"Failed to load VAE model '{vaeModelId}' before decoding.");
        }

        var (session, tensors) = _loadedSessions[vaeModelId];

        // Create input tensor from latent bytes [1, embeddingDim, h/8, w/8]
        var denseTensor = LatentBytesToDenseTensor(latents, session.OutputMetadata[tensors.DecoderInputName].Dimensions.Cast<int>().ToArray());

        // Run decoder inference — output is pixel-space RGB image in [-1, 1] range
        var decoderInputValues = new NamedOnnxValue[] { NamedOnnxValue.CreateFromTensor(tensors.DecoderInputName, denseTensor) };
        var outputNames2 = new string[] { tensors.PixelOutputName };
        var results3 = session.Run(decoderInputValues, outputNames2);

        // Extract output tensor [1, 3, H, W] and convert to PNG bytes — get shape from ONNX session metadata
        using var pixelResult = results3.First(r => r.Name == tensors.PixelOutputName);
        float[] pixelData = pixelResult.AsEnumerable<float>().ToArray();

        // Get the output dimensions from the ONNX Runtime session's OutputMetadata (shape is [1, 3, H, W] for NCHW)
        var outputShape = session.OutputMetadata[tensors.PixelOutputName].Dimensions;
        return PixelValuesToPng(pixelData, outputShape[2], outputShape[3]); // height, width
    }

    public async Task<IEnumerable<MultiModalModelMetadata>> GetAvailableModelsAsync()
    {
        var models = await _modelRepo.SearchMultiModalModelsAsync(modelTypeFilter: ModelType.Vae);
        return models;
    }

    /// <summary>
    /// Loads a VAE safetensors model into ONNX Runtime.
    /// Returns true if successful, false otherwise.
    /// </summary>
    public async Task<bool> LoadModelAsync(string vaeModelId)
    {
        // Already loaded — nothing to do
        if (_loadedSessions.ContainsKey(vaeModelId))
            return true;

        try
        {
            _logger?.LogInformation("Loading VAE model: {VaeModelId}", vaeModelId);

            var metadata = await _modelRepo.GetMultiModalModelByIdAsync(vaeModelId);
            if (metadata == null)
                throw new InvalidOperationException($"VAE model '{vaeModelId}' not found in repository.");

            // Determine the primary weight file path
            var weightFilePath = GetPrimaryWeightFile(metadata);
            if (string.IsNullOrEmpty(weightFilePath) || !File.Exists(weightFilePath))
                throw new FileNotFoundException($"VAE weight file not found: {weightFilePath}");

            // Validate safetensors header before loading
            var safetensorParser = new SafetensorParser((ILogger<SafetensorParser>?)_logger);
            var headerValid = await safetensorParser.ValidateHeaderAsync(weightFilePath);
            if (!headerValid)
                throw new InvalidDataException($"VAE safetensors header validation failed for: {vaeModelId}");

            // For large models (>8GB), use memory-mapped I/O to reduce peak RAM usage
            var fileSize = new FileInfo(weightFilePath).Length;
            SessionOptions sessionOptions = new();
            if (fileSize > 8L * 1024 * 1024 * 1024) // >8GB — use memory mapping
            {
                _logger?.LogInformation("Large VAE model detected ({Size} bytes) for '{VaeModelId}' — using memory-mapped weight loading", fileSize, vaeModelId);
                sessionOptions = new SessionOptions();
            }

            var inferenceSession = new InferenceSession(weightFilePath, sessionOptions);

            // Parse tensor names from the model to identify input/output mappings
            var tensors = ParseVaETensors(inferenceSession);

            _loadedSessions[vaeModelId] = (inferenceSession, tensors);

            _logger?.LogInformation("VAE model '{VaeModelId}' loaded successfully — {Size} bytes", vaeModelId, fileSize);

            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or FileNotFoundException or InvalidDataException)
        {
            _logger?.LogError(ex, "Failed to load VAE model: {VaeModelId}", vaeModelId);

            // Clean up partial loading state
            if (_loadedSessions.ContainsKey(vaeModelId))
                _loadedSessions[vaeModelId].Session.Dispose();
            _loadedSessions.Remove(vaeModelId);
            return false;
        }
    }

    /// <summary>
    /// Unloads a VAE model from ONNX Runtime.
    /// </summary>
    public async Task<bool> UnloadModelAsync(string vaeModelId)
    {
        if (!_loadedSessions.ContainsKey(vaeModelId))
            return false;

        _logger?.LogDebug("Unloading VAE model: {VaeModelId}", vaeModelId);

        var (session, _) = _loadedSessions[vaeModelId];
        session.Dispose();
        _loadedSessions.Remove(vaeModelId);

        return true;
    }

    /// <summary>
    /// Gets all currently loaded VAE model IDs.
    /// </summary>
    public async Task<IEnumerable<string>> GetLoadedModelsAsync()
    {
        return _loadedSessions.Keys.ToList();
    }

    public void Dispose()
    {
        foreach (var (session, _) in _loadedSessions.Values)
            session.Dispose();
        _loadedSessions.Clear();
    }

    // ---- Helpers ----

    /// <summary>
    /// Gets the primary weight file path from model metadata.
    /// </summary>
    private string? GetPrimaryWeightFile(MultiModalModelMetadata metadata)
    {
        if (metadata.FilePath != null && File.Exists(metadata.FilePath))
            return metadata.FilePath;

        // Try sharded index for multi-file VAEs
        var indexPath = $"{metadata.FilePath}.index.json";
        if (!string.IsNullOrEmpty(metadata.FilePath) && File.Exists(indexPath))
        {
            try
            {
                var indexJson = File.ReadAllText(indexPath);
                var indexDoc = System.Text.Json.JsonDocument.Parse(indexJson);
                var weightMap = indexDoc.RootElement.GetProperty("weight_map");

                // VAE models typically have encoder/decoder weights — find first non-null entry
                foreach (var kvp in weightMap.EnumerateObject())
                {
                    var value = kvp.Value.GetString();
                    if (!string.IsNullOrEmpty(value) && File.Exists(Path.Combine(metadata.FilePath, value)))
                        return Path.Combine(metadata.FilePath, value);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to parse VAE safetensors index: {Path}", indexPath);
            }
        }

        // Try finding any .safetensors file in the model directory
        var dir = metadata.FilePath;
        if (dir != null && Directory.Exists(dir))
        {
            var safetensorsFiles = Directory.GetFiles(dir, "*.safetensors");
            return safetensorsFiles.Length > 0 ? Path.Combine(dir, safetensorsFiles[0]) : null;
        }

        _logger?.LogWarning("Cannot determine weight file for VAE model '{ModelId}' — no FilePath set", metadata.Id);
        return null;
    }

    /// <summary>
    /// Parses ONNX Runtime model to identify input/output tensor names specific to VAE architecture.
    /// Uses session metadata to discover actual tensor names rather than hardcoding them.
    /// </summary>
    private static VaeTensorMap ParseVaETensors(InferenceSession session)
    {
        var inputNames = session.InputMetadata.Keys.ToList();
        var outputNames = session.OutputMetadata.Keys.ToList();

        // Find encoder input: prefer "sample" or "pixel_values", fall back to first input
        string encoderInput = inputNames.FirstOrDefault(n =>
            n == "sample" || n == "pixel_values" || n.Contains("pixel", StringComparison.OrdinalIgnoreCase))
            ?? inputNames.FirstOrDefault() ?? "sample";

        // Find latent output (encoder output): prefer "latent_dist.mean", then "latents", then first output
        string latentOutput = outputNames.FirstOrDefault(n =>
            n.Contains("latent", StringComparison.OrdinalIgnoreCase))
            ?? outputNames.FirstOrDefault() ?? "latent_dist.mean";

        // Find decoder input: prefer "z" or "latents", fall back to first input not used as encoder input
        string decoderInput = inputNames.FirstOrDefault(n =>
            n == "z" || n.Contains("latent", StringComparison.OrdinalIgnoreCase))
            ?? inputNames.Where(n => n != encoderInput).FirstOrDefault() ?? "z";

        // Find pixel output (decoder output): prefer "x_sample", then "output", then last output
        string pixelOutput = outputNames.FirstOrDefault(n =>
            n.Contains("x_sample", StringComparison.OrdinalIgnoreCase) || n.Contains("recon", StringComparison.OrdinalIgnoreCase))
            ?? outputNames.LastOrDefault() ?? "x_sample";

        return new VaeTensorMap(encoderInput, latentOutput, decoderInput, pixelOutput);
    }

    /// <summary>
    /// Converts image bytes to pixel values with normalization.
    /// Returns (pixels, channels, height, width) where pixels is flattened [1, H*W*C] array of float32 in [-1, 1].
    /// </summary>
    private static (float[] Pixels, int Channels, int Height, int Width) ImageToPixels(byte[] imageBytes)
    {
        // Use SkiaSharp for cross-platform PNG/JPEG decoding — works on Windows/Linux/macOS
        using var bitmap = SKBitmap.Decode(new MemoryStream(imageBytes));
        SKBitmap bitmapToUse = bitmap;

        var width = bitmap.Width;
        var height = bitmap.Height;

        // Convert to RGB if needed (RGBA -> RGB by dropping alpha)
        if (bitmap.ColorType == SKColorType.Rgba8888)
        {
            var rgbBitmap = new SKBitmap(bitmap.Width, bitmap.Height, SKColorType.Rgb888x, bitmap.AlphaType);
            using var canvas = new SKCanvas(rgbBitmap);
            canvas.DrawBitmap(bitmap, 0, 0);
            bitmap.Dispose();
            bitmapToUse = rgbBitmap;
        }
        else if (bitmap.ColorType != SKColorType.Rgb888x)
        {
            throw new InvalidOperationException($"VAE expects RGB images, got {bitmap.ColorType}");
        }

        var pixels = new float[width * height]; // Will be [1, C, H, W] after reshaping to tensor

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var color = bitmapToUse.GetPixel(x, y);
                // Normalize RGB from [0, 255] to [-1, 1]: value * 2/255 - 1
                pixels[y * width + x] = (color.Red / 255f) * 2f - 1f;
            }
        }

        return (pixels, 3, height, width);
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
                    // For RGB image stored as flat array: pixels[c * height * width + h * width + w] would be needed
                    // But our ImageToPixels returns flattened HWC — we only have one channel of pixel data here
                    // The pixel values are already normalized to [-1, 1], stored in order [0, H*W-1] for R channel
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
        const int channels = 3; // RGB

        // Create bitmap with 3-channel RGB format using SkiaSharp (cross-platform compatible)
        using var skBitmap = new SKBitmap(width, height);

        for (int h = 0; h < height; h++)
        {
            for (int w = 0; w < width; w++)
            {
                // Accumulate all three channels for this pixel position in one pass.
                float r = 0, g = 0, b = 0;
                for (int c = 0; c < channels; c++)
                {
                    var pixelValue = pixelData[c * height * width + h * width + w];
                    // Denormalize from [-1, 1] to [0, 255]: value * 127.5 + 127.5
                    float clampedPixel = Math.Clamp(pixelValue * 127.5f + 127.5f, 0, 255);
                    if (c == 0) r = clampedPixel;
                    else if (c == 1) g = clampedPixel;
                    else b = clampedPixel;
                }

                skBitmap.SetPixel(w, h, new SKColor((byte)r, (byte)g, (byte)b));
            }
        }

        // Convert bitmap to PNG bytes using SkiaSharp
        using var image = SKImage.FromBitmap(skBitmap);
        using var imageData = image.Encode(SKEncodedImageFormat.Png, 100);
        return imageData.ToArray();
    }

    /// <summary>
    /// Converts a flat float32 byte array (NCHW layout [1, embeddingDim, h/8, w/8]) to a DenseTensor.
    /// </summary>
    /// <summary>
    /// Converts a flat float32 byte array to a DenseTensor using the provided dimensions from model metadata.
    /// Falls back to heuristic inference when dimensions are unknown.
    /// </summary>
    private static DenseTensor<float> LatentBytesToDenseTensor(byte[] latents, int[]? dimensions = null)
    {
        var elementCount = latents.Length / sizeof(float);

        // Use explicit dimensions when provided (from model metadata) — preferred path
        if (dimensions != null && dimensions.Length == 4)
        {
            var explicitTensor = new DenseTensor<float>(dimensions);
            int copyCount = (int)Math.Min(elementCount, explicitTensor.Length);
            for (int i = 0; i < copyCount && i * sizeof(float) < latents.Length; i++)
                explicitTensor[i] = BitConverter.ToSingle(latents, i * sizeof(float));
            return explicitTensor;
        }

        // Heuristic fallback: infer [1, embeddingDim, h/8, w/8] from total element count.
        // Factor elementCount into 4 dimensions: 1 × embeddingDim × side × side.
        int embeddingDim = 4; // default
        int side = (int)Math.Round(Math.Pow(elementCount, 0.25));
        if (side <= 0) side = 4;
        embeddingDim = elementCount / (side * side);
        if (embeddingDim <= 0) embeddingDim = 4;

        var dims = new[] { 1, embeddingDim, side, side };
        var tensor = new DenseTensor<float>(dims);
        int tensorLength = dims[1] * dims[2] * dims[3];
        int heuristicCopyCount = (int)Math.Min(elementCount, tensorLength);
        for (int i = 0; i < heuristicCopyCount && i * sizeof(float) < latents.Length; i++)
            tensor[i] = BitConverter.ToSingle(latents, i * sizeof(float));
        return tensor;
    }

    /// <summary>
    /// Converts a flat float32 array to byte array using little-endian packing.
    /// </summary>
    private static byte[] DenseTensorToBytes(IReadOnlyList<float> tensor)
    {
        var bytes = new byte[tensor.Count * sizeof(float)];
        for (int i = 0; i < tensor.Count; i++)
        {
            BitConverter.TryWriteBytes(new Span<byte>(bytes, i * sizeof(float), sizeof(float)), tensor[i]);
        }
        return bytes;
    }

    /// <summary>
    /// Holds ONNX Runtime tensor names for VAE model I/O.
    /// </summary>
    private readonly record struct VaeTensorMap(
        string EncoderInputName,   // "sample" — input pixel-space image name in session
        string LatentOutputName,   // "latent_dist.mean" or similar — encoder output latent tensor
        string DecoderInputName,   // "z" or similar — decoder input (latent) tensor
        string PixelOutputName     // "x_sample" — decoder output reconstructed pixel-space image name
    );
}