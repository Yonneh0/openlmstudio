using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using System.Text.Json;

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

    public async Task StartSessionAsync(EngineType engineId, EngineLoggerConfig? config = null)
    {
        var session = new EngineLoggingSession
        {
            EngineId = engineId,
            IsActive = true,
            MaxEntries = 10000,
        };

        var cfg = config ?? _config;
        if (cfg.EnableDiskLogging && !string.IsNullOrEmpty(cfg.LogDirectory))
        {
            var logFile = Path.Combine(cfg.LogDirectory, $"engine-{engineId.ToString().ToLower()}.log");
            session.LogFile = logFile;
            Directory.CreateDirectory(cfg.LogDirectory);
        }

        lock (_sessions)
            _sessions[engineId] = session;
    }

    public Task StopSessionAsync(EngineType engineId)
    {
        lock (_sessions)
            _sessions.Remove(engineId);
        return Task.CompletedTask;
    }

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

    public void HandleEngineStdout(EngineType engineId, string data)
    {
        var lines = data.Split('\n', '\r').Where(l => l.Trim().Length > 0);
        foreach (var line in lines)
        {
            try
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(line);
                using var parsed = JsonDocument.Parse(bytes);
                if (parsed.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var delta = choices[0];
                    if (delta.TryGetProperty("delta", out var deltaProp) && deltaProp.TryGetProperty("content", out var contentProp))
                    {
                        var content = contentProp.GetString();
                        if (!string.IsNullOrEmpty(content))
                            AddLogEntry(engineId, AppLogLevel.Trace, $"Token: {content}");
                    }
                }
            }
            catch
            {
                AddLogEntry(engineId, AppLogLevel.Debug, $"Raw: {line}");
            }
        }
    }

    public void HandleEngineStderr(EngineType engineId, string data)
    {
        var trimmed = data.Trim();
        if (trimmed.Length > 0)
            AddLogEntry(engineId, AppLogLevel.Warn, $"stderr: {trimmed}");
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

    private void RotateLogFileIfNecessary(string logFilePath)
    {
        try
        {
            var fileInfo = new FileInfo(logFilePath);
            if (fileInfo.Length >= _config.DiskLogRotationSizeMB * 1024 * 1024)
            {
                int rotationIndex = 1;
                while (File.Exists(logFilePath + $".{rotationIndex}"))
                    rotationIndex++;
                File.Move(logFilePath, logFilePath + $".{rotationIndex}", overwrite: true);
                File.WriteAllText(logFilePath, $"[Rotated] previous {fileInfo.Length / (1024 * 1024)} MB moved to .{rotationIndex}\n");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to rotate log file {LogFile}", logFilePath);
        }
    }
}