using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using System.Text.RegularExpressions;

namespace OpenLMStudio.Infrastructure.Services;

public class TokenEstimator : ITokenEstimator
{
    private readonly ILogger<TokenEstimator> _logger;
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex WordRegex = new(@"\b[a-zA-Z]{2,}\b", RegexOptions.Compiled);

    public TokenEstimator(ILogger<TokenEstimator> logger)
    {
        _logger = logger;
    }

    public int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        var lengthBasedEstimate = (text.Length + 3) / 4;
        _logger.LogDebug("Estimated {Tokens} tokens for {Length} character input", lengthBasedEstimate, text.Length);
        return lengthBasedEstimate;
    }

    public int EstimateTokensAdvanced(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        var wordCount = WordRegex.Matches(text).Count;
        var wordBasedEstimate = wordCount * 1.3;
        var lengthBasedEstimate = (text.Length + 3) / 4;
        var estimate = (int)Math.Max(wordBasedEstimate, lengthBasedEstimate);
        return estimate;
    }

    public int EstimateMessagesTokens(IEnumerable<(string Role, string Content)> messages)
    {
        return messages.Sum(m => EstimateTokens(m.Content));
    }

    public string GetHumanReadableTokenCount(int tokenCount)
    {
        return tokenCount switch
        {
            >= 1_000_000 => $"{tokenCount / 1_000_000.0:F2}M tokens",
            >= 1000 => $"{tokenCount / 1000.0:F2}K tokens",
            _ => $"{tokenCount} tokens"
        };
    }
}