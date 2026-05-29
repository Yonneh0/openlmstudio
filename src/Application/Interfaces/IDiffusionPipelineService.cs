using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

// Re-export types from Application.Types for convenience
using ImageGenerationRequest = OpenLMStudio.Application.Types.ImageGenerationRequest;
using ImageGenerationResult = OpenLMStudio.Application.Types.ImageGenerationResult;
using ImageGenerationProgress = OpenLMStudio.Application.Types.ImageGenerationProgress;
using ImageOutputFormat = OpenLMStudio.Application.Types.ImageOutputFormat;
using ImageGenerationMetadata = OpenLMStudio.Application.Types.ImageGenerationMetadata;
using ImageInpaintingRequest = OpenLMStudio.Application.Types.ImageInpaintingRequest;
using ImageOutpaintingRequest = OpenLMStudio.Application.Types.ImageOutpaintingRequest;
using MultiModalModelMetadata = OpenLMStudio.Domain.Models.MultiModalModelMetadata;

/// <summary>
/// Represents a LoRA adapter weight tensor delta for runtime application to ONNX Runtime inference.
/// The Weight property is the scaling factor applied: output += Weight * DeltaData.
/// </summary>
public record LoraDeltaTensor(
    string TensorName,
    float[] DeltaData,
    int[] Shape,
    double Weight = 1.0);

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
/// Reference to a LoRA adapter for on-the-fly application during image generation.
/// </summary>
public record LoraAdapterReference(
    string ModelId,
    double Weight = 1.0);

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

/// <summary>
/// Configuration for a specific diffusion model family (SD 1.x, SDXL, SD 3, Flux, etc.).
/// </summary>
public record DiffusionModelFamilyConfig(
    string Name,
    string PipelineType,
    int LatentChannels,
    int DefaultWidth,
    int DefaultHeight,
    int RecommendedStepsMin,
    int RecommendedStepsMax,
    double CfgScaleMin,
    double CfgScaleMax,
    string? SupportedSafetensorsFilePattern,
    IReadOnlyList<ImageSamplerType>? SupportedSamplers = null);

/// <summary>
/// Manages diffusion model families and provides configuration per family.
/// Supports SD 1.x, SDXL, SD 3, Flux, and custom families.
/// </summary>
public interface IDiffusionModelFamilyService : IDisposable
{
    IReadOnlyList<DiffusionModelFamilyConfig> Families { get; }
    DiffusionModelFamilyConfig? GetFamily(string pipelineType);
    DiffusionModelFamilyConfig? GetFamilyByModelId(string modelId);
    void RegisterFamily(DiffusionModelFamilyConfig config);
    void RegisterDefaultFamilies();
}

/// <summary>
/// Interface for the image generation coordinator — SystemAI's primary entry point for image generation.
/// </summary>
public interface IImageGenerationCoordinator : IDisposable
{
    Task<ImageGenerationResult> ExecuteAsync(ImageGenerationCommand command, CancellationToken ct = default);
    IAsyncEnumerable<ImageGenerationProgress> ExecuteStreamingAsync(ImageGenerationCommand command, CancellationToken ct = default);
    ImageGenerationStatus GetStatus();
}

/// <summary>
/// Command record for SystemAI to control image generation.
/// </summary>
public record ImageGenerationCommand(
    string PipelineType,
    string Prompt,
    string? NegativePrompt,
    int Width,
    int Height,
    int Steps,
    double CfgScale,
    int Seed,
    string SamplerType,
    IReadOnlyList<LoraAdapterCommand>? LoRAAdapters,
    ImageToImageCommand? ImageToImage,
    ControlNetCommand? ControlNet,
    InpaintCommand? Inpaint,
    string OutputFormat,
    string? OutputPath);

/// <summary>
/// Command for applying a LoRA adapter.
/// </summary>
public record LoraAdapterCommand(string ModelId, double Weight);

/// <summary>
/// Command for image-to-image input.
/// </summary>
public record ImageToImageCommand(byte[] InputImage, double DenoiseStrength);

/// <summary>
/// Command for ControlNet conditioning.
/// </summary>
public record ControlNetCommand(string ControlType, byte[] ControlImage);

/// <summary>
/// Command for inpainting.
/// </summary>
public record InpaintCommand(byte[] MaskImage, string Prompt);

/// <summary>
/// Current status of an image generation operation.
/// </summary>
public record ImageGenerationStatus(
    bool IsGenerating,
    int CurrentStep,
    int TotalSteps,
    float Percentage,
    string? CurrentModelId,
    string? CurrentPrompt);

