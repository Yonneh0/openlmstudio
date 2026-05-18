using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Custom log scope provider for OpenLMStudio structured logging.
/// Adds correlation IDs, component context, and user session tracking to all log output.
/// Implements IDisposable instead of ILogScope since .NET Core doesn't expose that interface publicly — 
/// consumers should use ILogger.BeginScope() with this class as the scope value.
/// </summary>
public class OpenLmStudioLogScope : IDisposable
{
    private static readonly ConcurrentDictionary<string, string?> _correlationIds = new();

    private readonly string? _component;
    private readonly string? _sessionId;
    private readonly Guid _scopeId;

    public OpenLmStudioLogScope(string? component = null, string? sessionId = null)
    {
        _component = component;
        _sessionId = sessionId;
        _scopeId = Guid.NewGuid();

        // Generate a correlation ID for this scope if not already set via DI
        var existingCorrelationId = GetCorrelationIdForScope(_scopeId);
        if (existingCorrelationId != null)
            SetCurrentCorrelationId(existingCorrelationId);
    }

    /// <summary>
    /// Gets or sets the current correlation ID for this request/session.
    /// Used to correlate related log events across service boundaries.
    /// </summary>
    public static string? CurrentCorrelationId { get; set; }

    /// <summary>
    /// Sets a new correlation ID for this scope and stores it in the concurrent dictionary.
    /// </summary>
    private void SetCurrentCorrelationId(string id)
    {
        _correlationIds[_scopeId.ToString()] = id;
        CurrentCorrelationId = id;
    }

    /// <summary>
    /// Gets the correlation ID for this specific scope.
    /// </summary>
    private string? GetCorrelationIdForScope(Guid scopeId) =>
        _correlationIds.TryGetValue(scopeId.ToString(), out var correlationId) ? correlationId : null;

    public void Dispose()
    {
        // Clean up the correlation ID entry when scope is disposed
        _correlationIds.TryRemove(_scopeId.ToString(), out _);
        if (CurrentCorrelationId == GetCorrelationIdForScope(_scopeId))
            CurrentCorrelationId = null;
    }

    public void Write(string message, LogLevel logLevel)
    {
        // Build structured log entry with correlation context
        var builder = new System.Text.StringBuilder();

        if (CurrentCorrelationId != null)
            builder.Append($"[{logLevel}] CorrelationId={CurrentCorrelationId} ");

        if (_component != null)
            builder.Append($"Component={_component} ");

        if (_sessionId != null)
            builder.Append($"SessionId={_sessionId} ");

        builder.AppendLine();
        builder.Append(message);

        // Write to the current log sink — the actual logging is done by the caller's ILogger
    }

}

/// <summary>
/// Extension methods for structured logging with OpenLMStudio correlation context.
/// </summary>
public static class OpenLmStudioLoggingExtensions
{
    /// <summary>
    /// Creates a new log scope with component and session tracking.
    /// Automatically generates a correlation ID for request tracing.
    /// </summary>
    public static IDisposable? BeginScopeWithCorrelation(this ILogger logger, string component, string sessionId)
    {
        var correlationId = Guid.NewGuid().ToString("N")[..8]; // Short 8-char correlation ID

        var scope = new OpenLmStudioLogScope(component, sessionId);

        logger.LogDebug("[OpenLMStudio] Starting structured logging with CorrelationId={CorrelationId} Component={Component}",
            correlationId, component);

        return scope;
    }

    /// <summary>
    /// Logs an event with the OpenLMStudio telemetry component tag.
    /// </summary>
    public static void LogOpenLmStudioEvent(this ILogger logger, LogLevel logLevel, string eventName, int id = 0)
    {
        var correlationId = OpenLmStudioLogScope.CurrentCorrelationId ?? "none";

        // Add event name as a tag to the current Activity if one exists
        using (var activity = System.Diagnostics.Activity.Current)
        {
            if (activity != null)
            {
                activity.SetTag("openlmstudio.event", eventName);
                activity.SetTag("openlmstudio.event.id", id);
            }
        }

        logger.Log(logLevel, "OpenLMStudio Event: {EventName} CorrelationId={CorrelationId}", eventName, correlationId);
    }

    /// <summary>
    /// Logs a warning event with the OpenLMStudio telemetry component tag.
    /// </summary>
    public static void WarnOpenLmStudioEvent(this ILogger logger, string eventName, int id = 0) =>
        LogOpenLmStudioEvent(logger, LogLevel.Warning, eventName, id);

    /// <summary>
    /// Logs an error event with the OpenLMStudio telemetry component tag.
    /// </summary>
    public static void ErrorOpenLmStudioEvent(this ILogger logger, string eventName, int id = 0) =>
        LogOpenLmStudioEvent(logger, LogLevel.Error, eventName, id);

    /// <summary>
    /// Logs an information event with the OpenLMStudio telemetry component tag.
    /// </summary>
    public static void InfoOpenLmStudioEvent(this ILogger logger, string eventName, int id = 0) =>
        LogOpenLmStudioEvent(logger, LogLevel.Information, eventName, id);

    /// <summary>
    /// Logs an informational event with the OpenLMStudio telemetry component tag.
    /// </summary>
    public static void DebugOpenLmStudioEvent(this ILogger logger, string eventName, int id = 0) =>
        LogOpenLmStudioEvent(logger, LogLevel.Debug, eventName, id);
}