using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Defines a relevance score for context segments relative to a given goal/prompt.
/// </summary>
public record RelevanceScore(
    Guid SegmentId,
    double Score)  // 0 = irrelevant, 1 = highly relevant
{
    public bool IsRelevant(double threshold = 0.3) => Score >= threshold;

    public static RelevanceScore CreateIrrelevant(Guid segmentId) => new(segmentId, 0);
}


/// <summary>
/// Determines which context segments are most relevant to a given goal/prompt.
/// </summary>
public interface IContextRelevanceEngine : IDisposable
{
    /// <summary>
    /// Scores each segment's relevance relative to the given query/goal text.
    /// Higher scores indicate more relevance — used for ordering and eviction decisions.
    /// </summary>
    Task<List<RelevanceScore>> ScoreSegmentsAsync(Guid chatId, List<ContextSegment> segments, string goalText);

    /// <summary>
    /// Dynamically adjusts the relevance threshold based on conversation length and available token budget.
    /// Returns a threshold between 0 (include everything) and 1 (only highest-relevance).
    /// </summary>
    double CalculateRelevanceThreshold(int conversationLength, long remainingBudget);

    /// <summary>
    /// Reorders segments by relevance score — most relevant first for maximum impact.
    /// </summary>
    List<ContextSegment> OrderByRelevance(List<ContextSegment> segments, string goalText);
}
