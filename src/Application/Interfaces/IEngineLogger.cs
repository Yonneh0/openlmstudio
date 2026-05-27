using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Log level for engine logging.
/// </summary>
public enum LogLevel
{
    Trace,
    Debug,
    Info,
    Warn,
    Error
}

/// <summary>
/// Type of engine being logged.
/// </summary>
public enum EngineType
{
    Primary,
    SystemAI,
    Diffusion,
    Embedding
}

/// <summary>
/// A single log entry from an engine.
/// </summary>
public class EngineLogEntry
{
    public string Id { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Message { get; set; } = "";
    public EngineType Source { get; set; }
}

/// <summary>
/// Configuration for engine logging.
/// </summary>
public class EngineLoggerConfig
{
    public bool EnableDiskLogging { get; set; } = true;
    public string? LogDirectory { get; set; }
    public int DiskLogRotationSizeMB { get; set; } = 50;
}

/// <summary>
/// Logging session for a specific engine.
/// </summary>
public class EngineLoggingSession
{
    public EngineType EngineId { get; set; }
    public bool IsActive { get; set; }
    public List<EngineLogEntry> LogEntries { get; set; } = new();
    public string? LogFile { get; set; }
    public int MaxEntries { get; set; } = 10000;
}

/// <summary>
/// Structured logging for engine stdout/stderr with disk rotation.
/// </summary>
public interface IEngineLogger
{
    /// <summary>
    /// Starts logging for a given engine.
    /// </summary>
    Task StartSessionAsync(EngineType engineId, EngineLoggerConfig? config = null);

    /// <summary>
    /// Stops logging for a given engine.
    /// </summary>
    Task StopSessionAsync(EngineType engineId);

    /// <summary>
    /// Adds a log entry.
    /// </summary>
    EngineLogEntry AddLogEntry(EngineType engineId, LogLevel level, string message);

    /// <summary>
    /// Handles engine stdout output, parsing SSE chunks and logging raw lines.
    /// </summary>
    void HandleEngineStdout(EngineType engineId, string data);

    /// <summary>
    /// Handles engine stderr output.
    /// </summary>
    void HandleEngineStderr(EngineType engineId, string data);

    /// <summary>
    /// Gets all log entries for an engine.
    /// </summary>
    IEnumerable<EngineLogEntry> GetLogs(EngineType engineId);

    /// <summary>
    /// Clears logs for an engine.
    /// </summary>
    void ClearLogs(EngineType engineId);

    /// <summary>
    /// Gets the most recent log entries for an engine, sorted by timestamp descending.
    /// </summary>
    IEnumerable<EngineLogEntry> GetRecentLogs(EngineType engineId, int count = 20);

    /// <summary>
    /// Gets log entries for an engine filtered by minimum log level.
    /// </summary>
    IEnumerable<EngineLogEntry> GetLogsByLevel(EngineType engineId, LogLevel minLevel);
}
