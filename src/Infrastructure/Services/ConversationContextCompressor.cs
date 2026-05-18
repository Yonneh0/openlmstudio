using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Implements IContextCompressor for conversation-level context compression.
/// Uses multiple strategies: temporal decay, semantic relevance scoring, and tool output condensation.
/// </summary>
public class ConversationContextCompressor : IContextCompressor, IDisposable
{
    private readonly ILogger<ConversationContextCompressor>? _logger;

    public ConversationContextCompressor(ILogger<ConversationContextCompressor>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CompressionResult> CompressAsync(IEnumerable<ContextSegment> segments, CompressionLevel level)
    {
        if (level == CompressionLevel.None)
            return new CompressionResult(segments.ToList(), 
                segments.Sum(s => s.TokenCount), 
                segments.Sum(s => s.TokenCount), 
                0);

        var segmentList = segments.ToList();
        if (!segmentList.Any())
            return CompressionResult.CreateEmpty();

        // Separate pinned vs. non-pinned segments (pinned are never compressed)
        var pinnedSegments = segmentList.Where(s => s.IsPinned).ToList();
        var compressibleSegments = segmentList.Where(s => !s.IsPinned && !s.IsSuppressed).ToList();

        if (!compressibleSegments.Any())
            return new CompressionResult(pinnedSegments, 
                pinnedSegments.Sum(s => s.TokenCount), 
                pinnedSegments.Sum(s => s.TokenCount), 
                0);

        // Sort compressible segments by relevance (most recent first) for temporal decay
        var sorted = SortByRelevance(compressibleSegments).ToList();

        long originalTokenCount = segmentList.Sum(s => s.TokenCount);

        List<ContextSegment> compressed;
        switch (level)
        {
            case CompressionLevel.Light:
                compressed = ApplyLightCompression(sorted, pinnedSegments);
                break;
            case CompressionLevel.Medium:
                compressed = ApplyMediumCompression(sorted, pinnedSegments);
                break;
            case CompressionLevel.Aggressive:
                compressed = ApplyAggressiveCompression(sorted, pinnedSegments);
                break;
            default:
                compressed = sorted;
                break;
        }

        var compressedTokenCount = compressed.Sum(s => s.TokenCount);
        var ratio = originalTokenCount > 0 ? (double)(originalTokenCount - compressedTokenCount) / originalTokenCount : 0;

        _logger?.LogDebug("Compression applied at level {Level}: {OriginalTokens} → {CompressedTokens} tokens ({Ratio:P1})", 
            level, originalTokenCount, compressedTokenCount, ratio);

        return new CompressionResult(compressed, originalTokenCount, compressedTokenCount, ratio);
    }

    /// <inheritdoc />
    public async Task<string?> DecompressAsync(ContextSegment compressedSegment)
    {
        // Decompression is not possible for segments that were lossily compressed.
        // For lossless compression (summarization), the summary IS the compressed form — no decompression needed.
        _logger?.LogDebug("DecompressAsync called for segment {SegmentId} with role {Role}", 
            compressedSegment.Id, compressedSegment.Role);

        return null; // No meaningful decompression exists for context compression
    }

    public void Dispose()
    {
        // No unmanaged resources to clean up
    }

    /// <summary>
    /// Sorts segments by relevance: most recent first, then user messages before assistant/tool.
    /// </summary>
    private static IEnumerable<ContextSegment> SortByRelevance(IEnumerable<ContextSegment> segments)
    {
        return segments.OrderByDescending(s => s.Role == MessageRole.User)
                      .ThenByDescending(s => s.TokenCount); // Prefer longer (more detailed) messages
    }

    /// <summary>
    /// Light compression: extract key phrases from older messages, keep recent ones intact.
    /// </summary>
    private List<ContextSegment> ApplyLightCompression(List<ContextSegment> sortedSegments, List<ContextSegment> pinnedSegments)
    {
        var result = new List<ContextSegment>(pinnedSegments);

        // Keep first N segments (most relevant/recent) uncompressed
        const int UncompressedThreshold = 10;

        for (int i = 0; i < sortedSegments.Count; i++)
        {
            var segment = sortedSegments[i];

            if (i < UncompressedThreshold || segment.IsCompressed)
            {
                // Keep as-is
                result.Add(segment);
            }
            else
            {
                // Extract key phrases from older content
                var compressedContent = ExtractKeyPhrases(segment.Content, maxPhrases: 5);
                
                if (!string.IsNullOrEmpty(compressedContent))
                {
                    result.Add(new ContextSegment
                    {
                        Id = segment.Id,
                        Content = $"[Compressed] Key points: {compressedContent}",
                        Role = segment.Role,
                        IsCompressed = true,
                        TokenCount = EstimateTokenCount(compressedContent),
                        InjectionType = ContextInjectionType.CompressedHistory
                    });
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Medium compression: summarize older messages into short summaries.
    /// </summary>
    private List<ContextSegment> ApplyMediumCompression(List<ContextSegment> sortedSegments, List<ContextSegment> pinnedSegments)
    {
        var result = new List<ContextSegment>(pinnedSegments);

        // Keep first 5 segments uncompressed for context window integrity
        const int UncompressedThreshold = 5;
        const int MaxSummaryLength = 100; // characters max per summary

        for (int i = 0; i < sortedSegments.Count; i++)
        {
            var segment = sortedSegments[i];

            if (i < UncompressedThreshold || segment.IsCompressed)
            {
                result.Add(segment);
            }
            else if (segment.Role == MessageRole.Tool)
            {
                // Tool outputs: condense to just the key result
                var condensedContent = CondenseToolOutput(segment.Content, MaxSummaryLength);
                
                if (!string.IsNullOrEmpty(condensedContent))
                {
                    result.Add(new ContextSegment
                    {
                        Id = segment.Id,
                        Content = $"[Compressed] Tool result: {condensedContent}",
                        Role = MessageRole.Tool,
                        IsCompressed = true,
                        TokenCount = EstimateTokenCount(condensedContent),
                        InjectionType = ContextInjectionType.CompressedHistory
                    });
                }
            }
            else
            {
                // User/assistant messages: create a short summary of the exchange
                var summary = GenerateConversationSummary(segment.Content, MaxSummaryLength);

                if (!string.IsNullOrEmpty(summary))
                {
                    result.Add(new ContextSegment
                    {
                        Id = segment.Id,
                        Content = $"[Compressed] Summary: {summary}",
                        Role = segment.Role == MessageRole.User ? MessageRole.System : segment.Role, // Promote user messages to system for priority
                        IsCompressed = true,
                        TokenCount = EstimateTokenCount(summary),
                        InjectionType = ContextInjectionType.CompressedHistory
                    });
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Aggressive compression: only keep outline-level information.
    /// </summary>
    private List<ContextSegment> ApplyAggressiveCompression(List<ContextSegment> sortedSegments, List<ContextSegment> pinnedSegments)
    {
        var result = new List<ContextSegment>(pinnedSegments);

        // Keep first 3 segments uncompressed for critical context
        const int UncompressedThreshold = 3;
        const int MaxOutlineLength = 50; // characters max per outline entry

        for (int i = 0; i < sortedSegments.Count; i++)
        {
            var segment = sortedSegments[i];

            if (i < UncompressedThreshold || segment.IsCompressed)
            {
                result.Add(segment);
            }
            else if (segment.Role == MessageRole.Tool)
            {
                // Tool outputs: just the command name and success/failure
                var outline = ExtractToolOutline(segment.Content, MaxOutlineLength);

                if (!string.IsNullOrEmpty(outline))
                {
                    result.Add(new ContextSegment
                    {
                        Id = segment.Id,
                        Content = $"[Condensed] [{outline}]",
                        Role = MessageRole.Tool,
                        IsCompressed = true,
                        TokenCount = EstimateTokenCount(outline),
                        InjectionType = ContextInjectionType.CompressedHistory
                    });
                }
            }
            else if (segment.Role == MessageRole.User)
            {
                // User messages: just the first few words of intent
                var outline = ExtractUserIntentOutline(segment.Content, MaxOutlineLength);

                if (!string.IsNullOrEmpty(outline))
                {
                    result.Add(new ContextSegment
                    {
                        Id = segment.Id,
                        Content = $"[Condensed] Intent: {outline}",
                        Role = MessageRole.System, // Promote user intent to system priority
                        IsCompressed = true,
                        TokenCount = EstimateTokenCount(outline),
                        InjectionType = ContextInjectionType.CompressedHistory
                    });
                }
            }
            else if (segment.Role == MessageRole.Assistant)
            {
                // Assistant messages: just the key action taken
                var outline = ExtractAssistantOutline(segment.Content, MaxOutlineLength);

                if (!string.IsNullOrEmpty(outline))
                {
                    result.Add(new ContextSegment
                    {
                        Id = segment.Id,
                        Content = $"[Condensed] Action: {outline}",
                        Role = MessageRole.System, // Promote to system priority for context window integrity
                        IsCompressed = true,
                        TokenCount = EstimateTokenCount(outline),
                        InjectionType = ContextInjectionType.CompressedHistory
                    });
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Extracts key phrases from text content for light compression.
    /// </summary>
    private string? ExtractKeyPhrases(string content, int maxPhrases)
    {
        if (string.IsNullOrEmpty(content))
            return null;

        // Simple heuristic: extract sentences/lines and pick the most meaningful ones
        var sentences = SplitSentences(content);
        
        // Score each sentence by length and presence of key terms
        var scored = new List<(int Index, double Score)>();
        var keyTerms = new[] { "important", "critical", "key", "result", "output", "error", "fix", "change" };

        for (var i = 0; i < sentences.Count && scored.Count < maxPhrases; i++)
        {
            var sentence = sentences[i];
            if (string.IsNullOrEmpty(sentence)) continue;

            double score = sentence.Length * 0.1; // Prefer longer sentences
            foreach (var term in keyTerms)
            {
                if (sentence.Contains(term, StringComparison.OrdinalIgnoreCase))
                    score += 5;
            }

            scored.Add((i, score));
        }

        // Sort by score descending and take top N
        var selected = scored.OrderByDescending(s => s.Score).Take(maxPhrases);
        
        var phrases = new List<string>();
        foreach (var item in selected)
        {
            if (item.Index < sentences.Count && !string.IsNullOrEmpty(sentences[item.Index]))
                phrases.Add(sentences[item.Index]);

            if (phrases.Count >= maxPhrases) break;
        }

        return string.Join("; ", phrases);
    }

    /// <summary>
    /// Condenses a tool output to its essential result for medium compression.
    /// </summary>
    private string? CondenseToolOutput(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content))
            return null;

        // Look for key results: status codes, exit codes, success/failure indicators
        var lines = SplitSentences(content);

        // Find the most informative line
        string? bestLine = null;
        int bestScore = 0;

        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line)) continue;

            int score = 0;
            var lowerLine = line.ToLowerInvariant();
            
            if (lowerLine.Contains("success") || lowerLine.Contains("completed") || 
                lowerLine.Contains("output") || lowerLine.Contains("result"))
                score += 10;
            else if (lowerLine.Contains("error") || lowerLine.Contains("fail") || lowerLine.Contains("exception"))
                score += 5;

            // Prefer lines with numbers (likely actual data)
            var digits = line.Where(char.IsDigit).Count();
            score += digits * 2;

            if (score > bestScore)
            {
                bestScore = score;
                bestLine = line;
            }
        }

        return !string.IsNullOrEmpty(bestLine) 
            ? TruncateToLength(bestLine, maxLength) 
            : TruncateToLength(content, maxLength);
    }

    /// <summary>
    /// Generates a short summary of a conversation exchange for medium compression.
    /// </summary>
    private string? GenerateConversationSummary(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content))
            return null;

        // Extract the core meaning by taking key phrases and truncating
        var sentences = SplitSentences(content);
        
        if (!sentences.Any())
            return TruncateToLength(content, maxLength);

        // Take first sentence or up to maxLength characters
        foreach (var sentence in sentences)
        {
            if (string.IsNullOrEmpty(sentence)) continue;

            if (sentence.Length <= maxLength)
                return sentence;

            var truncated = TruncateToLength(sentence, maxLength - 3) + "...";
            return truncated.StartsWith("[", StringComparison.Ordinal) ? truncated : $"[{truncated}";
        }

        // Fallback: first N characters of content wrapped in brackets
        var result = TruncateToLength(content, maxLength - 2);
        return !string.IsNullOrEmpty(result) ? $"[{result}" : null;
    }

    /// <summary>
    /// Extracts an outline of a tool output for aggressive compression.
    /// </summary>
    private string? ExtractToolOutline(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content))
            return null;

        // Look for the command/tool name and result status
        var lines = SplitSentences(content);
        
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line)) continue;

            // Check for common tool output patterns
            if (line.StartsWith("Command:", StringComparison.Ordinal) || line.StartsWith("$ ", StringComparison.Ordinal))
                return TruncateToLength(line, maxLength);

            // Look for exit codes or status indicators
            var lowerLine = line.ToLowerInvariant();
            if (lowerLine.Contains("exit code") || lowerLine.Contains("status:"))
                return TruncateToLength(line, maxLength - 2) + "]";
        }

        // Fallback: take first word of content as the "command" identifier
        var words = content.Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 0)
            return TruncateToLength(words[0], maxLength - 2) + "]";

        return null;
    }

