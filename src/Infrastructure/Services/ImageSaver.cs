using System.Text.Json;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Types;
using SkiaSharp;

namespace OpenLMStudio.Infrastructure.Services;

// Resolve ambiguous ImageOutputFormat reference
using ImageOutputFormat = OpenLMStudio.Application.Types.ImageOutputFormat;
using ImageGenerationMetadata = OpenLMStudio.Application.Types.ImageGenerationMetadata;

/// <summary>
/// Image saver with disk output, metadata sidecars, and gallery support.
/// </summary>
public class ImageSaver : IImageSaver
{
    private readonly string _outputDirectory;
    private readonly string _galleryDirectory;
    private readonly ILogger<ImageSaver>? _logger;

    public ImageSaver(ILogger<ImageSaver>? logger = null)
    {
        _logger = logger;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _outputDirectory = Path.Combine(appData, "Pictures", "OpenLMStudio");
        _galleryDirectory = Path.Combine(_outputDirectory, "Gallery");
        Directory.CreateDirectory(_outputDirectory);
        Directory.CreateDirectory(_galleryDirectory);
    }

    public string DefaultOutputDirectory => _outputDirectory;

    public string GenerateTimestampedFilename(string extension = "png")
    {
        var now = DateTime.Now;
        var name = $"IMG_{now:yyyyMMdd_HHmmss}.{extension}";
        return name;
    }

    public async Task<string> SaveToDiskAsync(byte[] imageBytes, string? outputPath = null, OpenLMStudio.Application.Types.ImageOutputFormat format = OpenLMStudio.Application.Types.ImageOutputFormat.Png, CancellationToken ct = default)
    {
        var filename = outputPath ?? Path.Combine(_outputDirectory, GenerateTimestampedFilename(GetExtension(format)));
        Directory.CreateDirectory(Path.GetDirectoryName(filename)!);
        await File.WriteAllBytesAsync(filename, imageBytes, ct);
        _logger?.LogInformation("Saved image to {Path}", filename);
        return filename;
    }

    public async Task<string> SaveWithMetadataAsync(byte[] imageBytes, OpenLMStudio.Application.Types.ImageGenerationMetadata metadata, string? outputPath = null, OpenLMStudio.Application.Types.ImageOutputFormat format = OpenLMStudio.Application.Types.ImageOutputFormat.Png, CancellationToken ct = default)
    {
        var imagePath = await SaveToDiskAsync(imageBytes, outputPath, format, ct);
        var metaPath = Path.ChangeExtension(imagePath, ".json");
        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(metaPath, json, ct);
        _logger?.LogInformation("Saved metadata sidecar to {Path}", metaPath);
        return imagePath;
    }

    public async Task<ImageGalleryEntry> SaveToGalleryAsync(byte[] imageBytes, OpenLMStudio.Application.Types.ImageGenerationMetadata metadata, CancellationToken ct = default)
    {
        var filename = GenerateTimestampedFilename("png");
        var imagePath = Path.Combine(_galleryDirectory, filename);
        var thumbPath = Path.Combine(_galleryDirectory, Path.GetFileNameWithoutExtension(filename) + "_thumb.png");

        // Generate thumbnail (128x128)
        using var thumbBitmap = SKBitmap.Decode(new MemoryStream(imageBytes))!;
        var thumbWidth = 128;
        var thumbHeight = 128;
        using var scaledBitmap = thumbBitmap.Resize(new SKSizeI(thumbWidth, thumbHeight), SKSamplingOptions.Default);
        using var thumbImage = SKImage.FromBitmap(scaledBitmap);
        using var thumbData = thumbImage.Encode(SKEncodedImageFormat.Png, 80);
        await File.WriteAllBytesAsync(thumbPath, thumbData.ToArray(), ct);

        // Save full image
        await File.WriteAllBytesAsync(imagePath, imageBytes, ct);

        var entry = new ImageGalleryEntry(
            Id: Guid.NewGuid().ToString(),
            Prompt: metadata.Prompt,
            ModelId: metadata.ModelId,
            Width: metadata.Width,
            Height: metadata.Height,
            Seed: metadata.Seed,
            CfgScale: metadata.CfgScale,
            Steps: metadata.Steps,
            SamplerType: metadata.SamplerType,
            FilePath: imagePath,
            ThumbnailPath: thumbPath,
            Timestamp: DateTime.Now,
            NegativePrompt: metadata.NegativePrompt,
            LoRAAdapters: metadata.LoRAAdapters);

        _logger?.LogInformation("Saved to gallery: {Path}", imagePath);
        return entry;
    }

    public Task<string> GetGalleryDirectoryAsync() => Task.FromResult(_galleryDirectory);

    public void Dispose() { }

    private static string GetExtension(OpenLMStudio.Application.Types.ImageOutputFormat format) => format switch
    {
        OpenLMStudio.Application.Types.ImageOutputFormat.Png => "png",
        OpenLMStudio.Application.Types.ImageOutputFormat.Jpeg => "jpg",
        OpenLMStudio.Application.Types.ImageOutputFormat.WebP => "webp",
        OpenLMStudio.Application.Types.ImageOutputFormat.Ico => "ico",
        OpenLMStudio.Application.Types.ImageOutputFormat.Bmp => "bmp",
        OpenLMStudio.Application.Types.ImageOutputFormat.Gif => "gif",
        _ => "png",
    };
}