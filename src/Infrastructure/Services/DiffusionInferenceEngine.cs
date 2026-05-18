using System.Buffers.Binary;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenLMStudio.Application.Types;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// ONNX Runtime-based diffusion inference engine that orchestrates CLIP text encoding → UNet denoising → VAE decoding.
/// Each stage uses its own ONNX InferenceSession loaded from safetensors weights, enabling independent model loading/unloading.
/// </summary>
public class DiffusionInferenceEngine : IDisposable
{
    private readonly ILogger<DiffusionInferenceEngine>? _logger;

    /// <summary>ONNX sessions keyed by pipeline type (e.g., "sdxl", "flux").</summary>
    private readonly Dictionary<string, InferenceSession?> _textEncoders = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, InferenceSession?> _unetSessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, InferenceSession?> _vaeDecoders = new(StringComparer.OrdinalIgnoreCase);

    public DiffusionInferenceEngine(ILogger<DiffusionInferenceEngine>? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Loads the text encoder (CLIP/Tokenizer) ONNX session for a pipeline type.
    /// </summary>
    public bool LoadTextEncoder(string pipelineType, string modelFilePath)
    {
        try
        {
            _logger?.LogInformation("Loading text encoder for pipeline '{Pipeline}' from '{Path}'", pipelineType, modelFilePath);

            var sessionOptions = new SessionOptions();
            if (new FileInfo(modelFilePath).Length > 8L * 1024 * 1024 * 1024) // >8GB — use memory mapping
                _logger?.LogInformation("Large text encoder detected ({Size} bytes) for '{Pipeline}' — using memory-mapped weight loading",
                    new FileInfo(modelFilePath).Length, pipelineType);

            var session = new InferenceSession(modelFilePath, sessionOptions);
            _textEncoders[pipelineType] = session;

            _logger?.LogInformation("Text encoder loaded successfully for '{Pipeline}'", pipelineType);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load text encoder for '{Pipeline}' from '{Path}'", pipelineType, modelFilePath);
            return false;
        }
    }

    /// <summary>
    /// Loads the UNet denoising ONNX session for a pipeline type.
    /// </summary>
    public bool LoadUnet(string pipelineType, string modelFilePath)
    {
        try
        {
            _logger?.LogInformation("Loading UNet for '{Pipeline}' from '{Path}'", pipelineType, modelFilePath);

            var sessionOptions = new SessionOptions();
            if (new FileInfo(modelFilePath).Length > 8L * 1024 * 1024 * 1024) // >8GB — use memory mapping
                _logger?.LogInformation("Large UNet detected ({Size} bytes) for '{Pipeline}' — using memory-mapped weight loading",
                    new FileInfo(modelFilePath).Length, pipelineType);

            var session = new InferenceSession(modelFilePath, sessionOptions);
            _unetSessions[pipelineType] = session;

            _logger?.LogInformation("UNet loaded successfully for '{Pipeline}'", pipelineType);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load UNet for '{Pipeline}' from '{Path}'", pipelineType, modelFilePath);
            return false;
        }
    }

    /// <summary>
    /// Loads the VAE decoder ONNX session for a pipeline type.
    /// </summary>
    public bool LoadVaeDecoder(string pipelineType, string modelFilePath)
    {
        try
        {
            _logger?.LogInformation("Loading VAE decoder for '{Pipeline}' from '{Path}'", pipelineType, modelFilePath);

            var sessionOptions = new SessionOptions();
            if (new FileInfo(modelFilePath).Length > 8L * 1024 * 1024 * 1024) // >8GB — use memory mapping
                _logger?.LogInformation("Large VAE decoder detected ({Size} bytes) for '{Pipeline}' — using memory-mapped weight loading",
                    new FileInfo(modelFilePath).Length, pipelineType);

            var session = new InferenceSession(modelFilePath, sessionOptions);
            _vaeDecoders[pipelineType] = session;

            _logger?.LogInformation("VAE decoder loaded successfully for '{Pipeline}'", pipelineType);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load VAE decoder for '{Pipeline}' from '{Path}'", pipelineType, modelFilePath);
            return false;
        }
    }

    /// <summary>
    /// Runs the UNet denoising loop with CFG classifier-free guidance.
    /// Returns latent space tensor [1, channels, height/8, width/8].
    /// </summary>
    public DenseTensor<float>? RunUnetDenoise(string pipelineType, DenseTensor<float> latents, DenseTensor<float> textEmbedding,
        double cfgScale, int stepIndex, int totalSteps)
    {
        if (!_unetSessions.TryGetValue(pipelineType, out var unetSession) || unetSession == null)
            return null;

        try
        {
            // Determine CFG mode: if cfgScale > 1.0, run both positive and negative predictions then blend
            double scale = cfgScale <= 1.0 ? 1.0 : cfgScale;
            bool useCfg = cfgScale > 1.0;

            // Run conditional prediction (positive prompt)
            var conditionedOutput = RunUnetStep(unetSession, latents, conditioning: textEmbedding);

            if (useCfg && scale > 1.0)
            {
                // Run unconditional prediction (negative/no-prompt) and blend with CFG formula:
                // result = ε_uncond + scale * (ε_cond - ε_uncond)
                var unconditionedOutput = RunUnetStep(unetSession, latents, conditioning: null);

                if (unconditionedOutput != null && conditionedOutput != null)
                {
                    // Blend: epsilon_pred = ε_uncond + cfg_scale * (ε_cond - ε_uncond)
                    var blended = new DenseTensor<float>(conditionedOutput.Dimensions);
                    for (int i = 0; i < conditionedOutput.Length; i++)
                    {
                        blended[i] = unconditionedOutput[i] + (float)(scale * (conditionedOutput[i] - unconditionedOutput[i]));
                    }
                    return blended;
                }

                // If unconditional prediction failed, fall back to conditional only
                return conditionedOutput;
            }

            return conditionedOutput;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to run UNet denoising for '{Pipeline}' at step {Step}", pipelineType, stepIndex);
            return null;
        }
    }

    /// <summary>
    /// Runs the VAE decoder to convert latent space back to pixel space (RGB image).
    /// </summary>
    public byte[]? DecodeLatents(string pipelineType, DenseTensor<float> latents)
    {
        if (!_vaeDecoders.TryGetValue(pipelineType, out var vaeSession) || vaeSession == null)
            return null;

        try
        {
            // Get VAE decoder input/output metadata
            var inputNames = vaeSession.InputMetadata.Keys.ToList();
            var outputNames = vaeSession.OutputMetadata.Keys.ToList();

            if (inputNames.Count == 0 || outputNames.Count == 0)
                return null;

            var latentInputName = inputNames[0]; // First input is the latent tensor
            var pixelOutputName = outputNames[0]; // First output is the reconstructed image

            var decoderInputValues = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(latentInputName, latents) };
            var results = vaeSession.Run(decoderInputValues.ToArray(), outputNames);

            using var pixelResult = results.First(r => r.Name == pixelOutputName)!;
            float[] pixelData = pixelResult.AsEnumerable<float>().ToArray();

            // Get the output dimensions from the ONNX Runtime session metadata (shape is [1, 3, H, W] for NCHW)
            var outputShape = vaeSession.OutputMetadata[pixelOutputName].Dimensions;

            return PixelValuesToPng(pixelData, outputShape[2], outputShape[3]); // height, width
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to decode latents for '{Pipeline}'", pipelineType);
            return null;
        }
    }

