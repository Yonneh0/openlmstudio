using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OpenLMStudio.Application.Types;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Defines the sampling algorithm used for diffusion image generation.
/// Each sampler has different noise scheduling and convergence properties.
/// </summary>
public enum ImageSamplerType
{
    /// <summary>Euler — standard first-order ODE solver with linear timestep schedule.</summary>
    Euler,

    /// <summary>Euler a (ancestral) — adds noise between steps for smoother results.</summary>
    EulerA,

    /// <summary>DPM++ (multi-step) — higher-accuracy multi-step denoising with improved convergence.</summary>
    DPMS,

    /// <summary>LMS (linear multistep) — fixed sigma schedule for stable denoising.</summary>
    LMS,
}

/// <summary>
/// Represents a request to generate an image using a diffusion model.
/// </summary>
public record ImageGenerationRequest(
    string ModelId,
    string Prompt,
    string? NegativePrompt = null,
    int Width = 1024,
    int Height = 1024,
    double GuidanceScale = 7.5,
    int Steps = 30,
    long Seed = -1,
    List<LoraAdapterReference>? LoraAdapters = null,
    bool StreamProgress = false,
    ImageSamplerType SamplerType = ImageSamplerType.Euler)
{
    public long EffectiveSeed => Seed == -1 ? (long)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % int.MaxValue) : Seed;
}

/// <summary>
/// Reference to a LoRA adapter for on-the-fly application during image generation.
/// </summary>
public record LoraAdapterReference(
    string ModelId,
    double Weight = 1.0);

/// <summary>
/// Represents progress during an image generation operation.
/// </summary>
public record ImageGenerationProgress(
    int Step,
    int TotalSteps,
    float Percentage)
{
    public float ProgressPercent => TotalSteps > 0 ? (Step / (float)TotalSteps) * 100 : 0;
}

/// <summary>
/// Result of an image generation operation.
/// </summary>
public record ImageGenerationResult(
    byte[] ImageBytes,           // PNG bytes
    int Width,                   // required: output width in pixels
    int Height,                  // required: output height in pixels
    long Seed,                   // required: seed used for generation
    double GuidanceScale,        // required: CFG scale factor
    int Steps,                   // required: number of diffusion steps
    string ModelId)              // required: model ID that generated this result
{
    /// <summary>Convert image bytes to a Base64-encoded data URI.</summary>
    public string DataUri => $"data:image/png;base64,{Convert.ToBase64String(ImageBytes)}";

    /// <summary>MIME type for the generated image (default: PNG).</summary>
    public string MimeType { get; init; } = "image/png";
}

/// <summary>
/// Interface for managing diffusion-based image generation pipelines.
/// </summary>
public interface IDiffusionPipelineService : IDisposable
{
    /// <summary>
    /// Generates an image from a text prompt using the specified model and parameters.
    /// </summary>
    Task<ImageGenerationResult> GenerateImageAsync(ImageGenerationRequest request, CancellationToken ct = default);

    /// <summary>
    /// Streams progress updates during generation (if requested).
    /// </summary>
    IAsyncEnumerable<ImageGenerationProgress> StreamProgressAsync(ImageGenerationRequest request, CancellationToken ct = default);

    /// <summary>
    /// Gets information about available image generation models.
    /// </summary>
    Task<IEnumerable<MultiModalModelMetadata>> GetAvailableModelsAsync();

    /// <summary>
    /// Loads a model into memory for faster subsequent generations.
    /// Returns null if the model is already loaded.
    /// </summary>
    Task<bool> LoadModelAsync(string modelId);

    /// <summary>
    /// Unloads a model from memory to free resources.
    /// </summary>
    Task<bool> UnloadModelAsync(string modelId);

    /// <summary>
    /// Gets all currently loaded image generation models.
    /// </summary>
    Task<IEnumerable<string>> GetLoadedModelsAsync();

    /// <summary>
    /// Generates an inpainted image — replaces regions inside the init image with new content based on the mask.
    /// </summary>
    Task<ImageGenerationResult> GenerateInpaintingAsync(ImageInpaintingRequest request, CancellationToken ct = default);

    /// <summary>
    /// Generates an outpainted image — extends the init image beyond its original boundaries.
    /// </summary>
    Task<ImageGenerationResult> GenerateOutpaintingAsync(ImageOutpaintingRequest request, CancellationToken ct = default);
}

/// <summary>
/// Interface for VAE (Variational Autoencoder) pipeline services — encoding and decoding latent representations.
/// </summary>
public interface IVAEPipelineService : IDisposable
{
    /// <summary>
    /// Encodes an image to its latent representation using the specified VAE model.
    /// Returns bytes of the encoded latent tensor (as a binary file format).
    /// </summary>
    Task<byte[]> EncodeAsync(string vaeModelId, byte[] imageBytes, CancellationToken ct = default);

    /// <summary>
    /// Decodes a latent representation back to an image using the specified VAE model.
    /// Returns PNG bytes of the decoded image.
    /// </summary>
    Task<byte[]> DecodeAsync(string vaeModelId, byte[] latents, CancellationToken ct = default);

    /// <summary>
    /// Gets all available VAE models.
    /// </summary>
    Task<IEnumerable<MultiModalModelMetadata>> GetAvailableModelsAsync();
}

/// <summary>
/// Interface for managing LoRA adapters during image generation.
/// </summary>
public interface ILoraAdapterManager : IDisposable
{
    /// <summary>
    /// Applies a LoRA adapter to the diffusion pipeline at runtime without persisting it into the model weights.
    /// This is the preferred method — avoids merging and keeps models unchanged.
    /// </summary>
    Task ApplyAdapterAsync(string imagePipelineId, LoraAdapterReference reference, CancellationToken ct = default);

    /// <summary>
    /// Merges a LoRA adapter's weights into the base model (persistent). Returns path to merged model or null if failed.
    /// Only use when you need to share a merged model across generations without re-applying on each call.
    /// </summary>
    Task<string?> MergeAdapterAsync(string baseModelId, string loraAdapterId, double scalingFactor = 1.0);

    /// <summary>
    /// Gets all available LoRA adapters for the specified base model type (e.g., SDXL).
    /// </summary>
    Task<IEnumerable<MultiModalModelMetadata>> GetAvailableAdaptersAsync(string? compatibleBaseModel = null);

    /// <summary>
    /// Gets currently applied adapters and their weights for a given pipeline.
    /// </summary>
    Task<IReadOnlyList<LoraAdapterReference>> GetAppliedAdaptersAsync(string imagePipelineId);

    /// <summary>
    /// Removes all applied LoRA adapters from the specified pipeline.
    /// </summary>
    Task RemoveAllAdaptersAsync(string imagePipelineId);
}

/// <summary>
/// Interface for text/image embedding generation pipelines (sentence-transformers, CLIP, etc.).
/// </summary>
public interface IEmbeddingPipelineService : IDisposable
{
    /// <summary>
    /// Generates a text or image embedding vector from the specified input.
    /// Returns normalized float[] vector of dimensionality determined by the model.
    /// </summary>
    Task<float[]> GenerateAsync(string modelId, string inputText, CancellationToken ct = default);

    /// <summary>
    /// Generates embeddings for multiple texts (batched) in one call — more efficient than individual calls.
    /// Returns array of normalized float[] vectors.
    /// </summary>
    Task<float[][]> GenerateBatchAsync(string modelId, IReadOnlyList<string> inputs, CancellationToken ct = default);

    /// <summary>
    /// Gets all available embedding models.
    /// </summary>
    Task<IEnumerable<MultiModalModelMetadata>> GetAvailableModelsAsync();
}