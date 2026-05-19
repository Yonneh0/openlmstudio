using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Implements IContextManipulator for user-driven context segment control (pin, suppress, add custom context).
/// Delegates pin/suppress operations to ChatContextManager and manages custom injection state.
/// </summary>
public class ContextManipulator : IContextManipulator, IDisposable
{
    private readonly ILogger<ContextManipulator>? _logger;
    private readonly ChatContextManager _chatContextManager;

    // In-memory cache of pinned segment IDs per chat (for fast UI access without DB round-trip)
    private readonly ConcurrentDictionary<Guid, HashSet<Guid>> _pinnedCache = new();

    // In-memory cache of suppressed segment IDs per chat
    private readonly ConcurrentDictionary<Guid, HashSet<Guid>> _suppressedCache = new();

    // Cache of chat IDs we've already synced, to avoid repeated DB scans
    private readonly ConcurrentDictionary<Guid, bool> _syncedChats = new();

    public ContextManipulator(ILogger<ContextManipulator>? logger, ChatContextManager chatContextManager)
    {
        _logger = logger;
        _chatContextManager = chatContextManager;
    }

    /// <inheritdoc />
    public async Task<ContextSegment?> ManipulateAsync(ContextManipulationRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        switch (request.Action)
        {
            case ContextManipulationAction.Pin:
                if (request.SegmentId != null && request.SegmentId.Value != Guid.Empty)
                {
                    await _chatContextManager.PinSegmentAsync(request.ChatId, request.SegmentId.Value);

                    // Update cache
                    var pinnedSet = _pinnedCache.GetOrAdd(request.ChatId, _ => new HashSet<Guid>());
                    pinnedSet.Add(request.SegmentId.Value);
                    _logger?.LogDebug("Segment {SegmentId} in chat {ChatId} pinned", request.SegmentId.Value, request.ChatId);
                }
                break;

            case ContextManipulationAction.Unpin:
                if (request.SegmentId != null && request.SegmentId.Value != Guid.Empty)
                {
                    await _chatContextManager.UnpinSegmentAsync(request.ChatId, request.SegmentId.Value);

                    // Update cache
                    var pinnedSet2 = _pinnedCache.GetOrAdd(request.ChatId, _ => new HashSet<Guid>());
                    pinnedSet2.Remove(request.SegmentId.Value);

                    _logger?.LogDebug("Segment {SegmentId} in chat {ChatId} unpinned", request.SegmentId.Value, request.ChatId);
                }
                break;

            case ContextManipulationAction.SuppressToggle:
                if (request.SegmentId.HasValue && request.SegmentId.Value != Guid.Empty)
                {
                    // Check current suppress state from cache first
                    var suppressedSet = _suppressedCache.GetOrAdd(request.ChatId, _ => new HashSet<Guid>());

                    if (suppressedSet.Contains(request.SegmentId.Value))
                    {
                        await _chatContextManager.RevealSegmentAsync(request.ChatId, request.SegmentId.Value);
                        suppressedSet.Remove(request.SegmentId.Value);
                        _logger?.LogDebug("Segment {SegmentId} in chat {ChatId} revealed", request.SegmentId, request.ChatId);
                    }
                    else
                    {
                        await _chatContextManager.SuppressSegmentAsync(request.ChatId, request.SegmentId.Value);
                        suppressedSet.Add(request.SegmentId.Value);
                        _logger?.LogDebug("Segment {SegmentId} in chat {ChatId} suppressed", request.SegmentId, request.ChatId);
                    }
                }
                break;

            case ContextManipulationAction.RemoveFromContext:
                if (request.SegmentId.HasValue && request.SegmentId.Value != Guid.Empty)
                {
                    var suppressedSet2 = _suppressedCache.GetOrAdd(request.ChatId, _ => new HashSet<Guid>());

                    if (!suppressedSet2.Contains(request.SegmentId.Value))
                    {
                        await _chatContextManager.SuppressSegmentAsync(request.ChatId, request.SegmentId.Value);
                        suppressedSet2.Add(request.SegmentId.Value);
                        _logger?.LogDebug("Segment {SegmentId} in chat {ChatId} removed from context", request.SegmentId, request.ChatId);
                    }
                }
                break;

            case ContextManipulationAction.AddCustomContext:
                if (!string.IsNullOrEmpty(request.Content))
                {
                    var segment = await _chatContextManager.InjectCustomContextAsync(
                        request.ChatId,
                        request.Content,
                        request.InjectionType);

                    _logger?.LogDebug("Custom context injected for chat {ChatId}: Type={InjectionType}",
                        request.ChatId, request.InjectionType);

                    return segment;
                }
                break;

            default:
                _logger?.LogWarning("Unknown manipulation action: {Action}", request.Action);
                break;
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<List<ContextSegment>> GetPinnedSegmentsAsync(Guid chatId)
    {
        try
        {
            // Use ChatContextManager to get pinned segments from the database
            var window = await _chatContextManager.GetCompressedContextAsync(chatId);

            return window.Segments.Where(s => s.IsPinned).ToList();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            _logger?.LogWarning(ex, "Failed to get pinned segments for chat: {ChatId}", chatId);
            return new List<ContextSegment>();
        }
    }

    /// <inheritdoc />
    public async Task<List<ContextSegment>> GetSuppressedSegmentsAsync(Guid chatId)
    {
        try
        {
            // Check cache first for speed, fall back to database
            var cached = _suppressedCache.GetOrAdd(chatId, _ => new HashSet<Guid>());

            if (cached.Count > 0)
            {
                return cached.Select(id => new ContextSegment
                {
                    Id = id,
                    IsSuppressed = true,
                    Role = MessageRole.System
                }).ToList();
            }

            // Database fallback: get all segments and filter by suppression state
            var window = await _chatContextManager.GetCompressedContextAsync(chatId);

            return window.Segments.Where(s => s.IsSuppressed).ToList();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            _logger?.LogWarning(ex, "Failed to get suppressed segments for chat: {ChatId}", chatId);
            return new List<ContextSegment>();
        }
    }

    /// <inheritdoc />
    public async Task<List<ContextSegment>> GetCustomInjectionsAsync(Guid chatId, ContextInjectionType? injectionType = null)
    {
        try
        {
            var window = await _chatContextManager.GetCompressedContextAsync(chatId);

            return window.Segments.Where(s =>
                s.InjectionType == ContextInjectionType.CustomInjection &&
                (injectionType == null || s.InjectionType == injectionType.Value))
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            _logger?.LogWarning(ex, "Failed to get custom injections for chat: {ChatId}", chatId);
            return new List<ContextSegment>();
        }
    }

    /// <summary>
    /// Rebuilds the in-memory caches for a given chat by scanning the database.
    /// Called automatically on first access to ensure caches reflect persisted state.
    /// </summary>
    private async Task EnsureCacheSyncedAsync(Guid chatId)
    {
        if (_syncedChats.TryGetValue(chatId, out _))
            return; // Already synced for this chat

        try
        {
            var window = await _chatContextManager.GetCompressedContextAsync(chatId);

            // Populate pinned cache
            var pinnedSet = _pinnedCache.GetOrAdd(chatId, _ => new HashSet<Guid>());
            foreach (var seg in window.Segments.Where(s => s.IsPinned))
            {
                pinnedSet.Add(seg.Id);
            }

            // Populate suppressed cache
            var suppressedSet = _suppressedCache.GetOrAdd(chatId, _ => new HashSet<Guid>());
            foreach (var seg in window.Segments.Where(s => s.IsSuppressed))
            {
                suppressedSet.Add(seg.Id);
            }

            _syncedChats[chatId] = true;
            _logger?.LogDebug("ContextManipulator caches rebuilt for chat {ChatId}: {Pinned} pinned, {Suppressed} suppressed",
                chatId, pinnedSet.Count, suppressedSet.Count);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            _logger?.LogWarning(ex, "Failed to rebuild caches for chat {ChatId}", chatId);
        }
    }

    public void Dispose()
    {
        _pinnedCache.Clear();
        _suppressedCache.Clear();
        _syncedChats.Clear();
    }
}
