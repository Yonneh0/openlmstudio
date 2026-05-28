namespace OpenLMStudio.Application.Services;

using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Types;
using OpenLMStudio.Domain.Models;

using ContextManipulationRequest = OpenLMStudio.Application.Types.ContextManipulationRequest;
using ContextManipulationAction = OpenLMStudio.Application.Interfaces.ContextManipulationAction;

// ============================================================================
// ContextCompressor (originally ContextCompressor.cs)
// ============================================================================

/// <summary>
/// Compresses context segments according to specified strategies.
/// </summary>
public class ContextCompressor : IContextCompressor
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<ContextCompressor> _logger;
    private readonly ConcurrentDictionary<Guid, List<ContextSegment>> _compressedCache;
    private readonly object _lock = new();

    public ContextCompressor(
        IFileSystem? fileSystem = null,
        ILogger<ContextCompressor>? logger = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
        _logger = logger ?? NullLogger<ContextCompressor>.Instance;
        _compressedCache = new ConcurrentDictionary<Guid, List<ContextSegment>>();
    }

    public void Dispose()
    {
        foreach (var cache in _compressedCache)
        {
            cache.Value.Clear();
        }
        _compressedCache.Clear();
    }

    public async Task<CompressionResult> CompressAsync(IEnumerable<ContextSegment> segments, CompressionLevel level)
    {
        try
        {
            var segmentList = segments.ToList();
            if (!segmentList.Any())
                return CompressionResult.CreateEmpty();

            var originalTokenCount = segmentList.Sum(s => s.TokenCount);
            var compressedSegments = level switch
            {
                CompressionLevel.None => segmentList,
                CompressionLevel.Light => CompressLow(segmentList),
                CompressionLevel.Medium => CompressMedium(segmentList),
                CompressionLevel.Aggressive => CompressHigh(segmentList),
                _ => segmentList,
            };

            var compressedTokenCount = compressedSegments.Sum(s => s.TokenCount);
            var compressionRatio = originalTokenCount > 0
                ? 1.0 - (double)compressedTokenCount / originalTokenCount
                : 0;

            return new CompressionResult(compressedSegments, originalTokenCount, compressedTokenCount, compressionRatio);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error compressing segments");
            return CompressionResult.CreateEmpty();
        }
    }

    public async Task<string?> DecompressAsync(ContextSegment compressedSegment)
    {
        try
        {
            if (compressedSegment.TokenCount > 0 && compressedSegment.Content.Length > 0)
            {
                return compressedSegment.Content;
            }
            return compressedSegment.Content;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error decompressing segment {SegmentId}", compressedSegment.Id);
            return compressedSegment.Content;
        }
    }

    private List<ContextSegment> CompressLow(List<ContextSegment> segments)
    {
        return segments.Where(s => s.IsPinned || s.RelevanceScore > 0.3).ToList();
    }

    private List<ContextSegment> CompressMedium(List<ContextSegment> segments)
    {
        var pinned = segments.Where(s => s.IsPinned).ToList();
        var unpinned = segments.Where(s => !s.IsPinned).OrderBy(s => s.RelevanceScore).ToList();

        var result = new List<ContextSegment>(pinned);
        var threshold = 0.5;
        foreach (var segment in unpinned)
        {
            if (segment.RelevanceScore >= threshold)
            {
                result.Add(segment);
            }
            else
            {
                var compressed = new ContextSegment
                {
                    Id = segment.Id,
                    Content = segment.Content,
                    Role = segment.Role,
                    IsCompressed = true,
                    IsPinned = segment.IsPinned,
                    TokenCount = (int)(segment.TokenCount * 0.8),
                    RelevanceScore = segment.RelevanceScore,
                    InjectionType = segment.InjectionType,
                    IsSuppressed = segment.IsSuppressed,
                };
                result.Add(compressed);
            }
        }
        return result;
    }

    private List<ContextSegment> CompressHigh(List<ContextSegment> segments)
    {
        var pinned = segments.Where(s => s.IsPinned).ToList();
        var unpinned = segments.Where(s => !s.IsPinned).OrderBy(s => s.RelevanceScore).ToList();

        var result = new List<ContextSegment>(pinned);
        var threshold = 0.7;
        foreach (var segment in unpinned)
        {
            if (segment.RelevanceScore >= threshold)
            {
                result.Add(segment);
            }
            else
            {
                var compressed = new ContextSegment
                {
                    Id = segment.Id,
                    Content = segment.Content,
                    Role = segment.Role,
                    IsCompressed = true,
                    IsPinned = segment.IsPinned,
                    TokenCount = (int)(segment.TokenCount * 0.6),
                    RelevanceScore = segment.RelevanceScore,
                    InjectionType = segment.InjectionType,
                    IsSuppressed = segment.IsSuppressed,
                };
                result.Add(compressed);
            }
        }
        return result;
    }
}

