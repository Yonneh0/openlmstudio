using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Implements IContextRelevanceEngine for scoring segment relevance based on recency, semantic content, and entity matching.
/// </summary>
public class ContextRelevanceEngine : IContextRelevanceEngine, IDisposable
{
    private readonly ILogger<ContextRelevanceEngine>? _logger;

    public ContextRelevanceEngine(ILogger<ContextRelevanceEngine>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<RelevanceScore>> ScoreSegmentsAsync(Guid chatId, List<ContextSegment> segments, string goalText)
    {
        if (segments == null || !segments.Any() || string.IsNullOrEmpty(goalText))
            return new();

        var scores = new List<(Guid SegmentId, double Score)>();

        // Extract key terms from the goal text for semantic matching
        var goalTerms = ExtractKeyTerms(goalText);

        foreach (var segment in segments)
        {
            if (segment.IsSuppressed)
                continue; // Suppressed segments get lowest relevance regardless of content

            double score = 0;

            // --- Recency scoring: recent messages score higher ---
            var recencyScore = CalculateRecencyScore(segment, goalText);
            score += recencyScore * 0.3; // Weight recency at 30%

            // --- Semantic relevance scoring: check if segment contains key terms from goal ---
            var semanticScore = CalculateSemanticRelevance(segment.Content, goalTerms);
            score += semanticScore * 0.4; // Weight semantics at 40%

            // --- Entity matching scoring: check for mentioned entities (files, paths, code) ---
            var entityScore = ScoreEntityMatch(segment.Content, goalText);
            score += entityScore * 0.3; // Weight entity match at 30%

            scores.Add((segment.Id, Math.Clamp(score, 0, 1))); // Clamp between 0 and 1
        }

        return scores.Select(s => new RelevanceScore(s.SegmentId, s.Score)).ToList();
    }

    /// <inheritdoc />
    public double CalculateRelevanceThreshold(int conversationLength, long remainingBudget)
    {
        if (conversationLength == 0 || remainingBudget <= 0)
            return 1.0; // Only include everything when budget is unlimited or no context exists

        var budgetRatio = Math.Clamp((double)remainingBudget / 8192.0, 0.05, 1.0);

        // Dynamic threshold: higher when budget is tight, lower when we have room
        var baseThreshold = 0.3; // Default: include anything with at least 30% relevance

        if (budgetRatio > 0.7) return baseThreshold - 0.1; // More generous when lots of space
        if (budgetRatio < 0.2) return Math.Min(0.9, baseThreshold + 0.5); // Very strict when almost out

        // Scale threshold inversely with remaining budget ratio
        var adjusted = baseThreshold + (1.0 - budgetRatio) * 0.6;
        return Math.Clamp(adjusted, 0.2, 0.95);
    }

    /// <inheritdoc />
    public List<ContextSegment> OrderByRelevance(List<ContextSegment>? segments, string goalText)
    {
        if (segments == null || !segments.Any() || string.IsNullOrEmpty(goalText))
            return [];

        var scores = ScoreSegmentsAsync(Guid.Empty, segments, goalText).GetAwaiter().GetResult();

        // Map segment IDs to their relevance scores for sorting
        var scoreMap = scores.ToDictionary(s => s.SegmentId, s => s.Score);

        return segments.OrderBy(s => !scoreMap.ContainsKey(s.Id) ? 0 : scoreMap[s.Id])
                       .ThenByDescending(s => s.Role == MessageRole.User) // Break ties: user messages first
                       .ToList();
    }

    public void Dispose()
    {
        // No unmanaged resources to clean up
    }

    /// <summary>
    /// Extracts key terms from the goal text by removing stop words and keeping nouns/verbs.
    /// </summary>
    private static HashSet<string> ExtractKeyTerms(string text)
    {
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(text))
            return terms;

        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "a", "an", "is", "are", "was", "were", "be", "been", "being", "have", "has", "had",
            "do", "does", "did", "will", "would", "should", "could", "can", "may", "might", "shall",
            "to", "of", "in", "for", "on", "with", "at", "by", "from", "as", "into", "through",
            "and", "but", "or", "nor", "not", "that", "this", "these", "those", "it", "its", "my",
            "your", "his", "her", "our", "their", "we", "they", "he", "she", "i", "me", "you"
        };

