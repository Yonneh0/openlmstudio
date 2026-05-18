using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Defines a strategy for compressing context within a conversation.
/// </summary>
public interface IContextCompressor : IDisposable
{
    /// <summary>
    /// Compresses the given segments according to the specified compression level.
    /// </summary>
    Task<CompressionResult> CompressAsync(IEnumerable<ContextSegment> segments, CompressionLevel level);

    /// <summary>
    /// Decompresses a previously compressed segment back to its original form (where possible).
    /// </summary>
    Task<string?> DecompressAsync(ContextSegment compressedSegment);
}

/// <summary>
/// Result of a context compression operation.
/// </summary>
public record CompressionResult(
    List<ContextSegment> CompressedSegments,
    long OriginalTokenCount,
    long CompressedTokenCount,
    double CompressionRatio)  // 0 = no compression, 1 = fully compressed
{
    public int TokensSaved => (int)Math.Max(0, OriginalTokenCount - CompressedTokenCount);

    public static CompressionResult CreateEmpty() => new([], 0, 0, 0);
}