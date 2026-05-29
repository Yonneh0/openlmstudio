using OpenLMStudio.Application.Types;
using SkiaSharp;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// SkiaSharp-based image format converter supporting PNG, JPEG, WebP, ICO, BMP, and GIF.
/// </summary>
public class ImageFormatConverter : IImageFormatConverter
{
    private static readonly int[] DefaultIcoSizes = [16, 32, 48, 64, 128, 256];

    public Task<byte[]> ToPngAsync(byte[] imageBytes)
        => Task.FromResult(SKImage.FromEncodedData(imageBytes)!.Encode(SKEncodedImageFormat.Png, 100)!.ToArray());

    public Task<byte[]> ToJpegAsync(byte[] imageBytes, int quality = 90)
        => Task.FromResult(SKImage.FromEncodedData(imageBytes)!.Encode(SKEncodedImageFormat.Jpeg, quality)!.ToArray());

    public Task<byte[]> ToWebPAsync(byte[] imageBytes, int quality = 90)
        => Task.FromResult(SKImage.FromEncodedData(imageBytes)!.Encode(SKEncodedImageFormat.Webp, quality)!.ToArray());

    public async Task<byte[]> ToIcoAsync(byte[] imageBytes, int[]? sizes = null)
    {
        sizes ??= DefaultIcoSizes;
        var frames = new List<byte[]>();
        foreach (var size in sizes)
        {
            using var bitmap = SKBitmap.Decode(new MemoryStream(imageBytes));
            using var resized = bitmap.Resize(new SKSizeI(size, size), SKSamplingOptions.Default);
            using var img = SKImage.FromBitmap(resized);
            frames.Add(img.Encode(SKEncodedImageFormat.Png, 100)!.ToArray());
        }
        return EncodeIco(frames);
    }

    public Task<byte[]> ToBmpAsync(byte[] imageBytes)
        => Task.FromResult(SKImage.FromEncodedData(imageBytes)!.Encode(SKEncodedImageFormat.Bmp, 100)!.ToArray());

    public async Task<byte[]> ToGifAsync(IReadOnlyList<byte[]> frames, int delayMs = 100)
    {
        if (frames.Count == 1)
            return frames[0];

        // Use SkiaSharp's built-in GIF encoding via SKCodec.
        using var result = SKImage.FromEncodedData(frames[0])!;
        var encoded = result.Encode(SKEncodedImageFormat.Gif, 100);
        return encoded!.ToArray();
    }

    public Task<byte[]> ConvertAsync(byte[] imageBytes, ImageOutputFormat format, int quality = 90, int[]? icoSizes = null)
        => format switch
        {
            ImageOutputFormat.Png => ToPngAsync(imageBytes),
            ImageOutputFormat.Jpeg => ToJpegAsync(imageBytes, quality),
            ImageOutputFormat.WebP => ToWebPAsync(imageBytes, quality),
            ImageOutputFormat.Ico => ToIcoAsync(imageBytes, icoSizes),
            ImageOutputFormat.Bmp => ToBmpAsync(imageBytes),
            ImageOutputFormat.Gif => ToGifAsync([imageBytes], 100),
            _ => ToPngAsync(imageBytes),
        };

    public void Dispose() { }

    private static byte[] EncodeIco(IReadOnlyList<byte[]> frames)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // ICO header (22 bytes)
        writer.Write((short)0);       // Reserved
        writer.Write((short)1);       // Type: ICO
        writer.Write((short)frames.Count); // Number of images

        var offset = 22;
        foreach (var frame in frames)
        {
            using var frameImg = SKImage.FromEncodedData(frame)!;
            var frameBitmap = frameImg.Encode(SKEncodedImageFormat.Png, 100)!;
            var width = (byte)(frameBitmap.Length > 16 ? Math.Min((int)(frameBitmap[12] | (frameBitmap[13] << 8)), 255) : 256);
            var height = (byte)(frameBitmap.Length > 18 ? Math.Min((int)(frameBitmap[14] | (frameBitmap[15] << 8)), 255) : 256);
            writer.Write((byte)(width == 0 ? 256 : width));
            writer.Write((byte)(height == 0 ? 256 : height));
            writer.Write((byte)0);      // Reserved
            writer.Write((byte)0);      // Color planes
            writer.Write((short)32);    // Bits per pixel
            writer.Write((int)frame.Length); // Image data size
            writer.Write((int)offset);  // Offset
            offset += frame.Length;
        }

        foreach (var frame in frames)
            stream.Write(frame, 0, frame.Length);

        return stream.ToArray();
    }
}