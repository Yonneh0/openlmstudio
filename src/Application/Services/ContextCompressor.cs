namespace OpenLMStudio.Application.Services;

using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

/// <summary>
/// Compresses context segments according to specified strategies.
/// </summary>
public class ContextCompressor : IContextCompressor
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<ContextCompressor> _logger;
    private readonly ConcurrentDictionary<Guid, List<ContextSegment>> _compressedCache;
    private readonly object _lock = new();

    public ContextCompressor(
        IFileSystem? fileSystem = null,
        ILogger<ContextCompressor>? logger = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
        _logger = logger ?? NullLogger<ContextCompressor>.Instance;
        _compressedCache = new ConcurrentDictionary<Guid, List<ContextSegment>>();
    }

    public void Dispose()
    {
        foreach (var cache in _compressedCache)
        {
            cache.Value.Clear();
        }
        _compressedCache.Clear();
    }

    public async Task<CompressionResult> CompressAsync(IEnumerable<ContextSegment> segments, CompressionLevel level)
    {
        try
        {
            var segmentList = segments.ToList();
            if (!segmentList.Any())
                return CompressionResult.CreateEmpty();

            var originalTokenCount = segmentList.Sum(s => s.TokenCount);
            var compressedSegments = level switch
            {
                CompressionLevel.None => segmentList,
                CompressionLevel.Light => CompressLow(segmentList),
                CompressionLevel.Medium => CompressMedium(segmentList),
                CompressionLevel.Aggressive => CompressHigh(segmentList),
                _ => segmentList,
            };

            var compressedTokenCount = compressedSegments.Sum(s => s.TokenCount);
            var compressionRatio = originalTokenCount > 0
                ? 1.0 - (double)compressedTokenCount / originalTokenCount
                : 0;

            return new CompressionResult(compressedSegments, originalTokenCount, compressedTokenCount, compressionRatio);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error compressing segments");
            return CompressionResult.CreateEmpty();
        }
    }

    public async Task<string?> DecompressAsync(ContextSegment compressedSegment)
    {
        try
        {
            if (compressedSegment.TokenCount > 0 && compressedSegment.Content.Length > 0)
            {
                return compressedSegment.Content;
            }
            return compressedSegment.Content;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error decompressing segment {SegmentId}", compressedSegment.Id);
            return compressedSegment.Content;
        }
    }

    private List<ContextSegment> CompressLow(List<ContextSegment> segments)
    {
        return segments.Where(s => s.IsPinned || s.RelevanceScore > 0.3).ToList();
    }

    private List<ContextSegment> CompressMedium(List<ContextSegment> segments)
    {
        var pinned = segments.Where(s => s.IsPinned).ToList();
        var unpinned = segments.Where(s => !s.IsPinned).OrderBy(s => s.RelevanceScore).ToList();

        var result = new List<ContextSegment>(pinned);
        var threshold = 0.5;
        foreach (var segment in unpinned)
        {
            if (segment.RelevanceScore >= threshold)
            {
                result.Add(segment);
            }
            else
            {
                var compressed = new ContextSegment
                {
                    Id = segment.Id,
                    Content = segment.Content,
                    Role = segment.Role,
                    IsCompressed = true,
                    IsPinned = segment.IsPinned,
                    TokenCount = (int)(segment.TokenCount * 0.8),
                    RelevanceScore = segment.RelevanceScore,
                    InjectionType = segment.InjectionType,
                    IsSuppressed = segment.IsSuppressed,
                };
                result.Add(compressed);
            }
        }
        return result;
    }

    private List<ContextSegment> CompressHigh(List<ContextSegment> segments)
    {
        var pinned = segments.Where(s => s.IsPinned).ToList();
        var unpinned = segments.Where(s => !s.IsPinned).OrderBy(s => s.RelevanceScore).ToList();

        var result = new List<ContextSegment>(pinned);
        var threshold = 0.7;
        foreach (var segment in unpinned)
        {
            if (segment.RelevanceScore >= threshold)
            {
                result.Add(segment);
            }
            else
            {
                var compressed = new ContextSegment
                {
                    Id = segment.Id,
                    Content = segment.Content,
                    Role = segment.Role,
                    IsCompressed = true,
                    IsPinned = segment.IsPinned,
                    TokenCount = (int)(segment.TokenCount * 0.6),
                    RelevanceScore = segment.RelevanceScore,
                    InjectionType = segment.InjectionType,
                    IsSuppressed = segment.IsSuppressed,
                };
                result.Add(compressed);
            }
        }
        return result;
    }
}