    /// <summary>
    /// Extracts user intent from a user message for aggressive compression.
    /// </summary>
    private string? ExtractUserIntentOutline(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content))
            return null;

        // Look for the core request/question pattern
        var sentences = SplitSentences(content);
        
        foreach (var sentence in sentences)
        {
            if (string.IsNullOrEmpty(sentence)) continue;

            // Check for question/intent patterns
            var lowerSentence = sentence.ToLowerInvariant();
            if (lowerSentence.StartsWith("how", StringComparison.Ordinal) || 
                lowerSentence.StartsWith("what", StringComparison.Ordinal) ||
                lowerSentence.StartsWith("can you", StringComparison.Ordinal) ||
                lowerSentence.StartsWith("please", StringComparison.Ordinal))
            {
                return TruncateToLength(sentence, maxLength - 2) + "]";
            }

            // Otherwise take the first meaningful sentence
            if (sentence.Length > 10)
                return TruncateToLength(sentence, maxLength - 2) + "]";
        }

        // Fallback: first few words of content
        var words = content.Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 0)
            return TruncateToLength(string.Join(" ", words.Take(5)), maxLength - 2) + "]";

        return null;
    }

    /// <summary>
    /// Extracts key action from an assistant message for aggressive compression.
    /// </summary>
    private string? ExtractAssistantOutline(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content))
            return null;

        // Look for the primary action/description in assistant responses
        var sentences = SplitSentences(content);
        
        foreach (var sentence in sentences)
        {
            if (string.IsNullOrEmpty(sentence)) continue;

            // Prefer sentences that describe actions or changes
            var lowerSentence = sentence.ToLowerInvariant();
            if (lowerSentence.Contains("done") || lowerSentence.Contains("changed") || 
                lowerSentence.Contains("updated") || lowerSentence.Contains("created"))
            {
                return TruncateToLength(sentence, maxLength - 2) + "]";
            }

            // Otherwise take the first meaningful sentence
            if (sentence.Length > 10)
                return TruncateToLength(sentence, maxLength - 2) + "]";
        }

        // Fallback: first few words of content
        var words = content.Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 0)
            return TruncateToLength(string.Join(" ", words.Take(5)), maxLength - 2) + "]";

        return null;
    }

    /// <summary>
    /// Splits text into sentences/lines for processing.
    /// </summary>
    private static List<string> SplitSentences(string content)
    {
        if (string.IsNullOrEmpty(content))
            return new();

        var result = new List<string>();
        
        // Split by sentence-ending punctuation and newlines
        var parts = content.Split(new[] { '\n', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                result.Add(trimmed);
        }

        return result;
    }

    /// <summary>
    /// Truncates text to a maximum length with ellipsis if needed.
    /// </summary>
    private static string TruncateToLength(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? "";

        return text.Length > maxLength 
            ? text.Substring(0, Math.Max(1, maxLength - 3)) + "..."
            : text;
    }

    /// <summary>
    /// Standardized token counting method using consistent estimation: ~1 token per 4 characters for English.
    /// </summary>
    private static int EstimateTokenCount(string text) => 
        string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;
}