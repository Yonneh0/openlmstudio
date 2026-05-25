namespace OpenLMStudio.Application.Services;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

/// <summary>
/// Manages user-driven context segment manipulation (pin, suppress, remove, custom context).
/// </summary>
public class ContextManipulator : IContextManipulator
{
    private readonly ConcurrentDictionary<Guid, List<ContextSegment>> _segments;
    private readonly ILogger<ContextManipulator> _logger;
    private readonly object _lock = new();

    public ContextManipulator(
        ILogger<ContextManipulator>? logger = null)
    {
        _segments = new ConcurrentDictionary<Guid, List<ContextSegment>>();
        _logger = logger ?? NullLogger<ContextManipulator>.Instance;
    }

    public void Dispose()
    {
        foreach (var segmentList in _segments)
        {
            segmentList.Value.Clear();
        }
        _segments.Clear();
    }

    public async Task<ContextSegment?> ManipulateAsync(ContextManipulationRequest request)
    {
        try
        {
            var segmentList = _segments.GetOrAdd(request.ChatId, _ => new List<ContextSegment>());

            return request.Action switch
            {
                ContextManipulationAction.Pin => PinSegment(segmentList, request.SegmentId),
                ContextManipulationAction.Unpin => UnpinSegment(segmentList, request.SegmentId),
                ContextManipulationAction.SuppressToggle => ToggleSuppress(segmentList, request.SegmentId),
                ContextManipulationAction.RemoveFromContext => RemoveFromContext(segmentList, request.SegmentId),
                ContextManipulationAction.AddCustomContext => AddCustomContext(segmentList, request.Content!, request.InjectionType),
                _ => null,
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error manipulating context for chat {ChatId}", request.ChatId);
            return null;
        }
    }

    public async Task<List<ContextSegment>> GetPinnedSegmentsAsync(Guid chatId)
    {
        if (_segments.TryGetValue(chatId, out var segments))
        {
            return segments.Where(s => s.IsPinned).ToList();
        }
        return new List<ContextSegment>();
    }

    public async Task<List<ContextSegment>> GetSuppressedSegmentsAsync(Guid chatId)
    {
        if (_segments.TryGetValue(chatId, out var segments))
        {
            return segments.Where(s => s.IsSuppressed).ToList();
        }
        return new List<ContextSegment>();
    }

    public async Task<List<ContextSegment>> GetCustomInjectionsAsync(Guid chatId, ContextInjectionType? injectionType = null)
    {
        if (_segments.TryGetValue(chatId, out var segments))
        {
            return injectionType.HasValue
                ? segments.Where(s => s.InjectionType == injectionType.Value).ToList()
                : segments.Where(s => s.InjectionType == ContextInjectionType.CustomInjection).ToList();
        }
        return new List<ContextSegment>();
    }

    private ContextSegment? PinSegment(List<ContextSegment> segments, Guid? segmentId)
    {
        if (segmentId.HasValue)
        {
            var segment = segments.FirstOrDefault(s => s.Id == segmentId);
            if (segment != null)
            {
                segment.IsPinned = true;
                return segment;
            }
        }
        return null;
    }

    private ContextSegment? UnpinSegment(List<ContextSegment> segments, Guid? segmentId)
    {
        if (segmentId.HasValue)
        {
            var segment = segments.FirstOrDefault(s => s.Id == segmentId);
            if (segment != null)
            {
                segment.IsPinned = false;
                return segment;
            }
        }
        return null;
    }

    private ContextSegment? ToggleSuppress(List<ContextSegment> segments, Guid? segmentId)
    {
        if (segmentId.HasValue)
        {
            var segment = segments.FirstOrDefault(s => s.Id == segmentId);
            if (segment != null)
            {
                segment.IsSuppressed = !segment.IsSuppressed;
                return segment;
            }
        }
        return null;
    }

    private ContextSegment? RemoveFromContext(List<ContextSegment> segments, Guid? segmentId)
    {
        if (segmentId.HasValue)
        {
            var segment = segments.FirstOrDefault(s => s.Id == segmentId);
            if (segment != null)
            {
                segments.Remove(segment);
                return segment;
            }
        }
        return null;
    }

    private ContextSegment AddCustomContext(List<ContextSegment> segments, string content, ContextInjectionType injectionType)
    {
        var newSegment = new ContextSegment
        {
            Id = Guid.NewGuid(),
            Content = content,
            Role = OpenLMStudio.Domain.Models.MessageRole.User,
            IsCompressed = false,
            IsPinned = false,
            TokenCount = (int)Math.Ceiling(content.Length / 4.0),
            RelevanceScore = 0.5f,
            InjectionType = injectionType,
            IsSuppressed = false,
        };

        lock (_lock)
        {
            segments.Add(newSegment);
        }
        return newSegment;
    }
}