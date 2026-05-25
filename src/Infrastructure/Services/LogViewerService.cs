using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Application.Interfaces;
using DomainLogLevel = OpenLMStudio.Domain.Models.LogLevel;
using DomainEngineType = OpenLMStudio.Application.Interfaces.EngineType;
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
    private readonly Dictionary<DomainEngineType, ObservableCollection<LogEntry>> _logStores = new();
    private readonly Dictionary<DomainEngineType, List<string>> _rawBuffers = new();

    // Filters
    private DomainLogLevel? _minLevel;
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
    public IReadOnlyList<LogEntry> GetFilteredLogs(DomainEngineType engine)
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
    public IReadOnlyList<LogEntry> GetRawLogs(DomainEngineType engine)
    {
        lock (_lock)
        {
            if (_logStores.TryGetValue(engine, out var store))
                return store.ToList();
            return Array.Empty<LogEntry>();
        }
    }

    /// <summary>
    /// Adds a log entry from the engine.
    /// </summary>
    public void AddLogEntry(DomainEngineType engine, DomainLogLevel level, string message, bool isImportant = false, string? category = null)
    {
        var domainEngine = engine switch
        {
            DomainEngineType.Primary => OpenLMStudio.Domain.Models.EngineType.Primary,
            DomainEngineType.SystemAI => OpenLMStudio.Domain.Models.EngineType.SystemAI,
            DomainEngineType.Diffusion => OpenLMStudio.Domain.Models.EngineType.Diffusion,
            DomainEngineType.Embedding => OpenLMStudio.Domain.Models.EngineType.Embedding,
            _ => OpenLMStudio.Domain.Models.EngineType.Primary
        };

        var entry = new LogEntry(
            Id: $"log-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{engine}",
            Timestamp: DateTime.UtcNow,
            Level: level,
            Message: message,
            Source: domainEngine,
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
    public void SetMinLevel(DomainLogLevel? level)
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
    public void ClearLogs(DomainEngineType engine)
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
    public static string GetColorForLevel(DomainLogLevel level) => level switch
    {
        DomainLogLevel.Trace => "#888888",
        DomainLogLevel.Debug => "#4FC3F7",
        DomainLogLevel.Info => "#66BB6A",
        DomainLogLevel.Warn => "#FFA726",
        DomainLogLevel.Error => "#EF5350",
        _ => "#CCCCCC"
    };

    /// <summary>
    /// Gets the emoji for a log level.
    /// </summary>
    public static string GetEmojiForLevel(DomainLogLevel level) => level switch
    {
        DomainLogLevel.Trace => "\U0001F535",
        DomainLogLevel.Debug => "\U0001F535",
        DomainLogLevel.Info => "\U0001F7E2",
        DomainLogLevel.Warn => "\U0001F7E1",
        DomainLogLevel.Error => "\U0001F534",
        _ => "\u26AA"
    };
}