        var words = text.Split(new[] { ' ', '.', ',', ':', ';', '?', '!', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            var cleanWord = word.Trim().ToLowerInvariant();

            // Skip stop words and very short words
            if (cleanWord.Length < 3 || stopWords.Contains(cleanWord))
                continue;

            terms.Add(cleanWord);
        }

        return terms;
    }

    /// <summary>
    /// Calculates recency score based on segment position relative to goal text proximity.
    /// More recent segments score higher (0-1 scale).
    /// </summary>
    private static double CalculateRecencyScore(ContextSegment segment, string goalText)
    {
        // User messages near the query get highest recency bonus
        var userProximity = segment.Role == MessageRole.User ? 0.3 : 0;

        // Recent segments (shorter content = likely more recent in context window ordering)
        var lengthBasedScore = Math.Max(0, 1.0 - segment.Content.Length / 5000.0);

        return userProximity + lengthBasedScore * 0.7;
    }

    /// <summary>
    /// Calculates semantic relevance by checking if segment content contains key terms from the goal.
    /// Returns score between 0 and 1.
    /// </summary>
    private static double CalculateSemanticRelevance(string content, HashSet<string> goalTerms)
    {
        if (string.IsNullOrEmpty(content) || !goalTerms.Any())
            return 0;

        var lowerContent = content.ToLowerInvariant();
        int? termHits = null;
        double weightedScore = 0;

        foreach (var term in goalTerms)
        {
            // Count occurrences of each term in the segment
            var count = CountSubstrings(lowerContent, term);

            if (count > 0)
                weightedScore += term.Length * count; // Weight longer terms more heavily

            if (count > 0)
                termHits = termHits.GetValueOrDefault(0) + 1;
        }

        if (termHits == null || termHits <= 0)
            return 0;

        // Normalize: divide by number of terms found, then scale to 0-1 range
        var normalized = weightedScore / Math.Max(goalTerms.Count * goalTerms.Max(t => t.Length), 1);

        // Exponential decay for high values (diminishing returns)
        return Math.Clamp(normalized / Math.Sqrt(1 + normalized), 0, 1);
    }

    /// <summary>
    /// Scores entity matches between segment content and goal text.
    /// Returns score based on presence of file paths, code identifiers, and technical terms.
    /// </summary>
    private static double ScoreEntityMatch(string content, string goalText)
    {
        if (string.IsNullOrEmpty(content))
            return 0;

        var lowerContent = content.ToLowerInvariant();

        // Look for file paths in the segment that match with the goal text
        var pathPatternMatches = CountSubstrings(lowerContent, "/") + CountSubstrings(lowerContent, "\\");
        if (pathPatternMatches > 0)
            return Math.Clamp(pathPatternMatches / 5.0, 0, 1);

        // Look for code identifiers that appear in both content and goal text
        var codeTerms = ExtractCodeIdentifiers(content);
        var goalCodeTerms = ExtractCodeIdentifiers(goalText);

        int matchedTerms = 0;
        foreach (var term in codeTerms)
        {
            if (goalCodeTerms.Contains(term, StringComparer.OrdinalIgnoreCase))
                matchedTerms++;
        }

        var maxCount = codeTerms.Count > goalCodeTerms.Count ? codeTerms.Count : goalCodeTerms.Count;
        if (maxCount == 0) return 0;

        return Math.Clamp((double)matchedTerms / maxCount, 0, 1);
    }

    private static int CountSubstrings(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle))
            return 0;

        var count = 0;
        var index = 0;

        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    /// <summary>
    /// Extracts code identifiers (camelCase, PascalCase, snake_case words) from text.
    /// </summary>
    private static HashSet<string> ExtractCodeIdentifiers(string text)
    {
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(text))
            return terms;

        // Match camelCase and PascalCase identifiers
        var camelMatch = System.Text.RegularExpressions.Regex.Matches(text, "[a-z]+[A-Z][a-zA-Z]*");
        foreach (var match in camelMatch)
        {
            var m = match.ToString();
            if (!string.IsNullOrEmpty(m))
                terms.Add(m);
        }

        // Match snake_case identifiers  
        var snakeMatch = System.Text.RegularExpressions.Regex.Matches(text, "[a-z]+_[a-z]+");
        foreach (var match in snakeMatch)
        {
            var m = match.ToString();
            if (!string.IsNullOrEmpty(m))
                terms.Add(m.ToLowerInvariant());
        }

        return terms;
    }
}