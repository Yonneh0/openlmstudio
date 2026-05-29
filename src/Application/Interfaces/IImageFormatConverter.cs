using OpenLMStudio.Application.Types;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Interface for converting image bytes between formats (PNG, JPEG, WebP, ICO, BMP, GIF).
/// Uses SkiaSharp for cross-platform format support.
/// </summary>
public interface IImageFormatConverter : IDisposable
{
    /// <summary>
    /// Converts image bytes to PNG (lossless, supports transparency).
    /// </summary>
    Task<byte[]> ToPngAsync(byte[] imageBytes);

    /// <summary>
    /// Converts image bytes to JPEG with the specified quality (1-100, where 100 is best).
    /// </summary>
    Task<byte[]> ToJpegAsync(byte[] imageBytes, int quality = 90);

    /// <summary>
    /// Converts image bytes to WebP with the specified quality (1-100).
    /// </summary>
    Task<byte[]> ToWebPAsync(byte[] imageBytes, int quality = 90);

    /// <summary>
    /// Converts image bytes to ICO (Windows icon) with the specified sizes.
    /// </summary>
    Task<byte[]> ToIcoAsync(byte[] imageBytes, int[]? sizes = null);

    /// <summary>
    /// Converts image bytes to BMP (uncompressed bitmap).
    /// </summary>
    Task<byte[]> ToBmpAsync(byte[] imageBytes);

    /// <summary>
    /// Converts image bytes to GIF with the specified delay between frames (in milliseconds).
    /// </summary>
    Task<byte[]> ToGifAsync(IReadOnlyList<byte[]> frames, int delayMs = 100);

    /// <summary>
    /// Converts image bytes to the specified output format.
    /// </summary>
    Task<byte[]> ConvertAsync(byte[] imageBytes, ImageOutputFormat format, int quality = 90, int[]? icoSizes = null);
}
