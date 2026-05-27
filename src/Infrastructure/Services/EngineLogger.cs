using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using System.Text.Json;
using System.Text;

using AppLogLevel = OpenLMStudio.Application.Interfaces.LogLevel;
using MLogLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Structured logging for engine stdout/stderr with disk rotation.
/// </summary>
public class EngineLogger : IEngineLogger
{
    private readonly ILogger<EngineLogger> _logger;
    private readonly Dictionary<EngineType, EngineLoggingSession> _sessions = new();
    private readonly EngineLoggerConfig _config;

    public EngineLogger(ILogger<EngineLogger> logger, EngineLoggerConfig? config = null)
    {
        _logger = logger;
        _config = config ?? new EngineLoggerConfig();
    }

    /// <summary>
    /// Starts logging for a given engine, reusing the session if one already exists.
    /// </summary>
    public async Task StartSessionAsync(EngineType engineId, EngineLoggerConfig? config = null)
    {
        var cfg = config ?? _config;
        var session = new EngineLoggingSession
        {
            EngineId = engineId,
            IsActive = true,
            MaxEntries = 10000,
        };

        if (cfg.EnableDiskLogging && !string.IsNullOrEmpty(cfg.LogDirectory))
        {
            var logFile = Path.Combine(cfg.LogDirectory, $"engine-{engineId.ToString().ToLower()}.log");
            session.LogFile = logFile;
            Directory.CreateDirectory(cfg.LogDirectory);

            // Clean up old rotated logs (keep only the last 5 rotations)
            await CleanupOldRotatedLogsAsync(logFile);
        }

        lock (_sessions)
        {
            if (_sessions.TryGetValue(engineId, out var existing))
            {
                // Reuse existing session but update config
                existing.IsActive = true;
                if (session.LogFile != null)
                    existing.LogFile = session.LogFile;
                return;
            }

            _sessions[engineId] = session;
        }
    }

