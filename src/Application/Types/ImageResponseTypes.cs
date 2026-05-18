namespace OpenLMStudio.Application.Types;

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