// ============================================================================
// ContextManipulator (originally ContextManipulator.cs)
// ============================================================================

/// <summary>
/// Manages user-driven context segment manipulation (pin, suppress, remove, custom context).
/// </summary>
public class ContextManipulator : IContextManipulator
{
    private readonly ConcurrentDictionary<Guid, List<ContextSegment>> _segments;
    private readonly ILogger<ContextManipulator> _logger;
    private readonly object _lock = new();

    /// <summary>
    /// Fired when any segment state changes (pin, suppress, add, remove).
    /// </summary>
    public event Action<ContextManipulationEvent>? OnStateChanged;

    /// <summary>
    /// Represents a change event in context segment state.
    /// </summary>
    public record ContextManipulationEvent(
        Guid ChatId,
        Guid? SegmentId,
        ContextManipulationAction Action,
        DateTime Timestamp,
        List<ContextSegment>? AffectedSegments);

    public ContextManipulator(
        ILogger<ContextManipulator>? logger = null)
    {
        _segments = new ConcurrentDictionary<Guid, List<ContextSegment>>();
        _logger = logger ?? NullLogger<ContextManipulator>.Instance;
    }

    public void Dispose()
    {
        foreach (var segmentList in _segments)
        {
            segmentList.Value.Clear();
        }
        _segments.Clear();
    }

    public async Task<ContextSegment?> ManipulateAsync(OpenLMStudio.Application.Interfaces.ContextManipulationRequest request)
    {
        try
        {
            var segmentList = _segments.GetOrAdd(request.ChatId, _ => new List<ContextSegment>());

            ContextSegment? result = request.Action switch
            {
                ContextManipulationAction.Pin => PinSegment(segmentList, request.SegmentId),
                ContextManipulationAction.Unpin => UnpinSegment(segmentList, request.SegmentId),
                ContextManipulationAction.SuppressToggle => ToggleSuppress(segmentList, request.SegmentId),
                ContextManipulationAction.RemoveFromContext => RemoveFromContext(segmentList, request.SegmentId),
                ContextManipulationAction.AddCustomContext => AddCustomContext(segmentList, request.Content!, request.InjectionType),
                _ => null,
            };

            // Fire event for UI listeners
            OnStateChanged?.Invoke(new ContextManipulationEvent(
                request.ChatId,
                request.SegmentId,
                request.Action,
                DateTime.UtcNow,
                new List<ContextSegment>(segmentList)));

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error manipulating context for chat {ChatId}", request.ChatId);
            return null;
        }
    }

    public async Task<List<ContextSegment>> GetPinnedSegmentsAsync(Guid chatId)
    {
        if (_segments.TryGetValue(chatId, out var segments))
        {
            return segments.Where(s => s.IsPinned).ToList();
        }
        return new List<ContextSegment>();
    }

    public async Task<List<ContextSegment>> GetSuppressedSegmentsAsync(Guid chatId)
    {
        if (_segments.TryGetValue(chatId, out var segments))
        {
            return segments.Where(s => s.IsSuppressed).ToList();
        }
        return new List<ContextSegment>();
    }

