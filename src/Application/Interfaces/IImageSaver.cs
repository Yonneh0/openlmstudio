using OpenLMStudio.Application.Types;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Service for saving generated images to disk with metadata.
/// </summary>
public interface IImageSaver : IDisposable
{
    /// <summary>
    /// Saves image bytes to disk as the specified format.
    /// </summary>
    Task<string> SaveToDiskAsync(byte[] imageBytes, string filePath, ImageOutputFormat format);

    /// <summary>
    /// Saves image with a JSON sidecar containing generation metadata.
    /// </summary>
    Task SaveWithMetadataAsync(byte[] imageBytes, string filePath, ImageOutputFormat format, ImageGenerationMetadata metadata);

    /// <summary>
    /// Generates a timestamped filename like IMG_20260529_020741.png.
    /// </summary>
    string GenerateTimestampedFilename(string extension = "png");

    /// <summary>
    /// Generates a thumbnail (128x128) from the image bytes.
    /// </summary>
    Task<byte[]> GenerateThumbnailAsync(byte[] imageBytes, int size = 128);
}