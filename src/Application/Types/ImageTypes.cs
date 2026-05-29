using OpenLMStudio.Application.Interfaces;
using System.Runtime.CompilerServices;

namespace OpenLMStudio.Application.Types;

/// <summary>
/// Supported output image formats for image generation.
/// </summary>
public enum ImageOutputFormat
{
    Png,
    Jpeg,
    WebP,
    Ico,
    Bmp,
    Gif,
}

/// <summary>
/// A request for image generation (used by DiffusionPipelineService).
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
    bool StreamProgress = false,
    OpenLMStudio.Application.Interfaces.ImageSamplerType SamplerType = OpenLMStudio.Application.Interfaces.ImageSamplerType.Euler,
    List<OpenLMStudio.Application.Interfaces.LoraAdapterReference>? LoraAdapters = null)
{
    public long EffectiveSeed => Seed == -1 ? (long)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % int.MaxValue) : Seed;
}

/// <summary>
/// Result of a single image generation request.
/// </summary>
public record ImageGenerationResult(
    byte[] ImageBytes,
    int Width,
    int Height,
    long Seed,
    double GuidanceScale,
    int Steps,
    string ModelId,
    string Prompt = "",
    string? NegativePrompt = null,
    IReadOnlyList<LoraAdapterReference>? LoraAdapters = null)
{
    public string DataUri => $"data:image/png;base64,{Convert.ToBase64String(ImageBytes)}";
    public string MimeType { get; init; } = "image/png";
    public string SamplerType { get; init; } = "Euler";
}

/// <summary>
/// Represents progress during an image generation operation.
/// </summary>
public record ImageGenerationProgress(
    int Step,
    int TotalSteps,
    float Percentage)
{
    public float ProgressPercent => TotalSteps > 0 ? (Step / (float)TotalSteps) * 100 : 0;
    public byte[]? ImageBytes { get; init; }
}

/// <summary>
/// Result of a batch image generation operation.
/// </summary>
public record ImageBatchResult(
    IReadOnlyList<ImageGenerationResult> Results,
    byte[]? GridImage,
    int Succeeded,
    int Failed);

/// <summary>
/// Request to the image generation endpoint via OpenAI-compatible API format.
/// </summary>
public class OpenAIImageGenerationRequest
{
    /// <summary>
    /// The model identifier for image generation (diffusion model).
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Text prompt describing the desired image.
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Negative text prompt to guide what should NOT appear in the output.
    /// </summary>
    public string? NegativePrompt { get; set; }

    /// <summary>
    /// Image resolution width (e.g., 512, 768, 1024).
    /// </summary>
    public int Width { get; set; } = 1024;

    /// <summary>
    /// Image resolution height (e.g., 512, 768, 1024).
    /// </summary>
    public int Height { get; set; } = 1024;

    /// <summary>
    /// Number of diffusion steps. Higher values produce more detailed but slower results.
    /// </summary>
    public int Steps { get; set; } = 30;

    /// <summary>
    /// Classifier-free guidance scale. Higher values follow the prompt more strictly.
    /// </summary>
    public float CfgScale { get; set; } = 7.5f;

    /// <summary>
    /// Random seed for reproducibility.
    /// </summary>
    public long? Seed { get; set; }

    /// <summary>
    /// Array of messages forming the conversation context (for chat-based image generation).
    /// </summary>
    public List<ChatMessage>? Messages { get; set; }

    /// <summary>
    /// Number of images to generate in parallel.
    /// </summary>
    public int NumImages { get; set; } = 1;

    /// <summary>
    /// Response format for the generated image (e.g., "base64png", "url").
    /// </summary>
    public string? ResponseFormat { get; set; }

    /// <summary>
    /// Model version for this generation.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Quality parameter for the image (e.g., "standard", "hd").
    /// </summary>
    public string? Quality { get; set; }

    /// <summary>
    /// Style of the generated image.
    /// </summary>
    public string? Style { get; set; }
}

/// <summary>
/// Represents a generated image from the /v1/images/generations endpoint.
/// </summary>
public class ImageGenerationResponse
{
    /// <summary>
    /// Unique identifier for this generation request.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The model used for this generation.
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the response was generated.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// List of generated images with their metadata.
    /// </summary>
    public IList<ImageData> Data { get; set; } = new List<ImageData>();
}

/// <summary>
/// Represents a single generated image with its associated metadata.
/// </summary>
public class ImageData
{
    /// <summary>
    /// Base64-encoded PNG image data (data URI format).
    /// </summary>
    public string B64Json { get; set; } = string.Empty;

    /// <summary>
    /// Width of the generated image in pixels.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Height of the generated image in pixels.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// The random seed used for this generation.
    /// </summary>
    public long? Seed { get; set; }