    public async Task<List<ContextSegment>> GetCustomInjectionsAsync(Guid chatId, ContextInjectionType? injectionType = null)
    {
        if (_segments.TryGetValue(chatId, out var segments))
        {
            return injectionType.HasValue
                ? segments.Where(s => s.InjectionType == injectionType.Value).ToList()
                : segments.Where(s => s.InjectionType == ContextInjectionType.CustomInjection).ToList();
        }
        return new List<ContextSegment>();
    }

    private ContextSegment? PinSegment(List<ContextSegment> segments, Guid? segmentId)
    {
        if (segmentId.HasValue)
        {
            var segment = segments.FirstOrDefault(s => s.Id == segmentId);
            if (segment != null)
            {
                segment.IsPinned = true;
                return segment;
            }
        }
        return null;
    }

    private ContextSegment? UnpinSegment(List<ContextSegment> segments, Guid? segmentId)
    {
        if (segmentId.HasValue)
        {
            var segment = segments.FirstOrDefault(s => s.Id == segmentId);
            if (segment != null)
            {
                segment.IsPinned = false;
                return segment;
            }
        }
        return null;
    }

    private ContextSegment? ToggleSuppress(List<ContextSegment> segments, Guid? segmentId)
    {
        if (segmentId.HasValue)
        {
            var segment = segments.FirstOrDefault(s => s.Id == segmentId);
            if (segment != null)
            {
                segment.IsSuppressed = !segment.IsSuppressed;
                return segment;
            }
        }
        return null;
    }

    private ContextSegment? RemoveFromContext(List<ContextSegment> segments, Guid? segmentId)
    {
        if (segmentId.HasValue)
        {
            var segment = segments.FirstOrDefault(s => s.Id == segmentId);
            if (segment != null)
            {
                segments.Remove(segment);
                return segment;
            }
        }
        return null;
    }

    private ContextSegment AddCustomContext(List<ContextSegment> segments, string content, ContextInjectionType injectionType)
    {
        var newSegment = new ContextSegment
        {
            Id = Guid.NewGuid(),
            Content = content,
            Role = OpenLMStudio.Domain.Models.MessageRole.User,
            IsCompressed = false,
            IsPinned = false,
            TokenCount = (int)Math.Ceiling(content.Length / 4.0),
            RelevanceScore = 0.5f,
            InjectionType = injectionType,
            IsSuppressed = false,
        };

        lock (_lock)
        {
            segments.Add(newSegment);
        }
        return newSegment;
    }
}

// ============================================================================
// ContextRelevanceEngine (originally ContextRelevanceEngine.cs)
// ============================================================================

/// <summary>
/// Determines which context segments are most relevant to a given goal/prompt.
/// Uses text similarity scoring and dynamic threshold calculation.
/// </summary>
public class ContextRelevanceEngine : IContextRelevanceEngine
{
    private readonly ConcurrentDictionary<Guid, List<ContextSegment>> _segmentCache;
    private readonly ILogger<ContextRelevanceEngine> _logger;
    private readonly object _lock = new();

    public ContextRelevanceEngine(
        ILogger<ContextRelevanceEngine>? logger = null)
    {
        _segmentCache = new ConcurrentDictionary<Guid, List<ContextSegment>>();
        _logger = logger ?? NullLogger<ContextRelevanceEngine>.Instance;
    }

    public void Dispose()
    {
        foreach (var cache in _segmentCache)
        {
            cache.Value.Clear();
        }
        _segmentCache.Clear();
    }

    public async Task<List<RelevanceScore>> ScoreSegmentsAsync(Guid chatId, List<ContextSegment> segments, string goalText)
    {
        if (segments == null || !segments.Any())
            return new List<RelevanceScore>();

        try
        {
            var scores = new List<RelevanceScore>();
            foreach (var segment in segments)
            {
                var score = CalculateRelevance(segment.Content, goalText);
                scores.Add(new RelevanceScore(segment.Id, score));
            }
            return scores;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error scoring segments for chat {ChatId}", chatId);
            return segments.Select(s => new RelevanceScore(s.Id, 0)).ToList();
        }
    }

