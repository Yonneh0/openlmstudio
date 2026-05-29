using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Application.Types;

// ImageOutputFormat is defined in Application.Types

/// <summary>
/// Request for image-to-image generation: encodes an input image, applies noise, and runs denoising.
/// </summary>
public record ImageToImageRequest(
    string ModelId,
    string Prompt,
    string? NegativePrompt = null,
    byte[] InputImage = null!,
    double DenoiseStrength = 0.75,
    int Width = 1024,
    int Height = 1024,
    double GuidanceScale = 7.5,
    int Steps = 30,
    long Seed = -1,
    List<LoraAdapterReference>? LoraAdapters = null,
    ImageSamplerType SamplerType = ImageSamplerType.Euler);

/// <summary>
/// Request for image variation (img2img with no prompt change).
/// </summary>
public record ImageVariationRequest(
    string ModelId,
    byte[] InputImage,
    double DenoiseStrength = 0.35,
    int Width = 1024,
    int Height = 1024,
    double GuidanceScale = 7.5,
    int Steps = 30,
    long Seed = -1,
    ImageSamplerType SamplerType = ImageSamplerType.Euler);

/// <summary>
/// Result of an image-to-image or image variation operation.
/// </summary>
public record ImageToImageResult(
    byte[] ImageBytes,
    int Width,
    int Height,
    long Seed,
    double GuidanceScale,
    int Steps,
    string ModelId,
    string InputImageId = "")
{
    public string DataUri => $"data:image/png;base64,{Convert.ToBase64String(ImageBytes)}";
    public string MimeType { get; init; } = "image/png";
}

/// <summary>
/// Request for inpainting (replacing masked region with new content).
/// </summary>
public record InpaintRequest(
    string ModelId,
    string Prompt,
    string? NegativePrompt = null,
    byte[] InitImage = null!,
    byte[] MaskImage = null!,
    int Width = 0,
    int Height = 0,
    double GuidanceScale = 7.5,
    int Steps = 30,
    long Seed = -1,
    List<LoraAdapterReference>? LoraAdapters = null,
    ImageSamplerType SamplerType = ImageSamplerType.Euler);

/// <summary>
/// Request for outpainting (extending image beyond original boundaries).
/// </summary>
public record OutpaintRequest(
    string ModelId,
    string Prompt,
    string? NegativePrompt = null,
    byte[] InitImage = null!,
    string Direction = "right",
    int Width = 1024,
    int Height = 1024,
    double GuidanceScale = 7.5,
    int Steps = 30,
    long Seed = -1,
    List<LoraAdapterReference>? LoraAdapters = null,
    ImageSamplerType SamplerType = ImageSamplerType.Euler);

/// <summary>
/// Metadata for a generated image (used by ImageSaver).
/// </summary>
public record ImageGenerationMetadata(
    string Prompt,
    string? NegativePrompt = null,
    string ModelId = "",
    int Width = 1024,
    int Height = 1024,
    long Seed = -1,
    double CfgScale = 7.5,
    int Steps = 30,
    string SamplerType = "Euler",
    IReadOnlyList<string>? LoRAAdapters = null)
{
    /// <summary>
    /// Alias for SamplerType to support ImageGalleryEntry's Sampler parameter.
    /// </summary>
    public string Sampler => SamplerType;
};

/// <summary>
/// Entry in the image gallery with metadata and file paths.
/// </summary>
public record ImageGalleryEntry(
    string Id,
    string Prompt,
    string ModelId,
    int Width,
    int Height,
    long Seed,
    double CfgScale,
    int Steps,
    string Sampler,
    string FilePath,
    string ThumbnailPath,
    DateTimeOffset Timestamp);