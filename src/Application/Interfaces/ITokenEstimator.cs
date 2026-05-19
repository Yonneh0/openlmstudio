namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Service interface for token estimation operations.
/// Provides standardized token counting using character-based estimation.
/// </summary>
public interface ITokenEstimator
{
    /// <summary>
    /// Estimates the token count for a given text using standardized method: ~1 token per 4 characters for English.
    /// </summary>
    /// <param name="text">The text to estimate.</param>
    /// <returns>Estimated token count.</returns>
    int EstimateTokens(string text);

    /// <summary>
    /// Estimates the token count using an advanced algorithm considering word boundaries.
    /// </summary>
    /// <param name="text">The text to estimate.</param>
    /// <returns>Estimated token count.</returns>
    int EstimateTokensAdvanced(string text);

    /// <summary>
    /// Estimates the total token count for a list of messages.
    /// </summary>
    /// <param name="messages">Messages with role and content.</param>
    /// <returns>Estimated total token count.</returns>
    int EstimateMessagesTokens(IEnumerable<(string Role, string Content)> messages);

    /// <summary>
    /// Gets a human-readable representation of a token count.
    /// </summary>
    /// <param name="tokenCount">The token count.</param>
    /// <returns>Human-readable string (e.g., "1.5K tokens").</returns>
    string GetHumanReadableTokenCount(int tokenCount);
}