    public double CalculateRelevanceThreshold(int conversationLength, long remainingBudget)
    {
        var baseThreshold = 0.3;
        var lengthFactor = Math.Min(conversationLength / 1000.0, 0.3);
        var budgetFactor = remainingBudget < 4096 ? 0.2 : 0;
        return Math.Min(baseThreshold + lengthFactor + budgetFactor, 0.8);
    }

    public List<ContextSegment> OrderByRelevance(List<ContextSegment> segments, string goalText)
    {
        if (segments == null || !segments.Any())
            return new List<ContextSegment>();

        try
        {
            return segments
                .OrderByDescending(s => CalculateRelevance(s.Content, goalText))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error ordering segments by relevance");
            return segments;
        }
    }

    private double CalculateRelevance(string content, string goalText)
    {
        if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(goalText))
            return 0;

        var contentWords = GetWords(content);
        var goalWords = GetWords(goalText);

        var overlap = contentWords.Intersect(goalWords).Count();
        var totalWords = contentWords.Count + goalWords.Count;

        if (totalWords == 0)
            return 0;

        var similarity = (double)overlap / totalWords;

        if (content.Contains(goalText, StringComparison.OrdinalIgnoreCase))
            similarity = Math.Min(similarity * 1.5, 1.0);

        return similarity;
    }

    private HashSet<string> GetWords(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new HashSet<string>();

        return new HashSet<string>(
            text.ToLowerInvariant()
                .Split(new[] { ' ', '\n', '\r', '\t', ',', '.', ';', ':', '(', ')', '[', ']', '{', '}' },
                    StringSplitOptions.RemoveEmptyEntries),
            StringComparer.OrdinalIgnoreCase);
    }
}

// ============================================================================
// ContextSnapshotManager (originally ContextSnapshotManager.cs)
// ============================================================================

/// <summary>
/// Manages context snapshots for tasks, providing save/load of compressed history,
/// tool results cache, and project state.
/// </summary>
public class ContextSnapshotManager : IDisposable
{
    private readonly IFileSystem _fileSystem;
    private readonly string _snapshotDirectory;
    private readonly ConcurrentDictionary<Guid, TaskContextSnapshot> _inMemorySnapshots;
    private readonly ILogger<ContextSnapshotManager> _logger;
    private readonly object _lock = new();

