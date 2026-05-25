namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Represents an image generation output from a diffusion pipeline.
/// Stores the generated image as base64-encoded PNG along with generation metadata.
/// </summary>
public class ImageOutput
{
    /// <summary>
    /// Unique identifier for this image output.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Base64-encoded PNG image data.
    /// </summary>
    public string ImageData { get; set; } = string.Empty;

    /// <summary>
    /// MIME type of the image (e.g., "image/png", "image/jpeg").
    /// </summary>
    public string MimeType { get; set; } = "image/png";

    /// <summary>
    /// Width of the generated image in pixels.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Height of the generated image in pixels.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Random seed used for generation (for reproducibility).
    /// </summary>
    public long Seed { get; set; }

    /// <summary>
    /// CFG scale used during generation (classifier-free guidance weight).
    /// </summary>
    public double CfgScale { get; set; } = 7.5;

    /// <summary>
    /// Number of denoising steps used during generation.
    /// </summary>
    public int Steps { get; set; } = 30;

    /// <summary>
    /// Model ID used to generate this image.
    /// </summary>
    public string? ModelId { get; set; }

    /// <summary>
    /// Timestamp when this image was generated.
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The prompt used to generate this image.
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Negative prompt used during generation.
    /// </summary>
    public string? NegativePrompt { get; set; }
}