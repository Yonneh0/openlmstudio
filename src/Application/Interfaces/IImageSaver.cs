using OpenLMStudio.Application.Types;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Service for saving generated images to disk with metadata.
/// </summary>
public interface IImageSaver : IDisposable
{
    /// <summary>
    /// Saves an image to the specified path with the given output format.
    /// </summary>
    Task<string> SaveToDiskAsync(byte[] imageBytes, string filePath, ImageOutputFormat format, ImageGenerationMetadata? metadata = null);

    /// <summary>
    /// Saves an image with a JSON sidecar containing generation metadata.
    /// </summary>
    Task<(string imagePath, string metadataPath)> SaveImageWithMetadataAsync(byte[] imageBytes, string directory, string baseFilename, ImageGenerationMetadata metadata, ImageOutputFormat format);

    /// <summary>
    /// Generates a timestamped filename (e.g., IMG_20260529_143022.png).
    /// </summary>
    string GenerateTimestampedFilename();

    /// <summary>
    /// Generates a thumbnail (128x128) from the source image.
    /// </summary>
    Task<byte[]> GenerateThumbnailAsync(byte[] imageBytes, int size = 128);

    /// <summary>
    /// Expands a path (resolves ~/ to user profile).
    /// </summary>
    string ExpandPath(string path);

    /// <summary>
    /// Gets the default save directory.
    /// </summary>
    string DefaultSaveDirectory { get; }
}