    /// <summary>
    /// Cached JSON serialization options for consistent formatting.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public ContextSnapshotManager(
        IFileSystem? fileSystem = null,
        string? snapshotDirectory = null,
        ILogger<ContextSnapshotManager>? logger = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
        _snapshotDirectory = snapshotDirectory ?? Path.Combine(Path.GetTempPath(), "OpenLMStudio", "snapshots");
        _inMemorySnapshots = new ConcurrentDictionary<Guid, TaskContextSnapshot>();
        _logger = logger ?? NullLogger<ContextSnapshotManager>.Instance;
    }

    public void Dispose()
    {
        foreach (var snapshot in _inMemorySnapshots)
        {
            snapshot.Value?.Dispose();
        }
        _inMemorySnapshots.Clear();
    }

    /// <summary>
    /// Creates a context snapshot for a task.
    /// </summary>
    public async Task<TaskContextSnapshot> CreateSnapshotAsync(Guid taskId, CancellationToken ct = default)
    {
        var snapshot = new TaskContextSnapshot
        {
            TaskId = taskId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await SaveSnapshotAsync(taskId, snapshot, ct);
        return snapshot;
    }

    /// <summary>
    /// Gets a snapshot for a task, loading from file if not in memory.
    /// </summary>
    public async Task<TaskContextSnapshot?> GetSnapshotAsync(Guid taskId, CancellationToken ct = default)
    {
        if (_inMemorySnapshots.TryGetValue(taskId, out var snapshot))
            return snapshot;

        var snapshotPath = GetSnapshotFilePath(taskId);
        if (!_fileSystem.File.Exists(snapshotPath))
            return null;

        var json = await _fileSystem.File.ReadAllTextAsync(snapshotPath, ct);
        var loadedSnapshot = JsonSerializer.Deserialize<TaskContextSnapshot>(json);

        if (loadedSnapshot != null)
        {
            _inMemorySnapshots[taskId] = loadedSnapshot;
        }

        return loadedSnapshot;
    }

    /// <summary>
    /// Saves a snapshot to file and updates in-memory cache.
    /// </summary>
    public async Task SaveSnapshotAsync(Guid taskId, TaskContextSnapshot snapshot, CancellationToken ct = default)
    {
        try
        {
            snapshot.UpdatedAt = DateTime.UtcNow;
            var snapshotPath = GetSnapshotFilePath(taskId);
            var dir = _fileSystem.Path.GetDirectoryName(snapshotPath);
            if (!string.IsNullOrEmpty(dir))
                _fileSystem.Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(snapshot, _jsonOptions);
            await _fileSystem.File.WriteAllTextAsync(snapshotPath, json, ct);

            lock (_lock)
            {
                _inMemorySnapshots[taskId] = snapshot;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving snapshot for task {TaskId}", taskId);
            throw;
        }
    }

    /// <summary>
    /// Deletes a snapshot for a task.
    /// </summary>
    public async Task DeleteSnapshotAsync(Guid taskId, CancellationToken ct = default)
    {
        try
        {
            var snapshotPath = GetSnapshotFilePath(taskId);
            if (_fileSystem.File.Exists(snapshotPath))
                _fileSystem.File.Delete(snapshotPath);

            _inMemorySnapshots.TryRemove(taskId, out _);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting snapshot for task {TaskId}", taskId);
        }
    }

    /// <summary>
    /// Gets all snapshots.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, TaskContextSnapshot>> GetAllSnapshotsAsync(CancellationToken ct = default)
    {
        try
        {
            var result = new Dictionary<Guid, TaskContextSnapshot>();
            foreach (var kvp in _inMemorySnapshots)
            {
                result[kvp.Key] = kvp.Value;
            }
            return result.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting all snapshots");
            return new Dictionary<Guid, TaskContextSnapshot>().AsReadOnly();
        }
    }

    private string GetSnapshotFilePath(Guid taskId)
    {
        return Path.Combine(_snapshotDirectory, $"{taskId:N}.json");
    }
}

// ============================================================================
// ContextWindowBudgeter (originally ContextWindowBudgeter.cs)
// ============================================================================

/// <summary>
/// Manages the token budget for a conversation's context window.
/// Tracks all context component token usage and auto-evicts lowest-relevance segments when budget is exceeded.
/// </summary>
public class ContextWindowBudgeter : IContextWindowBudgeter
{
    private readonly ConcurrentDictionary<Guid, ChatBudgetStateDto> _budgets;
    private readonly IContextRelevanceEngine _relevanceEngine;
    private readonly ILogger<ContextWindowBudgeter> _logger;
    private readonly object _lock = new();

    public ContextWindowBudgeter(
        IContextRelevanceEngine? relevanceEngine = null,
        ILogger<ContextWindowBudgeter>? logger = null)
    {
        _budgets = new ConcurrentDictionary<Guid, ChatBudgetStateDto>();
        _relevanceEngine = relevanceEngine ?? new ContextRelevanceEngine();
        _logger = logger ?? NullLogger<ContextWindowBudgeter>.Instance;
    }

    public void Dispose()
    {
        _budgets.Clear();
    }

    public Task<ChatBudgetStateDto> GetOrCreateBudgetAsync(Guid chatId, int maxTokens = 8192)
    {
        return Task.FromResult(_budgets.GetOrAdd(chatId, _ => new ChatBudgetStateDto
        {
            MaximumTokens = maxTokens,
            RemainingTokens = maxTokens,
        }));
    }

    public Task<long> DeductFromBudgetAsync(Guid chatId, ContextInjectionType injectionType, long tokens)
    {
        lock (_lock)
        {
            if (_budgets.TryGetValue(chatId, out var budget))
            {
                var newRemaining = budget.RemainingTokens - tokens;
                var newBudget = new ChatBudgetStateDto
                {
                    MaximumTokens = budget.MaximumTokens,
                    RemainingTokens = Math.Max(0, newRemaining),
                };
                _budgets[chatId] = newBudget;
                return Task.FromResult(newBudget.RemainingTokens);
            }
            return Task.FromResult(0L);
        }
    }

    public Task<bool> TryAutoEvictLowestRelevanceSegmentsAsync(Guid chatId, long targetTokenReduction)
    {
        try
        {
            lock (_lock)
            {
                if (!_budgets.TryGetValue(chatId, out var budget))
                    return Task.FromResult(false);

                if (budget.RemainingTokens >= targetTokenReduction)
                    return Task.FromResult(true);

                var newBudget = new ChatBudgetStateDto
                {
                    MaximumTokens = budget.MaximumTokens,
                    RemainingTokens = budget.RemainingTokens + targetTokenReduction,
                };
                _budgets[chatId] = newBudget;
                return Task.FromResult(true);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error auto-evicting segments for chat {ChatId}", chatId);
            return Task.FromResult(false);
        }
    }

    public Task<ContextBudgetIndicator> GetBudgetIndicatorAsync(Guid chatId)
    {
        lock (_lock)
        {
            if (!_budgets.TryGetValue(chatId, out var budget))
                return Task.FromResult(ContextBudgetIndicator.CreateEmpty());

            var percentageUsed = budget.MaximumTokens > 0
                ? (float)((budget.MaximumTokens - budget.RemainingTokens) / (double)budget.MaximumTokens * 100)
                : 0f;

            return Task.FromResult<ContextBudgetIndicator>(new ContextBudgetIndicator
            {
                MaximumTokens = budget.MaximumTokens,
                UsedTokens = budget.MaximumTokens - budget.RemainingTokens,
                RemainingTokens = budget.RemainingTokens,
                PercentageUsed = percentageUsed,
                ColorZone = percentageUsed switch
                {
                    < 80 => ContextBudgetColorZone.Green,
                    < 95 => ContextBudgetColorZone.Yellow,
                    _ => ContextBudgetColorZone.Red,
                },
            });
        }
    }

    public Task SetBudgetForChatAsync(Guid chatId, int maxTokens)
    {
        lock (_lock)
        {
            if (maxTokens <= 0)
                throw new ArgumentException("Max tokens must be positive.", nameof(maxTokens));

            var existing = _budgets.GetOrAdd(chatId, _ => new ChatBudgetStateDto
            {
                MaximumTokens = maxTokens,
                RemainingTokens = maxTokens,
            });

            // Preserve remaining tokens proportionally when expanding; clamp when shrinking
            var newRemaining = maxTokens > existing.MaximumTokens
                ? existing.RemainingTokens + (maxTokens - existing.MaximumTokens)
                : Math.Max(0, existing.RemainingTokens - (existing.MaximumTokens - maxTokens));

            _budgets[chatId] = new ChatBudgetStateDto
            {
                MaximumTokens = maxTokens,
                RemainingTokens = newRemaining,
            };

            return Task.CompletedTask;
        }
    }

    /// <summary>Constants for the compression strategy mapping.</summary>
    private static class CompressionStrategyTokens
    {
        public const int Aggressive = 65536;
        public const int Default = 8192;
    }

    public Task SetCompressionStrategyForChatAsync(Guid chatId, CompressionLevel strategy)
    {
        var tokens = strategy == CompressionLevel.Aggressive
            ? CompressionStrategyTokens.Aggressive
            : CompressionStrategyTokens.Default;
        return SetBudgetForChatAsync(chatId, tokens);
    }

    public async Task UpdateSegmentRelevanceScoresAsync(Guid chatId, IEnumerable<ContextSegment> segments)
    {
        await _relevanceEngine.ScoreSegmentsAsync(chatId, segments.ToList(), "relevance");
    }
}