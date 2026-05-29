using System.Text.Json;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Types;
using SkiaSharp;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Default implementation of IImageSaver — saves images with metadata sidecars.
/// </summary>
public class ImageSaver : IImageSaver
{
    private readonly string _defaultDirectory;

    public ImageSaver(string? defaultDirectory = null)
    {
        _defaultDirectory = defaultDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Pictures", "OpenLMStudio");
    }

    public async Task<string> SaveToDiskAsync(byte[] imageBytes, string filePath, ImageOutputFormat format)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var ext = format switch
        {
            ImageOutputFormat.Png => ".png",
            ImageOutputFormat.Jpeg => ".jpg",
            ImageOutputFormat.WebP => ".webp",
            ImageOutputFormat.Ico => ".ico",
            ImageOutputFormat.Bmp => ".bmp",
            ImageOutputFormat.Gif => ".gif",
            _ => ".png",
        };

        var fullPath = filePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)
            ? filePath
            : filePath + ext;

        await File.WriteAllBytesAsync(fullPath, imageBytes);
        return fullPath;
    }

    public async Task SaveWithMetadataAsync(byte[] imageBytes, string filePath, ImageOutputFormat format, ImageGenerationMetadata metadata)
    {
        var fullPath = Path.Combine(_defaultDirectory, $"{Path.GetFileNameWithoutExtension(filePath)}_{Guid.NewGuid():N}.{format.ToString().ToLowerInvariant()}");
        await SaveToDiskAsync(imageBytes, fullPath, format);

        // Save JSON sidecar
        var jsonPath = Path.ChangeExtension(fullPath, ".json");
        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(jsonPath, json);
    }

    public async Task<ImageGalleryEntry> SaveToGalleryAsync(byte[] imageBytes, ImageGenerationMetadata metadata, CancellationToken ct = default)
    {
        var thumbnail = await GenerateThumbnailAsync(imageBytes);
        var path = GenerateTimestampedFilename("png");
        await SaveToDiskAsync(imageBytes, path, ImageOutputFormat.Png);
        var thumbPath = GenerateTimestampedFilename("png");
        await File.WriteAllBytesAsync(thumbPath, thumbnail, ct);

        var entry = new ImageGalleryEntry(
            Id: Guid.NewGuid().ToString(),
            Prompt: metadata.Prompt,
            ModelId: metadata.ModelId,
            Width: metadata.Width,
            Height: metadata.Height,
            Seed: metadata.Seed,
            CfgScale: metadata.CfgScale,
            Steps: metadata.Steps,
            Sampler: metadata.Sampler,
            FilePath: path,
            ThumbnailPath: thumbPath,
            Timestamp: DateTimeOffset.UtcNow
        );

        return entry;
    }

    public string GenerateTimestampedFilename(string extension = "png")
    {
        var now = DateTime.UtcNow;
        var filename = $"IMG_{now:yyyyMMdd_HHmmss}.{extension}";
        return Path.Combine(_defaultDirectory, filename);
    }

    public async Task<byte[]> GenerateThumbnailAsync(byte[] imageBytes, int size = 128)
    {
        using var bitmap = SKBitmap.Decode(new MemoryStream(imageBytes));
        using var thumbnail = bitmap.Resize(new SKSizeI(size, size), SKSamplingOptions.Default);
        using var img = SKImage.FromBitmap(thumbnail);
        var encoded = img.Encode(SKEncodedImageFormat.Png, 80);
        return encoded!.ToArray();
    }

    public void Dispose() { }
}