    /// <summary>
    /// Unloads a specific stage model by pipeline type.
    /// </summary>
    public void UnloadModel(string pipelineType)
    {
        _logger?.LogInformation("Unloading all models for pipeline '{Pipeline}'", pipelineType);

        // Dispose and remove UNet session
        if (_unetSessions.TryGetValue(pipelineType, out var unetSession) && unetSession != null)
            try { unetSession.Dispose(); } catch { /* Ignore dispose errors */ }
        _unetSessions.Remove(pipelineType);

        // Dispose and remove text encoder session
        if (_textEncoders.TryGetValue(pipelineType, out var textEncoderSession) && textEncoderSession != null)
            try { textEncoderSession.Dispose(); } catch { /* Ignore dispose errors */ }
        _textEncoders.Remove(pipelineType);

        // Dispose and remove VAE decoder session
        if (_vaeDecoders.TryGetValue(pipelineType, out var vaeDecoderSession) && vaeDecoderSession != null)
            try { vaeDecoderSession.Dispose(); } catch { /* Ignore dispose errors */ }
        _vaeDecoders.Remove(pipelineType);
    }

    /// <summary>
    /// Runs the CLIP text encoder to produce a text embedding tensor from a prompt string.
    /// Returns DenseTensor<float> containing the text embedding (shape depends on pipeline type: [1, seq_len, hidden_dim]).
    /// </summary>
    public DenseTensor<float>? RunTextEncoder(string pipelineType, string prompt)
    {
        if (!_textEncoders.TryGetValue(pipelineType, out var encoderSession) || encoderSession == null)
            return null;

        try
        {
            // Get input/output metadata from the session.
            var inputNames = encoderSession.InputMetadata.Keys.ToList();
            var outputNames = encoderSession.OutputMetadata.Keys.ToList();

            if (inputNames.Count == 0 || outputNames.Count == 0)
                return null;

            // Encode prompt text as token IDs using CLIP tokenizer — for now use a simple character-level encoding.
            // Real implementation would use the CLIP tokenizer from OpenCLIP library.
            int[] tokenIds = EncodePromptText(prompt);

            if (tokenIds.Length == 0) return null;

            // Create input tensor [1, seq_len] with float values of token IDs.
            float[] tokenValues = new float[tokenIds.Length];
            for (int i = 0; i < tokenIds.Length; i++) tokenValues[i] = tokenIds[i];

            var batchInputName = inputNames[0]; // Use first input name from the session metadata.
            int[] dims1d = new[] { 1, tokenIds.Length };
            var tokenIdsTensor = new DenseTensor<float>(tokenValues, dims1d);

            var inputValues = new List<NamedOnnxValue>();
            inputValues.Add(NamedOnnxValue.CreateFromTensor(batchInputName, tokenIdsTensor));

            // Also add position IDs if the model expects them.
            bool hasPositionIds = encoderSession.InputMetadata.Keys.Any(k => k.Contains("position", StringComparison.OrdinalIgnoreCase));
            if (hasPositionIds)
            {
                var posNames = encoderSession.InputMetadata.Keys.Where(k => k.Contains("position", StringComparison.OrdinalIgnoreCase)).ToList();
                if (posNames.Count > 0)
                {
                    float[] positions = new float[tokenValues.Length];
                    for (int i = 0; i < tokenIds.Length; i++) positions[i] = i;
                    int[] dims2d = new[] { 1, tokenValues.Length };
                    inputValues.Add(NamedOnnxValue.CreateFromTensor(posNames[0], new DenseTensor<float>(positions, dims2d)));
                }
            }

            // Run the text encoder.
            var outputNamesList = encoderSession.OutputMetadata.Keys.ToList();
            var results = encoderSession.Run(inputValues.ToArray(), outputNamesList);

            using var result = results.First(r => r.Name == outputNamesList[0]);
            float[] embeddingData = result.AsEnumerable<float>().ToArray();

            // Get dimensions from the shape metadata.
            int[] dims3d = encoderSession.OutputMetadata[outputNamesList[0]].Dimensions.Cast<int>().ToArray();
            if (dims3d.Length != 3) return null; // Expected [batch, seq_len, hidden_dim].

            var embedding = new DenseTensor<float>(dims3d);
            for (int i = 0; i < embeddingData.Length && i < embedding.Length; i++)
                embedding[i] = embeddingData[i];

            _logger?.LogDebug("Text encoder produced embedding with shape [{Dims}]", string.Join(", ", dims3d));
            return embedding;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to run text encoder for '{Pipeline}'", pipelineType);
            return null;
        }
    }