    /// <summary>
    /// Model identifier that produced this image.
    /// </summary>
    public string ModelId { get; set; } = string.Empty;
}

/// <summary>
/// Request to the image inpainting endpoint — replaces regions inside an existing image with new content.
/// </summary>
public class ImageInpaintingRequest
{
    /// <summary>
    /// The model identifier for image generation (diffusion model).
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Text prompt describing the desired output.
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Negative text prompt to guide what should NOT appear in the output.
    /// </summary>
    public string? NegativePrompt { get; set; }

    /// <summary>
    /// Base64-encoded image data (data URI format: "data:image/png;base64,...").
    /// The original image for inpainting.
    /// </summary>
    public string InitImage { get; set; } = string.Empty;

    /// <summary>
    /// Base64-encoded mask image — white pixels indicate regions to be inpainted, black pixels are preserved.
    /// </summary>
    public string MaskImage { get; set; } = string.Empty;

    /// <summary>
    /// Image resolution width (e.g., 512, 768, 1024). Defaults to the init image's width if not specified.
    /// </summary>
    public int Width { get; set; } = 0; // 0 means use original image dimensions

    /// <summary>
    /// Image resolution height (e.g., 512, 768, 1024). Defaults to the init image's height if not specified.
    /// </summary>
    public int Height { get; set; } = 0; // 0 means use original image dimensions

    /// <summary>
    /// Number of diffusion steps. Higher values produce more detailed but slower results.
    /// </summary>
    public int Steps { get; set; } = 30;

    /// <summary>
    /// Classifier-free guidance scale. Higher values follow the prompt more strictly.
    /// </summary>
    public float CfgScale { get; set; } = 7.5f;

    /// <summary>
    /// Random seed for reproducibility.
    /// </summary>
    public long? Seed { get; set; }

    /// <summary>
    /// Number of images to generate in parallel.
    /// </summary>
    public int NumImages { get; set; } = 1;

    /// <summary>
    /// How much the denoising process is reduced relative to the original image (0 = full inpainting, 1 = no inpainting).
    /// </summary>
    public float? Strength { get; set; } = null; // Default: auto-detected from init image

    /// <summary>
    /// Response format for the generated image (e.g., "base64png", "url").
    /// </summary>
    public string? ResponseFormat { get; set; }

    /// <summary>
    /// Model version for this generation.
    /// </summary>
    public string? Version { get; set; }
}

/// <summary>
/// Request to the image outpainting endpoint — extends an existing image beyond its original boundaries.
/// </summary>
public class ImageOutpaintingRequest
{
    /// <summary>
    /// The model identifier for image generation (diffusion model).
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Text prompt describing the desired output.
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Negative text prompt to guide what should NOT appear in the output.
    /// </summary>
    public string? NegativePrompt { get; set; }

    /// <summary>
    /// Base64-encoded image data (data URI format: "data:image/png;base64,...").
    /// The original image for outpainting.
    /// </summary>
    public string InitImage { get; set; } = string.Empty;

    /// <summary>
    /// Image resolution width of the output — must be larger than the init image width.
    /// </summary>
    public int Width { get; set; } = 1024; // Must be > init image width

    /// <summary>
    /// Image resolution height of the output — must be larger than the init image height.
    /// </summary>
    public int Height { get; set; } = 1024; // Must be > init image height

    /// <summary>
    /// Number of diffusion steps. Higher values produce more detailed but slower results.
    /// </summary>
    public int Steps { get; set; } = 30;

    /// <summary>
    /// Classifier-free guidance scale. Higher values follow the prompt more strictly.
    /// </summary>
    public float CfgScale { get; set; } = 7.5f;

    /// <summary>
    /// Random seed for reproducibility.
    /// </summary>
    public long? Seed { get; set; }

    /// <summary>
    /// Number of images to generate in parallel.
    /// </summary>
    public int NumImages { get; set; } = 1;

    /// <summary>
    /// Direction(s) to extend the image: "left", "right", "top", "bottom".
    /// If not specified, defaults to all directions equally.
    /// </summary>
    public string? Direction { get; set; } = null; // e.g., "right" or "up"

    /// <summary>
    /// How much the denoising process is reduced relative to the original image (0 = full outpainting, 1 = no change).
    /// </summary>
    public float? Strength { get; set; } = null; // Default: auto-detected from init image

    /// <summary>
    /// Response format for the generated image (e.g., "base64png", "url").
    /// </summary>
    public string? ResponseFormat { get; set; }

    /// <summary>
    /// Model version for this generation.
    /// </summary>
    public string? Version { get; set; }
}