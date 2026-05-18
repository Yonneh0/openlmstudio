using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

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

/// <summary>
/// Extension methods for SSE reconnection service DI registration.
/// </summary>
public static class SseReconnectServiceExtensions
{
    public static IServiceCollection AddSseReconnectTracking(this IServiceCollection services)
    {
        return services.AddSingleton<ISseReconnectService, SseReconnectService>();
    }
}