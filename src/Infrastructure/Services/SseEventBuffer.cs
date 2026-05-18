using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Buffers SSE streaming events to allow partial response reconstruction when a connection drops.
/// Events are retained for the duration of the stream and cleaned up after completion.
/// </summary>
public interface ISseEventBuffer : IDisposable
{
    /// <summary>
    /// Records an SSE event in the buffer for potential reconnection replay.
    /// </summary>
    void RecordEvent(string connectionId, string eventId, string eventType, string data);

    /// <summary>
    /// Retrieves buffered events starting from a specific event ID (for reconnection).
    /// Returns null if no matching event is found or the buffer has been cleared.
    /// </summary>
    IReadOnlyList<SseBufferedEvent>? GetEventsFromId(string connectionId, string eventId);

    /// <summary>
    /// Marks the stream as complete for a given connection and returns any accumulated tokens.
    /// Called when the streaming response finishes or encounters an error.
    /// </summary>
    Task CompleteStreamAsync(string connectionId, long totalTokensGenerated);

    /// <summary>
    /// Gets whether buffered events exist for this connection (for reconnection).
    /// </summary>
    bool HasBufferedEvents(string connectionId);
}

/// <summary>
/// A single SSE event that was recorded in the buffer.
/// </summary>
public record SseBufferedEvent(
    string EventId,
    string EventType,
    string Data);

/// <summary>
/// Tracks buffered events and accumulated state for a single streaming connection.
/// Events are retained until completion or cleanup (whichever comes first).
/// </summary>
public class SseEventBuffer : ISseEventBuffer
{
    private readonly ILogger<SseEventBuffer>? _logger;

    // Per-connection event buffers, keyed by connection ID
    private readonly ConcurrentDictionary<string, List<SseBufferedEvent>> _buffers = new();

    // Per-connection accumulated state for reconnection reconstruction
    private readonly ConcurrentDictionary<string, (long TokensGenerated, DateTimeOffset LastSeen)> _streamStates = new();

    public SseEventBuffer(ILogger<SseEventBuffer>? logger = null)
    {
        _logger = logger;
    }

    public void RecordEvent(string connectionId, string eventId, string eventType, string data)
    {
        var evt = new SseBufferedEvent(eventId, eventType, data);

        if (!_buffers.TryGetValue(connectionId, out var buffer))
            _buffers[connectionId] = buffer = new List<SseBufferedEvent>();

        buffer.Add(evt);

        // Update the stream state with last-seen timestamp and token count
        if (eventType == "message_chunk" || eventType == "message_stop")
        {
            var tokensMatch = System.Text.RegularExpressions.Regex.Match(data, @"""\s*completion_tokens\s*:\s*(\d+)");
            long newTokens = 0;
            if (tokensMatch.Success && int.TryParse(tokensMatch.Groups[1].Value, out var tokenCount))
                newTokens = tokenCount;

            _streamStates.AddOrUpdate(
                connectionId,
                (newTokens, DateTimeOffset.UtcNow),   // Add
                (_, existing) => (existing.TokensGenerated + newTokens, DateTimeOffset.UtcNow));  // Update
        }

        _logger?.LogDebug("SSE event buffered for connection {ConnectionId}, type {EventType}", connectionId, eventType);
    }

    public IReadOnlyList<SseBufferedEvent>? GetEventsFromId(string connectionId, string eventId)
    {
        if (!_buffers.TryGetValue(connectionId, out var buffer))
            return null;

        // Find the index of the event matching the given ID (for SSE, events are sequential by timestamp)
        var idx = buffer.FindIndex(e => e.EventId == eventId);
        if (idx < 0)
            return null;

        // Return all events from that point onward
        _logger?.LogInformation("SSE reconnection: returning {Count} buffered events starting from ID {EventId}", buffer.Count - idx, eventId);
        return buffer.Skip(idx).ToList().AsReadOnly();
    }

    public async Task CompleteStreamAsync(string connectionId, long totalTokensGenerated)
    {
        // Record the final token count
        _streamStates[connectionId] = (totalTokensGenerated, DateTimeOffset.UtcNow);

        // Don't clear immediately — allow reconnection for a short window
        await Task.Delay(TimeSpan.FromSeconds(10)); // Short retention before cleanup

        if (_buffers.TryRemove(connectionId, out _))
            _logger?.LogDebug("SSE event buffer cleaned up for connection {ConnectionId}", connectionId);

        _streamStates.TryRemove(connectionId, out _);
    }

    public bool HasBufferedEvents(string connectionId) => _buffers.ContainsKey(connectionId);

    public void Dispose()
    {
        // Clean up all buffered events on disposal
        foreach (var kvp in _buffers)
            _logger?.LogDebug("SSE event buffer disposed: connection {ConnectionId}, {Count} events", kvp.Key, kvp.Value.Count);

        _buffers.Clear();
        _streamStates.Clear();
    }
}

/// <summary>
/// Extension methods for SSE event buffer DI registration.
/// </summary>
public static class SseEventBufferExtensions
{
    public static IServiceCollection AddSseEventBuffering(this IServiceCollection services)
    {
        return services.AddSingleton<ISseEventBuffer, SseEventBuffer>();
    }
}