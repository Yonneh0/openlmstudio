using System;
using System.IO;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Types;
using SkiaSharp;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Default implementation of IImageSaver — saves images to disk with metadata.
/// </summary>
public class ImageSaver : IImageSaver
{
    private readonly ILogger<ImageSaver>? _logger;
    private readonly string _defaultDirectory;

    public ImageSaver(ILogger<ImageSaver>? logger = null)
    {
        _logger = logger;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _defaultDirectory = Path.Combine(home, "Pictures", "OpenLMStudio");
        Directory.CreateDirectory(_defaultDirectory);
    }

    public string DefaultSaveDirectory => _defaultDirectory;

    public string ExpandPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return _defaultDirectory;
        if (path.StartsWith("~/", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, path.Substring(2));
        }
        return path;
    }

    public string GenerateTimestampedFilename()
    {
        var now = DateTime.Now;
        var ext = ".png";
        return $"IMG_{now:yyyyMMdd_HHmmss}{ext}";
    }

    public async Task<string> SaveToDiskAsync(byte[] imageBytes, string filePath, ImageOutputFormat format, ImageGenerationMetadata? metadata = null)
    {
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllBytesAsync(filePath, imageBytes);

            // Save metadata sidecar
            if (metadata != null)
            {
                var metaPath = Path.ChangeExtension(filePath, ".json");
                var json = System.Text.Json.JsonSerializer.Serialize(metadata, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(metaPath, json);
                _logger?.LogDebug("Saved metadata to {MetaPath}", metaPath);
            }

            return filePath;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save image to {FilePath}", filePath);
            throw;
        }
    }

    public async Task<(string imagePath, string metadataPath)> SaveImageWithMetadataAsync(
        byte[] imageBytes, string directory, string baseFilename, ImageGenerationMetadata metadata, ImageOutputFormat format)
    {
        var filePath = Path.Combine(directory, baseFilename);
        var metaPath = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(baseFilename)}.json");

        await SaveToDiskAsync(imageBytes, filePath, format, metadata);
        await File.WriteAllTextAsync(metaPath, System.Text.Json.JsonSerializer.Serialize(metadata, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

        return (filePath, metaPath);
    }

    public async Task<byte[]> GenerateThumbnailAsync(byte[] imageBytes, int size = 128)
    {
        using var bitmap = SKBitmap.Decode(new MemoryStream(imageBytes));
        if (bitmap == null)
            return imageBytes;

        using var resized = bitmap.Resize(new SKSizeI(size, size), SKSamplingOptions.Default);
        using var thumbImage = SKImage.FromBitmap(resized);
        var encoded = thumbImage.Encode(SKEncodedImageFormat.Png, 80);
        return encoded != null ? encoded.ToArray() : imageBytes;
    }

    /// <summary>
    /// Saves an image to the gallery with thumbnail generation.
    /// </summary>
    public async Task<ImageGalleryEntry> SaveToGalleryAsync(
        byte[] imageBytes, ImageGenerationMetadata metadata, CancellationToken ct = default)
    {
        var thumbnail = await GenerateThumbnailAsync(imageBytes);
        var thumbFilename = $"{Path.GetFileNameWithoutExtension(GenerateTimestampedFilename())}_thumb.png";
        var thumbPath = Path.Combine(_defaultDirectory, thumbFilename);
        await File.WriteAllBytesAsync(thumbPath, thumbnail, ct);

        var filename = GenerateTimestampedFilename();
        var filePath = Path.Combine(_defaultDirectory, filename);
        await File.WriteAllBytesAsync(filePath, imageBytes, ct);

        return new ImageGalleryEntry(
            Id: Guid.NewGuid().ToString(),
            Prompt: metadata.Prompt,
            ModelId: metadata.ModelId,
            Width: metadata.Width,
            Height: metadata.Height,
            Seed: metadata.Seed,
            CfgScale: metadata.CfgScale,
            Steps: metadata.Steps,
            Sampler: metadata.Sampler,
            FilePath: filePath,
            ThumbnailPath: thumbPath,
            Timestamp: DateTime.UtcNow);
    }

    public void Dispose() { }
}
