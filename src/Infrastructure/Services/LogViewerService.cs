using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models.LLamaCpp;
using DomainLogLevel = OpenLMStudio.Domain.Models.ContextCompression.LogLevel;
using System.Collections.ObjectModel;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Manages real-time log entries with filtering, searching, and colorization.
/// Bridges EngineLogger to the UI via ObservableCollection.
/// </summary>
public class LogViewerService
{
    private readonly ILogger<LogViewerService> _logger;
    private readonly object _lock = new();
    private readonly Dictionary<EngineType, ObservableCollection<LogEntry>> _logStores = new();
    private readonly Dictionary<EngineType, List<string>> _rawBuffers = new();

    // Filters
    private LogLevel? _minLevel;
    private string? _searchText;
    private string? _categoryFilter;

    public event EventHandler<LogEntry>? LogEntryAdded;
    public event EventHandler<string>? LogCleared;

    public LogViewerService(ILogger<LogViewerService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the log entries for an engine, filtered.
    /// </summary>
    public IReadOnlyList<LogEntry> GetFilteredLogs(EngineType engine)
    {
        lock (_lock)
        {
            if (!_logStores.TryGetValue(engine, out var store))
                return Array.Empty<LogEntry>();

            return store.Where(e =>
                (_minLevel == null || e.Level >= _minLevel) &&
                (_searchText == null || e.Message.Contains(_searchText, StringComparison.OrdinalIgnoreCase)) &&
                (_categoryFilter == null || e.Category == _categoryFilter)
            ).ToList();
        }
    }

    /// <summary>
    /// Gets the raw (unfiltered) log entries for an engine.
    /// </summary>
    public IReadOnlyList<LogEntry> GetRawLogs(EngineType engine)
    {
        lock (_lock)
            return _logStores.GetValueOrDefault(engine)?.ToList() ?? Array.Empty<LogEntry>();
    }

    /// <summary>
    /// Adds a log entry from the engine.
    /// </summary>
    public void AddLogEntry(EngineType engine, LogLevel level, string message, bool isImportant = false, string? category = null)
    {
        var entry = new LogEntry(
            Id: $"log-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{engine}",
            Timestamp: DateTime.UtcNow,
            Level: level,
            Message: message,
            Source: engine,
            IsImportant: isImportant,
            Category: category);

        lock (_lock)
        {
            if (!_logStores.TryGetValue(engine, out var store))
            {
                store = new ObservableCollection<LogEntry>();
                _logStores[engine] = store;
            }

            store.Add(entry);

            // Rotate if too large
            while (store.Count > 10000)
                store.RemoveAt(0);
        }

        LogEntryAdded?.Invoke(this, entry);
    }

    /// <summary>
    /// Sets the minimum log level filter.
    /// </summary>
    public void SetMinLevel(LogLevel? level)
    {
        lock (_lock)
            _minLevel = level;
    }

    /// <summary>
    /// Sets the search text filter.
    /// </summary>
    public void SetSearchText(string? text)
    {
        lock (_lock)
            _searchText = text;
    }

    /// <summary>
    /// Sets the category filter.
    /// </summary>
    public void SetCategoryFilter(string? category)
    {
        lock (_lock)
            _categoryFilter = category;
    }

    /// <summary>
    /// Clears all logs for an engine.
    /// </summary>
    public void ClearLogs(EngineType engine)
    {
        lock (_lock)
        {
            _logStores[engine]?.Clear();
        }
        LogCleared?.Invoke(this, engine.ToString());
    }

    /// <summary>
    /// Clears all logs for all engines.
    /// </summary>
    public void ClearAllLogs()
    {
        lock (_lock)
        {
            foreach (var store in _logStores.Values)
                store.Clear();
        }
        LogCleared?.Invoke(this, "all");
    }

    /// <summary>
    /// Gets the color for a log level (for UI colorization).
    /// </summary>
    public static string GetColorForLevel(LogLevel level) => level switch
    {
        LogLevel.Trace => "#888888",
        LogLevel.Debug => "#4FC3F7",
        LogLevel.Info => "#66BB6A",
        LogLevel.Warn => "#FFA726",
        LogLevel.Error => "#EF5350",
        _ => "#CCCCCC"
    };

    /// <summary>
    /// Gets the emoji for a log level.
    /// </summary>
    public static string GetEmojiForLevel(LogLevel level) => level switch
    {
        LogLevel.Trace => "🔵",
        LogLevel.Debug => "🔵",
        LogLevel.Info => "🟢",
        LogLevel.Warn => "🟡",
        LogLevel.Error => "🔴",
        _ => "⚪"
    };
}