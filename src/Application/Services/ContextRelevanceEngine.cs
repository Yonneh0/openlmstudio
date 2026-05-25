namespace OpenLMStudio.Application.Services;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

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