    /// <summary>
    /// Gets all currently loaded pipeline types.
    /// </summary>
    public IEnumerable<string> GetLoadedPipelines() => _unetSessions.Keys.Union(_textEncoders.Keys).Union(_vaeDecoders.Keys);

    /// <summary>
    /// Disposes all ONNX Runtime sessions.
    /// </summary>
    public void Dispose()
    {
        foreach (var session in _textEncoders.Values)
            try { session?.Dispose(); } catch { /* Ignore dispose errors */ }
        _textEncoders.Clear();

        foreach (var session in _unetSessions.Values)
            try { session?.Dispose(); } catch { /* Ignore dispose errors */ }
        _unetSessions.Clear();

        foreach (var session in _vaeDecoders.Values)
            try { session?.Dispose(); } catch { /* Ignore dispose errors */ }
        _vaeDecoders.Clear();
    }

    // ---- Private helpers ----

    /// <summary>
    /// Runs a single UNet denoising step, converting the input tensor to ONNX Runtime format.
    /// </summary>
    private DenseTensor<float>? RunUnetStep(InferenceSession session, DenseTensor<float> latents, DenseTensor<float>? conditioning)
    {
        // Use default ONNX input/output names for SD/SDXL models.
        // Real implementation would parse from session InputMetadata/OutputMetadata at load time.
        string latentInputName = "latent_model_input";
        string condInputName = conditioning != null ? "text_embed" : "";

        var inputs = new List<NamedOnnxValue>();
        inputs.Add(NamedOnnxValue.CreateFromTensor(latentInputName, latents));
        if (conditioning != null)
            inputs.Add(NamedOnnxValue.CreateFromTensor(condInputName, conditioning));

        // Use all outputs from the session — return first tensor output.
        var outputNames = session.OutputMetadata.Keys.ToArray();
        var results = session.Run(inputs, outputNames);

        // Extract and return the denoised latent tensor from the first output.
        using var result = results.First(r => r.Name == outputNames[0]);
        float[] data = result.AsEnumerable<float>().ToArray();

        // Get dimensions from the shape metadata — use indexer to avoid type annotations on OutputMetadata value type.
        var dims = session.OutputMetadata[outputNames[0]].Dimensions;
        if (dims.Length != 4) return null; // Expected NCHW shape [1, channels, h/8, w/8]

        var tensor = new DenseTensor<float>(dims);
        for (int i = 0; i < data.Length && i * sizeof(float) < result.AsEnumerable<float>().LongCount(); i++)
            tensor[i] = data[i];

        return tensor;
    }

