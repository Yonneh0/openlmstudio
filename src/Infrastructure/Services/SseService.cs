using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models;
using System.Collections.Concurrent;

namespace OpenLMStudio.Infrastructure.Services;

// ========== SseEventBuffer (was SseEventBuffer.cs) ==========

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

// ========== SseReconnectService (was SseReconnectService.cs) ==========

/// <summary>
/// Manages SSE reconnection tracking and session resumption for streaming chat completions.
/// Supports the Server-Sent Events (SSE) Last-Event-ID header to resume from a specific event ID.
/// </summary>
public interface ISseReconnectService : IDisposable
{
    /// <summary>
    /// Registers a new SSE connection and returns its unique identifier.
    /// The connection is tracked for cleanup on server shutdown.
    /// </summary>
    string RegisterConnection(string chatRequestId);

    /// <summary>
    /// Unregisters an SSE connection, freeing resources. Called when the client disconnects normally.
    /// </summary>
    void UnregisterConnection(string connectionId);

    /// <summary>
    /// Checks if a given event ID matches any recently completed streaming session for reconnection purposes.
    /// Returns the chat request context if found, null otherwise.
    /// </summary>
    Task<StreamedChatSession?> GetReconnectContextAsync(string eventId);

    /// <summary>
    /// Gets the list of active (connected) SSE connection IDs.
    /// </summary>
    IReadOnlyList<string> GetActiveConnections();

    /// <summary>
    /// Gets the count of currently active streaming connections.
    /// </summary>
    int ActiveConnectionCount { get; }
}

/// <summary>
/// Represents a streamed chat session that can be resumed on reconnection.
/// </summary>
public record StreamedChatSession(
    string ConnectionId,
    string ChatRequestId,
    string ModelId,
    List<Domain.Models.Message> ConversationHistory,
    long TokensGeneratedSoFar,
    double ElapsedMilliseconds);

/// <summary>
/// Tracks active and recently completed SSE streaming sessions for reconnection.
/// Sessions are retained for 5 minutes after completion to allow resumption.
/// </summary>
public class SseReconnectService : ISseReconnectService
{
    private readonly ILogger<SseReconnectService>? _logger;
    private readonly Dictionary<string, StreamedChatSession> _activeSessions = new();
    private readonly Dictionary<string, StreamedChatSession> _recentlyCompleted = new();
    private readonly TimeSpan _reconnectRetentionPeriod = TimeSpan.FromMinutes(5);
    private readonly object _lockObj = new();

    public SseReconnectService(ILogger<SseReconnectService>? logger = null)
    {
        _logger = logger;
    }

    public string RegisterConnection(string chatRequestId)
    {
        var connectionId = Guid.NewGuid().ToString("N")[..16]; // Shorter UUID-like ID for SSE events

        lock (_lockObj)
        {
            _activeSessions[connectionId] = new StreamedChatSession(
                connectionId,
                chatRequestId,
                "unknown",  // Model will be set on first chunk
                new List<Domain.Models.Message>(),
                0L,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        _logger?.LogDebug("SSE connection registered: {ConnectionId} for request {ChatRequestId}",
            connectionId, chatRequestId);

        return connectionId;
    }

    public void UnregisterConnection(string connectionId)
    {
        StreamedChatSession? sessionToMove = null;

        lock (_lockObj)
        {
            if (_activeSessions.TryGetValue(connectionId, out var session))
            {
                // Calculate elapsed time
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                sessionToMove = new(
                    session.ConnectionId,
                    session.ChatRequestId,
                    session.ModelId,
                    session.ConversationHistory.ToList(),
                    session.TokensGeneratedSoFar,
                    now - session.ElapsedMilliseconds);

                _activeSessions.Remove(connectionId);
            }
        }

        if (sessionToMove != null)
        {
            lock (_lockObj)
            {
                // Move to recently completed for reconnection window
                _recentlyCompleted[sessionToMove.ChatRequestId] = sessionToMove;
            }
        }

        _logger?.LogDebug("SSE connection unregistered: {ConnectionId}", connectionId);
    }

    public async Task<StreamedChatSession?> GetReconnectContextAsync(string eventId)
    {
        lock (_lockObj)
        {
            // Check if this event ID is for an active session (resuming from the same stream)
            if (_activeSessions.TryGetValue(eventId, out var activeSession))
                return activeSession;

            // Check recently completed sessions within retention period
            foreach (var kvp in _recentlyCompleted.ToList())
            {
                var elapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - kvp.Value.ElapsedMilliseconds;
                if (elapsed > _reconnectRetentionPeriod.TotalMilliseconds)
                    continue;  // Session expired

                // Return a copy with updated elapsed time
                return new(
                    kvp.Value.ConnectionId,
                    kvp.Value.ChatRequestId,
                    kvp.Value.ModelId,
                    kvp.Value.ConversationHistory.ToList(),
                    kvp.Value.TokensGeneratedSoFar,
                    elapsed);
            }
        }

        return null;
    }

    public IReadOnlyList<string> GetActiveConnections()
    {
        lock (_lockObj)
            return _activeSessions.Keys.ToList().AsReadOnly();
    }

    public int ActiveConnectionCount => _activeSessions.Count;

    /// <summary>
    /// Cleans up expired recently completed sessions. Called periodically by the server service.
    /// </summary>
    public void CleanupExpiredSessions()
    {
        lock (_lockObj)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach (var kvp in _recentlyCompleted.ToList())
            {
                if (now - kvp.Value.ElapsedMilliseconds > _reconnectRetentionPeriod.TotalMilliseconds)
                    _recentlyCompleted.Remove(kvp.Key);
            }
        }
    }

    public void Dispose()
    {
        lock (_lockObj)
        {
            _activeSessions.Clear();
            _recentlyCompleted.Clear();
        }
    }
}

// ========== Extension Methods (merged from both files) ==========

/// <summary>
/// Extension methods for SSE service DI registration.
/// </summary>
public static class SseServiceExtensions
{
    public static IServiceCollection AddSseEventBuffering(this IServiceCollection services)
    {
        return services.AddSingleton<ISseEventBuffer, SseEventBuffer>();
    }

    public static IServiceCollection AddSseReconnectTracking(this IServiceCollection services)
    {
        return services.AddSingleton<ISseReconnectService, SseReconnectService>();
    }
}