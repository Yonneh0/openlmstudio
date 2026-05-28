namespace OpenLMStudio.Application.Services;

using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

/// <summary>
/// Manages agent task context (truncation, summarization, file read cache).
/// </summary>
public class AgentTaskContextManager : IAgentTaskContextManager
{
    private readonly IFileSystem _fileSystem;
    private readonly string _contextDirectory;
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, string>> _fileReadCaches;
    private readonly ConcurrentDictionary<Guid, ContextBudget> _contextBuckets;
    private readonly ILogger<AgentTaskContextManager> _logger;
    private readonly object _lock = new();

    /// <summary>
    /// Cached JSON serialization options for consistent formatting.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public AgentTaskContextManager(
        IFileSystem? fileSystem = null,
        string? contextDirectory = null,
        ILogger<AgentTaskContextManager>? logger = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
        _contextDirectory = contextDirectory ?? Path.Combine(Path.GetTempPath(), "OpenLMStudio", "context");
        _fileReadCaches = new ConcurrentDictionary<Guid, ConcurrentDictionary<string, string>>();
        _contextBuckets = new ConcurrentDictionary<Guid, ContextBudget>();
        _logger = logger ?? NullLogger<AgentTaskContextManager>.Instance;
    }

    public void Dispose()
    {
        foreach (var cache in _fileReadCaches)
        {
            cache.Value?.Clear();
        }
        _fileReadCaches.Clear();
        _contextBuckets.Clear();
    }

    public async Task TruncateHistoryAsync(Guid taskId, int maxMessages, CancellationToken ct = default)
    {
        try
        {
            var historyPath = GetHistoryFilePath(taskId);
            if (!_fileSystem.File.Exists(historyPath))
                return;

            var history = await _fileSystem.File.ReadAllTextAsync(historyPath, ct);
            var segments = JsonSerializer.Deserialize<List<ContextSegment>>(history);
            if (segments != null && segments.Count > maxMessages)
            {
                var truncated = segments.Skip(segments.Count - maxMessages).ToList();
                var json = JsonSerializer.Serialize(truncated, _jsonOptions);
                await _fileSystem.File.WriteAllTextAsync(historyPath, json, ct);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error truncating history for task {TaskId}", taskId);
        }
    }

    public async Task SummarizeHistoryAsync(Guid taskId, CancellationToken ct = default)
    {
        try
        {
            var summaryPath = GetSummaryFilePath(taskId);
            var historyPath = GetHistoryFilePath(taskId);

            if (!_fileSystem.File.Exists(historyPath))
                return;

            var history = await _fileSystem.File.ReadAllTextAsync(historyPath, ct);
            var segments = JsonSerializer.Deserialize<List<ContextSegment>>(history);
            if (segments != null)
            {
                var summary = $"Summarized {segments.Count} messages";
                await _fileSystem.File.WriteAllTextAsync(summaryPath, summary, ct);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error summarizing history for task {TaskId}", taskId);
        }
    }

    public async Task<string?> GetCachedFileAsync(Guid taskId, string filePath, CancellationToken ct = default)
    {
        var cache = _fileReadCaches.GetOrAdd(taskId, _ => new ConcurrentDictionary<string, string>());
        return cache.TryGetValue(filePath, out var content) ? content : null;
    }

    public async Task SetCachedFileAsync(Guid taskId, string filePath, string content, CancellationToken ct = default)
    {
        var cache = _fileReadCaches.GetOrAdd(taskId, _ => new ConcurrentDictionary<string, string>());
        cache[filePath] = content;
    }

    public async Task ClearFileCacheAsync(Guid taskId, CancellationToken ct = default)
    {
        if (_fileReadCaches.TryRemove(taskId, out var cache))
        {
            cache.Clear();
        }
    }

    public async Task UpdateFileCacheAsync(Guid taskId, Dictionary<string, string> cache, CancellationToken ct = default)
    {
        var existingCache = _fileReadCaches.GetOrAdd(taskId, _ => new ConcurrentDictionary<string, string>());
        foreach (var kvp in cache)
        {
            existingCache[kvp.Key] = kvp.Value;
        }
    }

    public async Task<ContextBudget> GetContextBudgetAsync(Guid taskId, CancellationToken ct = default)
    {
        return _contextBuckets.GetOrAdd(taskId, _ => ContextBudget.CreateDefault());
    }

    public async Task SetContextBudgetAsync(Guid taskId, ContextBudget budget, CancellationToken ct = default)
    {
        _contextBuckets[taskId] = budget;
    }

    private string GetHistoryFilePath(Guid taskId)
    {
        return Path.Combine(_contextDirectory, $"{taskId:N}_history.json");
    }

    private string GetSummaryFilePath(Guid taskId)
    {
        return Path.Combine(_contextDirectory, $"{taskId:N}_summary.txt");
    }
}