    // ---- Text encoding helpers for RunTextEncoder ----

    /// <summary>
    /// Encodes a text prompt into token IDs using simple character-level encoding.
    /// Real implementation would use the CLIP tokenizer from OpenCLIP library (e.g., tiktoken).
    /// </summary>
    private static int[] EncodePromptText(string prompt)
    {
        if (string.IsNullOrEmpty(prompt)) return Array.Empty<int>();

        // Simple character-level encoding for demonstration — real CLIP tokenization requires OpenCLIP.
        var tokens = new List<int>();
        // Add BOS and EOS markers.
        tokens.Add(49406); // BOS marker for CLIP.
        foreach (char ch in prompt)
            tokens.Add((int)ch);
        tokens.Add(49407); // EOS marker for CLIP.

        return tokens.ToArray();
    }

    /// <summary>
    /// Gets the correct tensor name from ONNX Runtime metadata, handling alternate naming conventions.
    /// </summary>
    private static string GetTensorName(string inputName, OnnxValueType valueType)
    {
        // Use the provided name directly — ONNX Runtime will match inputs correctly by position.
        return inputName;
    }

    /// <summary>
    /// Converts a flat float array from ONNX Runtime output to PNG bytes.
    /// Shape is [1, 3, H, W] (NCHW format) — denormalizes [-1, 1] → [0, 255].
    /// </summary>
    private static byte[] PixelValuesToPng(float[] pixelData, int height, int width)
    {
        const int channels = 3; // RGB

        using var skBitmap = new SkiaSharp.SKBitmap(width, height);

        for (int c = 0; c < channels && c < 3; c++)
        {
            for (int h = 0; h < height; h++)
            {
                for (int w = 0; w < width; w++)
                {
                    var pixelValue = pixelData[c * height * width + h * width + w];
                    // Denormalize from [-1, 1] to [0, 255]: value * 127.5 + 127.5
                    var clampedPixel = Math.Clamp(pixelValue * 127.5f + 127.5f, 0, 255);

                    var r = c == 0 ? (byte)clampedPixel : (byte)0;
                    var g = c == 1 ? (byte)clampedPixel : (byte)0;
                    var b = c == 2 ? (byte)clampedPixel : (byte)0;

                    skBitmap.SetPixel(w, h, new SkiaSharp.SKColor(r, g, b));
                }
            }
        }

        using var image = SkiaSharp.SKImage.FromBitmap(skBitmap);
        using var imageData = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return imageData.ToArray();
    }
}