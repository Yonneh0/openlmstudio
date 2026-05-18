namespace OpenLMStudio.Application.Types;

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