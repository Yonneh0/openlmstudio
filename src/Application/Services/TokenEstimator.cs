namespace OpenLMStudio.Application.Services;

using OpenLMStudio.Application.Interfaces;

/// <summary>
/// Service implementation for token estimation operations.
/// Provides standardized token counting using character-based estimation.
/// </summary>
public class TokenEstimator : ITokenEstimator
{
    /// <summary>
    /// Average characters per token for English text (approximate).
    /// Based on the observation that ~4 characters ≈ 1 token.
    /// </summary>
    private const double CharsPerToken = 4.0;

    /// <summary>
    /// Average characters per token for complex text with punctuation and special characters.
    /// </summary>
    private const double ComplexCharsPerToken = 3.5;

    /// <summary>
    /// Average characters per token for code/technical content.
    /// </summary>
    private const double CodeCharsPerToken = 3.0;

    /// <summary>
    /// Estimates the token count for a given text using standardized method: ~1 token per 4 characters for English.
    /// </summary>
    /// <param name="text">The text to estimate.</param>
    /// <returns>Estimated token count.</returns>
    public int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        return (int)Math.Ceiling(text.Length / CharsPerToken);
    }

    /// <summary>
    /// Estimates the token count using an advanced algorithm considering word boundaries.
    /// </summary>
    /// <param name="text">The text to estimate.</param>
    /// <returns>Estimated token count.</returns>
    public int EstimateTokensAdvanced(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // Count words, punctuation, and special characters
        var wordCount = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        var punctuationCount = text.Count(c => char.IsPunctuation(c));
        var newlineCount = text.Count(c => c == '\n');

        // Word-based estimation: each word is roughly 1-2 tokens
        // Punctuation adds ~0.1 tokens each
        // Newlines add ~0.5 tokens each
        var wordTokens = wordCount * 1.3;
        var punctTokens = punctuationCount * 0.1;
        var newlineTokens = newlineCount * 0.5;

        return (int)Math.Ceiling(wordTokens + punctTokens + newlineTokens);
    }

    /// <summary>
    /// Estimates the total token count for a list of messages.
    /// </summary>
    /// <param name="messages">Messages with role and content.</param>
    /// <returns>Estimated total token count.</returns>
    public int EstimateMessagesTokens(IEnumerable<(string Role, string Content)> messages)
    {
        if (messages == null)
            return 0;

        var totalTokens = 0;
        foreach (var (role, content) in messages)
        {
            var contentTokens = EstimateTokens(content);
            // Add role prefix tokens (e.g., "user: ", "assistant: ")
            var roleTokens = EstimateTokens(role);
            totalTokens += contentTokens + roleTokens;
        }

        // Add message separator overhead (~2 tokens per message)
        totalTokens += messages.Count() * 2;

        return totalTokens;
    }

    /// <summary>
    /// Gets a human-readable representation of a token count.
    /// </summary>
    /// <param name="tokenCount">The token count.</param>
    /// <returns>Human-readable string (e.g., "1.5K tokens").</returns>
    public string GetHumanReadableTokenCount(int tokenCount)
    {
        if (tokenCount < 0)
            return "0 tokens";

        if (tokenCount < 1000)
            return $"{tokenCount} tokens";

        if (tokenCount < 1_000_000)
            return $"{tokenCount / 1000.0:F1}K tokens";

        return $"{tokenCount / 1_000_000.0:F1}M tokens";
    }
}