    /// <summary>
    /// Cleans up old rotated log files, keeping only the most recent rotations.
    /// </summary>
    private async Task CleanupOldRotatedLogsAsync(string logFile)
    {
        try
        {
            var dir = Path.GetDirectoryName(logFile);
            if (string.IsNullOrEmpty(dir))
                return;

            var baseName = Path.GetFileName(logFile);
            var rotatedLogs = Directory.GetFiles(dir, $"{baseName}.*")
                .OrderByDescending(f => f)
                .Skip(5)
                .ToList();

            foreach (var oldLog in rotatedLogs)
            {
                File.Delete(oldLog);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup old rotated logs for {LogFile}", logFile);
        }
    }

    public Task StopSessionAsync(EngineType engineId)
    {
        lock (_sessions)
            _sessions.Remove(engineId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Adds a log entry for the given engine.
    /// </summary>
    public EngineLogEntry AddLogEntry(EngineType engineId, AppLogLevel level, string message)
    {
        var entry = new EngineLogEntry
        {
            Id = $"log-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Timestamp = DateTime.UtcNow,
            Level = level,
            Message = message,
            Source = engineId,
        };

        lock (_sessions)
        {
            if (_sessions.TryGetValue(engineId, out var session))
            {
                session.LogEntries.Add(entry);
                if (session.LogEntries.Count > session.MaxEntries)
                    session.LogEntries.RemoveAt(0);
            }
        }

        if (_config.EnableDiskLogging)
        {
            var logFile = GetLogFile(engineId);
            if (logFile != null)
            {
                RotateLogFileIfNecessary(logFile);
                File.AppendAllText(logFile, $"[{entry.Timestamp:O}] [{level}] {message}\n");
            }
        }

        // Forward to Microsoft logging
        _logger.Log(MapLogLevel(level), message);

        return entry;
    }

    /// <summary>
    /// Gets the most recent log entries for an engine, sorted by timestamp descending.
    /// </summary>
    public IEnumerable<EngineLogEntry> GetRecentLogs(EngineType engineId, int count = 20)
    {
        lock (_sessions)
        {
            if (_sessions.TryGetValue(engineId, out var session))
            {
                return session.LogEntries
                    .OrderByDescending(e => e.Timestamp)
                    .Take(count)
                    .ToList();
            }
        }
        return Enumerable.Empty<EngineLogEntry>();
    }

    /// <summary>
    /// Gets log entries for an engine filtered by minimum log level.
    /// </summary>
    public IEnumerable<EngineLogEntry> GetLogsByLevel(EngineType engineId, AppLogLevel minLevel)
    {
        lock (_sessions)
        {
            if (_sessions.TryGetValue(engineId, out var session))
            {
                return session.LogEntries
                    .Where(e => e.Level >= minLevel)
                    .ToList();
            }
        }
        return Enumerable.Empty<EngineLogEntry>();
    }

    /// <summary>
    /// Handles engine stdout output, parsing JSON log lines and SSE chunks.
    /// Handles multiple formats: structured JSON, plain text, and SSE-style token streams.
    /// </summary>
    public void HandleEngineStdout(EngineType engineId, string data)
    {
        var lines = data.Split('\n', '\r').Where(l => l.Trim().Length > 0);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
                continue;

            try
            {
                // Try parsing as JSON log line (structured)
                using var parsed = JsonDocument.Parse(trimmed);

                // Check for SSE-style token stream: {"choices": [{"delta": {"content": "..."}}]}
                if (parsed.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var delta = choices[0];
                    if (delta.TryGetProperty("delta", out var deltaProp) && deltaProp.TryGetProperty("content", out var contentProp))
                    {
                        var content = contentProp.GetString();
                        if (!string.IsNullOrEmpty(content))
                            AddLogEntry(engineId, AppLogLevel.Trace, $"Token: {content}");
                        continue;
                    }
                }

                // Check for structured log entry: {"level": "...", "message": "..."}
                if (parsed.RootElement.TryGetProperty("level", out var levelProp) &&
                    parsed.RootElement.TryGetProperty("message", out var messageProp))
                {
                    var levelStr = levelProp.GetString()?.ToLowerInvariant();
                    var message = messageProp.GetString() ?? trimmed;
                    var parsedLevel = levelStr switch
                    {
                        "trace" or "debug" => AppLogLevel.Debug,
                        "info" => AppLogLevel.Info,
                        "warn" => AppLogLevel.Warn,
                        "error" => AppLogLevel.Error,
                        _ => AppLogLevel.Info
                    };
                    AddLogEntry(engineId, parsedLevel, message);
                    continue;
                }

                // Generic JSON with "msg" property
                if (parsed.RootElement.TryGetProperty("msg", out var msgProp))
                {
                    var msg = msgProp.GetString();
                    if (!string.IsNullOrEmpty(msg))
                        AddLogEntry(engineId, AppLogLevel.Info, msg);
                    continue;
                }

                // Fallback: treat as plain text
                AddLogEntry(engineId, AppLogLevel.Info, trimmed);
            }
            catch (JsonException)
            {
                // Not JSON, treat as plain text
                if (trimmed.Length > 0)
                    AddLogEntry(engineId, AppLogLevel.Info, trimmed);
            }
        }
    }

    /// <summary>
    /// Handles engine stderr output, parsing JSON error lines and logging plain text.
    /// </summary>
    public void HandleEngineStderr(EngineType engineId, string data)
    {
        var trimmed = data.Trim();
        if (trimmed.Length == 0)
            return;

        try
        {
            // Try parsing as JSON error
            using var parsed = JsonDocument.Parse(trimmed);
            if (parsed.RootElement.TryGetProperty("error", out var errorProp))
            {
                var error = errorProp.GetString();
                if (!string.IsNullOrEmpty(error))
                {
                    AddLogEntry(engineId, AppLogLevel.Error, $"stderr: {error}");
                    return;
                }
            }

            if (parsed.RootElement.TryGetProperty("message", out var messageProp))
            {
                var message = messageProp.GetString();
                if (!string.IsNullOrEmpty(message))
                {
                    AddLogEntry(engineId, AppLogLevel.Error, $"stderr: {message}");
                    return;
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON, log as plain text
        }

        AddLogEntry(engineId, AppLogLevel.Error, $"stderr: {trimmed}");
    }

    private static MLogLogLevel MapLogLevel(AppLogLevel level) => level switch
    {
        AppLogLevel.Trace => MLogLogLevel.Debug,
        AppLogLevel.Debug => MLogLogLevel.Debug,
        AppLogLevel.Info => MLogLogLevel.Information,
        AppLogLevel.Warn => MLogLogLevel.Warning,
        AppLogLevel.Error => MLogLogLevel.Error,
        _ => MLogLogLevel.Information,
    };

    public IEnumerable<EngineLogEntry> GetLogs(EngineType engineId)
    {
        lock (_sessions)
            return _sessions.GetValueOrDefault(engineId)?.LogEntries ?? Enumerable.Empty<EngineLogEntry>();
    }

    public void ClearLogs(EngineType engineId)
    {
        lock (_sessions)
            _sessions.GetValueOrDefault(engineId)?.LogEntries.Clear();
    }

    private string? GetLogFile(EngineType engineId)
    {
        lock (_sessions)
            return _sessions.GetValueOrDefault(engineId)?.LogFile;
    }

    /// <summary>
    /// Rotates the log file if it exceeds the configured size limit.
    /// Keeps up to 5 rotated copies, removing the oldest when exceeded.
    /// </summary>
    private void RotateLogFileIfNecessary(string logFilePath)
    {
        try
        {
            var fileInfo = new FileInfo(logFilePath);
            var maxSizeBytes = (long)_config.DiskLogRotationSizeMB * 1024 * 1024;

            if (fileInfo.Length >= maxSizeBytes)
            {
                // Find the next rotation index
                int rotationIndex = 1;
                while (File.Exists(logFilePath + $".{rotationIndex}"))
                    rotationIndex++;

                // If rotationIndex exceeds max rotations, delete the oldest
                if (rotationIndex > 5)
                {
                    var oldestLog = logFilePath + ".5";
                    if (File.Exists(oldestLog))
                        File.Delete(oldestLog);

                    // Shift remaining logs
                    for (int i = 4; i >= 1; i--)
                    {
                        var src = logFilePath + $".{i}";
                        var dst = logFilePath + $".{i + 1}";
                        if (File.Exists(src))
                            File.Move(src, dst, overwrite: true);
                    }
                }

                // Move current log to rotationIndex
                var destLog = logFilePath + $".{rotationIndex}";
                File.Move(logFilePath, destLog, overwrite: true);

                // Create new log file with header
                var header = new StringBuilder();
                header.AppendLine($"[Rotated] previous {fileInfo.Length / (1024 * 1024)} MB moved to .{rotationIndex}");
                header.AppendLine($"[Started] {DateTimeOffset.UtcNow:O}");
                File.WriteAllText(logFilePath, header.ToString());
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to rotate log file {LogFile}", logFilePath);
        }
    }
}