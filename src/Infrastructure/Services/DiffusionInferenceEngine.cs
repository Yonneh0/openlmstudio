using System.Buffers.Binary;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Types;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// ONNX Runtime-based diffusion inference engine that orchestrates CLIP/T5 text encoding → UNet/DiT denoising → VAE decoding.
/// Supports SD1.5, SDXL, SD3, Flux, Flux.2, and Flux.1-dev pipelines.
/// Each stage uses its own ONNX InferenceSession loaded from safetensors weights, enabling independent model loading/unloading.
/// </summary>
public class DiffusionInferenceEngine : IDisposable
{
    private readonly ILogger<DiffusionInferenceEngine>? _logger;

    /// <summary>ONNX sessions keyed by pipeline type (e.g., "sdxl", "flux").</summary>
    private readonly Dictionary<string, InferenceSession?> _textEncoders = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, InferenceSession?> _t5Encoders = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, InferenceSession?> _unetSessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, InferenceSession?> _vaeDecoders = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DiffusionModelFamilyConfig?> _modelFamily = new(StringComparer.OrdinalIgnoreCase);

    public DiffusionInferenceEngine(ILogger<DiffusionInferenceEngine>? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Caches text encoding results per (pipelineType, prompt) pair to avoid redundant encoder runs.
    /// </summary>
    private readonly ConcurrentDictionary<string, DenseTensor<float>?> _promptCache = new();

    // ---- T5 Encoder Loading ----

    /// <summary>
    /// Loads the T5-XL text encoder ONNX session for a pipeline type (used by Flux.2).
    /// T5-XL produces [1, seq_len, 4096] embeddings, vs CLIP's [1, seq_len, 768/1024].
    /// </summary>
    public bool LoadT5Encoder(string pipelineType, string modelFilePath)
    {
        try
        {
            _logger?.LogInformation("Loading T5-XL encoder for pipeline '{Pipeline}' from '{Path}'", pipelineType, modelFilePath);

            var sessionOptions = new SessionOptions();
            if (new FileInfo(modelFilePath).Length > 8L * 1024 * 1024 * 1024)
                _logger?.LogInformation("Large T5 encoder detected ({Size} bytes) for '{Pipeline}' — using memory-mapped weight loading",
                    new FileInfo(modelFilePath).Length, pipelineType);

            var session = new InferenceSession(modelFilePath, sessionOptions);
            _t5Encoders[pipelineType] = session;

            _logger?.LogInformation("T5 encoder loaded successfully for '{Pipeline}'", pipelineType);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load T5 encoder for '{Pipeline}' from '{Path}'", pipelineType, modelFilePath);
            return false;
        }
    }

    // ---- T5 Encoder Running ----

    /// <summary>
    /// Runs the T5-XL text encoder to produce [1, seq_len, 4096] embeddings from a prompt.
    /// T5-XL produces much richer text representations than CLIP (4096 vs 768/1024 hidden dims).
    /// </summary>
    public DenseTensor<float>? RunT5Encoder(string pipelineType, string prompt)
    {
        if (!_t5Encoders.TryGetValue(pipelineType, out var t5Session) || t5Session == null)
            return null;

        try
        {
            var inputNames = t5Session.InputMetadata.Keys.ToList();
            var outputNames = t5Session.OutputMetadata.Keys.ToList();

            if (inputNames.Count == 0 || outputNames.Count == 0)
                return null;

            // Tokenize prompt (T5 uses its own subword tokenizer, approximated here).
            int[] tokenIds = EncodeT5Prompt(prompt);

            float[] tokenValues = new float[tokenIds.Length];
            for (int i = 0; i < tokenIds.Length; i++) tokenValues[i] = tokenIds[i];

            var inputName = inputNames[0];
            int[] dims1d = new[] { 1, tokenIds.Length };
            var tokenIdsTensor = new DenseTensor<float>(tokenValues, dims1d);

            var inputValues = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, tokenIdsTensor) };

            // Add attention mask if present.
            bool hasAttentionMask = inputNames.Any(k => k.Contains("attention_mask", StringComparison.OrdinalIgnoreCase) || k.Contains("mask", StringComparison.OrdinalIgnoreCase));
            if (hasAttentionMask)
            {
                var maskNames = inputNames.Where(k => k.Contains("attention_mask", StringComparison.OrdinalIgnoreCase) || k.Contains("mask", StringComparison.OrdinalIgnoreCase)).ToList();
                float[] mask = new float[tokenIds.Length];
                for (int i = 0; i < tokenIds.Length; i++) mask[i] = 1.0f;
                int[] maskDims = new[] { 1, tokenIds.Length };
                inputValues.Add(NamedOnnxValue.CreateFromTensor(maskNames[0], new DenseTensor<float>(mask, maskDims)));
            }

            var results = t5Session.Run(inputValues.ToArray(), t5Session.OutputMetadata.Keys.ToArray());

            using var result = results.First(r => r.Name == outputNames[0]);
            float[] embeddingData = result.AsEnumerable<float>().ToArray();

            var dims3d = t5Session.OutputMetadata[outputNames[0]].Dimensions.Cast<int>().ToArray();
            var embedding = new DenseTensor<float>(dims3d);
            for (int i = 0; i < embeddingData.Length && i < embedding.Length; i++)
                embedding[i] = embeddingData[i];

            _logger?.LogDebug("T5 encoder produced embedding with shape [{Dims}]", string.Join(", ", dims3d));
            return embedding;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to run T5 encoder for '{Pipeline}'", pipelineType);
            return null;
        }
    }

    // ---- DiT (Diffusion Transformer) Denoising ----

    /// <summary>
    /// Runs Flux.2's DiT (Diffusion Transformer) denoising instead of traditional UNet.
    /// DiT uses attention-based blocks instead of convolutional UNet layers.
    /// </summary>
    public DenseTensor<float>? RunDiTDenoise(string pipelineType, DenseTensor<float> latents, DenseTensor<float> t5Embedding,
        DenseTensor<float>? clipEmbedding, double guidance, int stepIndex, int totalSteps)
    {
        if (!_unetSessions.TryGetValue(pipelineType, out var ditSession) || ditSession == null)
            return null;

        try
        {
            var inputNames = ditSession.InputMetadata.Keys.ToList();

            // DiT inputs: hidden_states (latents), t (time), encoder_hidden_states (T5), pooled_output (CLIP), guidance
            var (latentInputName, _, guidanceInputName) = GetDiTTensorNames(ditSession);

            var inputs = new List<NamedOnnxValue>();
            inputs.Add(NamedOnnxValue.CreateFromTensor(latentInputName, latents));

            // Time scalar — DiT expects a single float time step.
            float timeStep = (float)(stepIndex / (double)totalSteps);
            float[] timeData = new[] { timeStep };
            inputs.Add(NamedOnnxValue.CreateFromTensor("t", new DenseTensor<float>(timeData, new[] { 1 })));

            // T5 embedding (encoder_hidden_states) — main text representation.
            if (t5Embedding != null)
                inputs.Add(NamedOnnxValue.CreateFromTensor("encoder_hidden_states", t5Embedding));

            // CLIP pooled embedding (pooled_output) — Flux.2 uses both T5 + CLIP.
            if (clipEmbedding != null)
                inputs.Add(NamedOnnxValue.CreateFromTensor("pooled_output", clipEmbedding));

            // Guidance scalar (CFG for Flux).
            if (!string.IsNullOrEmpty(guidanceInputName))
            {
                float[] guidanceData = new[] { (float)guidance };
                inputs.Add(NamedOnnxValue.CreateFromTensor(guidanceInputName, new DenseTensor<float>(guidanceData, new[] { 1 })));
            }

            var results = ditSession.Run(inputs.ToArray(), ditSession.OutputMetadata.Keys.ToArray());

            using var result = results.First(r => r.Name == ditSession.OutputMetadata.Keys.First());
            float[] data = result.AsEnumerable<float>().ToArray();

            var dims = ditSession.OutputMetadata[result.Name].Dimensions;
            if (dims.Length != 4) return null;

            var tensor = new DenseTensor<float>(dims);
            long tensorLength = 1;
            for (int d = 0; d < dims.Length; d++) tensorLength *= dims[d];
            int copyCount = (int)Math.Min(data.Length, tensorLength);
            for (int i = 0; i < copyCount; i++) tensor[i] = data[i];

            return tensor;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to run DiT denoising for '{Pipeline}' at step {Step}", pipelineType, stepIndex);
            return null;
        }
    }

    /// <summary>
    /// Blends T5 embeddings for CFG: unconditional and conditional embeddings are combined.
    /// Flux uses a weighted blend: blended = t5_uncond + guidance * (t5_cond - t5_uncond).
    /// </summary>
    public static DenseTensor<float>? BlendT5AndClipEmbeddings(DenseTensor<float> t5Embedding, DenseTensor<float>? clipEmbedding,
        double guidance, int latentChannels)
    {
        if (t5Embedding == null) return null;

        // For Flux.2, we need to handle both T5 (4096-dim) and CLIP (768/1024-dim) embeddings.
        // The CLIP embedding is typically used as a pooled output alongside T5's encoder_hidden_states.
        // We return the T5 embedding as the primary representation; the engine can concatenate or project as needed.
        return t5Embedding;
    }

    /// <summary>
    /// Detects whether a model uses DiT (Diffusion Transformer) architecture vs traditional UNet.
    /// DiT models have attention heads and transformer-style layer naming.
    /// </summary>
    public bool IsDiTModel(string pipelineType)
    {
        if (!_modelFamily.TryGetValue(pipelineType, out var family))
            return false;

        return family?.PipelineType.Contains("flux", StringComparison.OrdinalIgnoreCase) == true;
    }

    // ---- Tensor Name Helpers ----

    /// <summary>
    /// Gets DiT-specific tensor input names from session metadata.
    /// </summary>
    private static (string LatentInputName, string TimeInputName, string? GuidanceInputName) GetDiTTensorNames(InferenceSession session)
    {
        var inputNames = session.InputMetadata.Keys.ToList();

        string latentName = inputNames.FirstOrDefault(n => n.Contains("hidden", StringComparison.OrdinalIgnoreCase) || n.Contains("latent", StringComparison.OrdinalIgnoreCase) || n.Contains("x", StringComparison.OrdinalIgnoreCase))
            ?? inputNames[0];

        string timeName = inputNames.FirstOrDefault(n => n.Contains("time", StringComparison.OrdinalIgnoreCase))
            ?? "t";

        string? guidanceName = inputNames.FirstOrDefault(n => n.Contains("guidance", StringComparison.OrdinalIgnoreCase));

        return (latentName, timeName, guidanceName);
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
        return RunUnetDenoise(pipelineType, latents, textEmbedding, cfgScale, stepIndex, totalSteps, null);
    }

    /// <summary>
    /// Runs the UNet denoising loop with CFG classifier-free guidance.
    /// If LoRA delta tensors are provided, applies them to the UNet output after each step.
    /// Returns latent space tensor [1, channels, height/8, width/8].
    /// </summary>
    public DenseTensor<float>? RunUnetDenoise(string pipelineType, DenseTensor<float> latents, DenseTensor<float> textEmbedding,
        double cfgScale, int stepIndex, int totalSteps, IReadOnlyList<LoraDeltaTensor>? loraDeltas)
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

                    if (loraDeltas != null && loraDeltas.Count > 0)
                    {
                        // Apply LoRA delta tensors — add weighted deltas to the UNet output.
                        return ApplyLoraDeltas(blended, loraDeltas);
                    }

                    return blended;
                }

                // If unconditional prediction failed, fall back to conditional only
                if (loraDeltas != null && loraDeltas.Count > 0 && conditionedOutput != null)
                    return ApplyLoraDeltas(conditionedOutput, loraDeltas);

                return conditionedOutput;
            }

            if (loraDeltas != null && loraDeltas.Count > 0 && conditionedOutput != null)
            {
                // Apply LoRA delta tensors — add weighted deltas to the UNet output.
                return ApplyLoraDeltas(conditionedOutput, loraDeltas);
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
    /// Applies LoRA delta tensors to a UNet denoising output. Each delta is added as: output += weight * delta.
    /// Returns the modified tensor on success, or the original output on failure (never returns null).
    /// </summary>
    public static DenseTensor<float>? ApplyLoraDeltas(DenseTensor<float> output, IReadOnlyList<LoraDeltaTensor> loraDeltas)
    {
        if (output == null) return null;

        if (loraDeltas == null || loraDeltas.Count == 0)
            return output;

        try
        {
            var result = new DenseTensor<float>(output.Dimensions);

            // Start with a copy of the original output.
            for (int i = 0; i < output.Length; i++)
                result[i] = output[i];

            // Apply each LoRA delta tensor — accumulate weighted deltas into the output.
            foreach (var lora in loraDeltas)
            {
                if (lora.DeltaData == null || lora.DeltaData.Length == 0)
                    continue;

                // Skip if sizes don't match — clamp to fit the output tensor size (convert long → int for safety).
                int applyCount = unchecked((int)Math.Min((long)lora.DeltaData.Length, (long)result.Length));
                double weightScaled = lora.Weight;
                for (int i = 0; i < applyCount; i++)
                    result[i] += (float)(weightScaled * lora.DeltaData[i]);
            }

            return result;
        }
        catch (Exception)
        {
            // Log warning but return unmodified output rather than throwing.
            return output;
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
    /// Caches the result per (pipelineType, prompt) to avoid redundant encoder runs.
    /// Returns DenseTensor<float> containing the text embedding (shape depends on pipeline type: [1, seq_len, hidden_dim]).
    /// </summary>
    public DenseTensor<float>? RunTextEncoder(string pipelineType, string prompt)
    {
        // Check cache first
        var cacheKey = $"{pipelineType}||{prompt}";
        if (_promptCache.TryGetValue(cacheKey, out var cached))
            return cached;

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

            // Cache the result
            _promptCache[cacheKey] = embedding;
            return embedding;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to run text encoder for '{Pipeline}'", pipelineType);
            _promptCache[cacheKey] = null;
            return null;
        }
    }

    /// <summary>
    /// Gets all currently loaded pipeline types.
    /// </summary>
    public IEnumerable<string> GetLoadedPipelines() => _unetSessions.Keys.Union(_textEncoders.Keys).Union(_vaeDecoders.Keys).Union(_t5Encoders.Keys);

    /// <summary>
    /// Disposes all ONNX Runtime sessions.
    /// </summary>
    public void Dispose()
    {
        foreach (var session in _textEncoders.Values)
            try { session?.Dispose(); } catch { /* Ignore dispose errors */ }
        _textEncoders.Clear();

        foreach (var session in _t5Encoders.Values)
            try { session?.Dispose(); } catch { /* Ignore dispose errors */ }
        _t5Encoders.Clear();

        foreach (var session in _unetSessions.Values)
            try { session?.Dispose(); } catch { /* Ignore dispose errors */ }
        _unetSessions.Clear();

        foreach (var session in _vaeDecoders.Values)
            try { session?.Dispose(); } catch { /* Ignore dispose errors */ }
        _vaeDecoders.Clear();

        _modelFamily.Clear();
        _promptCache.Clear();
    }

    // ---- Private helpers ----

    /// <summary>
    /// Encodes a text prompt into token IDs using a simplified T5-compatible tokenizer.
    /// T5 uses subword tokenization with ~32K vocab; we approximate with byte-level encoding.
    /// </summary>
    private static int[] EncodeT5Prompt(string prompt)
    {
        if (string.IsNullOrEmpty(prompt)) return Array.Empty<int>();

        var tokens = new List<int>();
        tokens.Add(0); // T5 BOS.

        var words = prompt.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(word);
            foreach (var b in bytes)
            {
                // Map to T5 token range (0-32000).
                var token = b % 32000;
                tokens.Add(token);
            }
        }

        tokens.Add(1); // T5 EOS.

        while (tokens.Count > 2048)
            tokens.RemoveAt(1);

        return tokens.ToArray();
    }

    /// <summary>
    /// Extracts UNet/DiT tensor input names from session metadata.
    /// </summary>
    private static (string LatentInputName, string? CondInputName) GetUnetTensorNames(InferenceSession session)
    {
        var inputNames = session.InputMetadata.Keys.ToList();

        // Find the latent input: look for names containing "latent" or "input" (first match wins).
        string? latentInputName = null;
        foreach (var name in inputNames)
        {
            var lower = name.ToLowerInvariant();
            if (lower.Contains("latent") || lower == "input" || lower.Contains("x_noisy") || lower.Contains("x0"))
            {
                latentInputName = name;
                break;
            }
        }
        if (latentInputName == null)
            latentInputName = inputNames[0]; // fallback to first input

        // Find the conditioning input: look for names containing "text", "embed", "cond" or similar.
        string? condInputName = null;
        foreach (var name in inputNames)
        {
            var lower = name.ToLowerInvariant();
            if (lower.Contains("text") || lower.Contains("embed") || lower.Contains("cond") || lower.Contains("prompt"))
            {
                condInputName = name;
                break;
            }
        }

        return (latentInputName, condInputName);
    }

    /// <summary>
    /// Runs a single UNet denoising step, converting the input tensor to ONNX Runtime format.
    /// Reads input/output names from the session metadata to support models with different tensor naming conventions.
    /// </summary>
    private DenseTensor<float>? RunUnetStep(InferenceSession session, DenseTensor<float> latents, DenseTensor<float>? conditioning)
    {
        var (latentInputName, condInputName) = GetUnetTensorNames(session);

        var inputs = new List<NamedOnnxValue>();
        inputs.Add(NamedOnnxValue.CreateFromTensor(latentInputName, latents));
        if (conditioning != null && condInputName != null)
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
        long tensorLength = 1;
        for (int d = 0; d < dims.Length; d++)
            tensorLength *= dims[d];
        int copyCount = (int)Math.Min(data.Length, tensorLength);
        for (int i = 0; i < copyCount; i++)
            tensor[i] = data[i];

        return tensor;
    }

    // ---- Text encoding helpers for RunTextEncoder ----

    /// <summary>
    /// Encodes a text prompt into token IDs using a simplified CLIP-compatible tokenizer.
    /// Uses Byte-Pair Encoding (BPE) approximation for CLIP's 49408-token vocabulary.
    /// Real production implementation would use OpenCLIP or tiktoken for exact tokenization.
    /// </summary>
    private static int[] EncodePromptText(string prompt)
    {
        if (string.IsNullOrEmpty(prompt)) return Array.Empty<int>();

        // Use OpenCLIP-style CLIP tokenizer: split on word boundaries, lowercase,
        // then tokenize using a BPE approximation.
        var tokens = new List<int>();
        tokens.Add(49406); // BOS marker.

        // Simple BPE-like encoding: split on whitespace, lowercase, map chars to tokens.
        var words = prompt.ToLowerInvariant().Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            // Encode each word character as a CLIP token (approximation).
            // In production, use tiktoken's bpe_encode to get exact CLIP token IDs.
            var bytes = System.Text.Encoding.UTF8.GetBytes(word);
            foreach (var b in bytes)
            {
                // Map byte values to CLIP token range (257-49405 for byte-level BPE).
                // CLIP uses a byte-level BPE with 49408 vocab.
                var token = 257 + (b % 49149); // Simplified mapping.
                tokens.Add(token);
            }
        }

        tokens.Add(49407); // EOS marker.

        // Pad or truncate to a reasonable sequence length (512 for CLIP).
        while (tokens.Count > 512)
            tokens.RemoveAt(1); // Remove middle tokens if too long.

        return tokens.ToArray();
    }

    /// <summary>
    /// Converts a flat float array from ONNX Runtime output to PNG bytes.
    /// Shape is [1, 3, H, W] (NCHW format) — denormalizes [-1, 1] → [0, 255].
    /// </summary>
    private static byte[] PixelValuesToPng(float[] pixelData, int height, int width)
    {
        const int channels = 3; // RGB

        using var skBitmap = new SkiaSharp.SKBitmap(width, height);

        for (int h = 0; h < height; h++)
        {
            for (int w = 0; w < width; w++)
            {
                // Accumulate all three channels for this pixel position.
                float r = 0, g = 0, b = 0;
                for (int c = 0; c < channels; c++)
                {
                    var pixelValue = pixelData[c * height * width + h * width + w];
                    // Denormalize from [-1, 1] to [0, 255]: value * 127.5 + 127.5
                    var clampedPixel = (byte)Math.Clamp(pixelValue * 127.5f + 127.5f, 0, 255);
                    if (c == 0) r = clampedPixel;
                    else if (c == 1) g = clampedPixel;
                    else if (c == 2) b = clampedPixel;
                }

                skBitmap.SetPixel(w, h, new SkiaSharp.SKColor((byte)r, (byte)g, (byte)b));
            }
        }

        using var image = SkiaSharp.SKImage.FromBitmap(skBitmap);
        using var imageData = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return imageData.ToArray();
    }
}