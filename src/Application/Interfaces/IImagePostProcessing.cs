using OpenLMStudio.Application.Types;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Control types for ControlNet preprocessing.
/// </summary>
public enum ControlType
{
    None,
    Canny,
    Depth,
    OpenPose
}

/// <summary>
/// Request for image upscaling via diffusion-based upscaler model.
/// </summary>
public record ImageUpscaleRequest(
    string? ImageId,
    string ImageData,
    string ModelId,
    string UpscaleModelId,
    float ScaleFactor = 2f,
    int Steps = 30,
    double GuidanceScale = 7.5,
    long? Seed = null);

/// <summary>
/// Request for HiRes.fix: two-pass image generation at reduced then full resolution.
/// </summary>
public record ImageHiResFixRequest(
    string? ImageId,
    string Prompt,
    string ModelId,
    int Width,
    int Height,
    int Steps,
    double GuidanceScale,
    long? Seed = null,
    ImageSamplerType SamplerType = ImageSamplerType.Euler,
    string? NegativePrompt = null,
    string? UpscaleModelId = null,
    float ScaleFactor = 2f);

/// <summary>
/// Interface for image post-processing (upscaling, hires.fix, ControlNet preprocessing).
/// </summary>
public interface IImagePostProcessingService : IDisposable
{
    /// <summary>
    /// Performs image-to-image upscaling using a diffusion-based upscaler.
    /// </summary>
    Task<ImageGenerationResult> UpscaleAsync(ImageUpscaleRequest request, CancellationToken ct = default);

    /// <summary>
    /// Performs HiRes.fix: generates at reduced resolution, then upscales to target resolution.
    /// </summary>
    Task<ImageGenerationResult> HiResFixAsync(ImageHiResFixRequest request, CancellationToken ct = default);

    /// <summary>
    /// Preprocesses an image for ControlNet (edge detection, depth estimation, etc.).
    /// </summary>
    Task<byte[]> PreprocessControlAsync(byte[] inputImage, ControlType controlType, int targetWidth, int targetHeight, CancellationToken ct = default);

    /// <summary>
    /// Extracts face embeddings for IP-Adapter face-conditioned generation.
    /// </summary>
    Task<byte[]> ExtractFaceEmbeddingsAsync(byte[] inputImage, CancellationToken ct = default);
}

