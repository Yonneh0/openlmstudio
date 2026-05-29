using OpenLMStudio.Application.Types;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Interface for saving generated images to disk with metadata and gallery support.
/// </summary>
public interface IImageSaver : IDisposable
{
    /// <summary>Default output directory: ~/Pictures/OpenLMStudio/</summary>
    string DefaultOutputDirectory { get; }

    /// <summary>
    /// Saves an image to disk with a timestamped filename.
    /// </summary>
    Task<string> SaveToDiskAsync(byte[] imageBytes, string? outputPath = null, ImageOutputFormat format = ImageOutputFormat.Png, CancellationToken ct = default);

    /// <summary>
    /// Saves an image alongside a JSON sidecar with generation parameters.
    /// </summary>
    Task<string> SaveWithMetadataAsync(byte[] imageBytes, ImageGenerationMetadata metadata, string? outputPath = null, ImageOutputFormat format = ImageOutputFormat.Png, CancellationToken ct = default);

    /// <summary>
    /// Saves an image to the gallery directory with thumbnail generation.
    /// </summary>
    Task<ImageGalleryEntry> SaveToGalleryAsync(byte[] imageBytes, ImageGenerationMetadata metadata, CancellationToken ct = default);

    /// <summary>
    /// Generates a timestamped filename: IMG_20260528_143022.png
    /// </summary>
    string GenerateTimestampedFilename(string extension = "png");

    /// <summary>
    /// Gets the gallery directory path.
    /// </summary>
    Task<string> GetGalleryDirectoryAsync();
}
