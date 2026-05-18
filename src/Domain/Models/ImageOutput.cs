using System.Runtime.Serialization;

namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Defines the type of image generation output (generation, inpainting, or outpainting).
/// </summary>
public enum ImageOutputType
{
    /// <summary>
    /// Standard text-to-image generation.
    /// </summary>
    [EnumMember(Value = "image_generation")]
    TextToImage = 0,

    /// <summary>
    /// Inpainting — replace masked region with new content based on prompt.
    /// </summary>
    [EnumMember(Value = "inpainting")]
    Inpainting = 1,

    /// <summary>
    /// Outpainting — extend image boundaries beyond original.
    /// </summary>
    [EnumMember(Value = "outpainting")]
    Outpainting = 2,
}

/// <summary>
/// Represents a generated or inpainted/outpainted image output within a chat message.
/// Contains the image data (base64), generation parameters, and metadata for UI display.
/// </summary>
public class ImageOutput : IDisposable
{
    /// <summary>
    /// Unique identifier for this image output instance.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    // Note: Image data is stored in the parent Message.Content field as a JSON-serialized representation
    // of this ImageOutput. The DataUri property is provided by the caller (ServerService) when creating
    // the response, not here. This class represents the domain model only.

    /// <summary>
    /// The type of image generation (generation, inpainting, or outpainting).
    /// </summary>
    public ImageOutputType OutputType { get; set; } = ImageOutputType.TextToImage;

    /// <summary>
    /// Text prompt used to generate this image.
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Negative prompt that guided what should NOT appear in the generated output.
    /// </summary>
    public string? NegativePrompt { get; set; }

    /// <summary>
    /// Width of the generated image in pixels.
    /// </summary>
    public int Width { get; set; } = 1024;

    /// <summary>
    /// Height of the generated image in pixels.
    /// </summary>
    public int Height { get; set; } = 1024;

    /// <summary>
    /// Random seed used for generation (reproducible if known).
    /// </summary>
    public long Seed { get; set; } = -1;

    /// <summary>
    /// Classifier-free guidance scale — higher values follow the prompt more strictly.
    /// </summary>
    public double GuidanceScale { get; set; } = 7.5;

    /// <summary>
    /// Number of diffusion steps performed during generation.
    /// </summary>
    public int Steps { get; set; } = 30;

    /// <summary>
    /// The model ID used for generating this image (e.g., "sdxl-v1", "flux-dev").
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the generation was completed.
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public void Dispose() { /* No unmanaged resources */ }
}