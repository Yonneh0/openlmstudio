using OpenLMStudio.Application.Types;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Service for image-to-image generation: encodes an input image, applies noise, and runs denoising.
/// </summary>
public interface IImageToImageService : IDisposable
{
    /// <summary>
    /// Encodes an input image via VAE, applies noise at the specified denoise strength,
    /// and runs the denoising loop with the given prompt.
    /// </summary>
    Task<ImageToImageResult> EncodeAndDenoiseAsync(ImageToImageRequest request, CancellationToken ct = default);

    /// <summary>
    /// Generates a variation of an input image with controlled denoise strength.
    /// </summary>
    Task<ImageToImageResult> ImageVariationAsync(ImageVariationRequest request, CancellationToken ct = default);

    /// <summary>
    /// Performs inpainting: replaces the masked region with new content based on the prompt.
    /// </summary>
    Task<ImageToImageResult> InpaintAsync(InpaintRequest request, CancellationToken ct = default);

    /// <summary>
    /// Performs outpainting: extends the image beyond its original boundaries.
    /// </summary>
    Task<ImageToImageResult> OutpaintAsync(OutpaintRequest request, CancellationToken ct = default);
}