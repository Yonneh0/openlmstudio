namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Represents a single .agentignore rule for matching files.
/// Supports glob patterns similar to .gitignore.
/// </summary>
public class AgentIgnoreRule
{
    /// <summary>The raw pattern string (e.g., "*.log", "bin/**").</summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>Whether this rule is negated (e.g., "!*.log").</summary>
    public bool IsNegated { get; set; }

    /// <summary>Whether the pattern matches directory names.</summary>
    public bool MatchesDirectories { get; set; } = true;

    /// <summary>
    /// Checks if the given file path matches this rule.
    /// </summary>
    public bool Matches(string filePath)
    {
        var pattern = Pattern;
        if (pattern.StartsWith('!'))
            pattern = pattern.Substring(1);

        // Handle ** (match everything including subdirectories)
        if (pattern.Contains("**"))
        {
            var parts = pattern.Split(new[] { "**" }, StringSplitOptions.RemoveEmptyEntries);
            var lastPart = parts[^1].Trim('/');
            return filePath.EndsWith(lastPart, StringComparison.OrdinalIgnoreCase) ||
                   filePath.Contains(lastPart);
        }

        // Handle * (match within single directory level)
        if (pattern.Contains('*'))
        {
            var fileName = Path.GetFileName(filePath);
            return WildcardMatch(fileName, pattern) ||
                   WildcardMatch(filePath, pattern);
        }

        // Simple string match
        return filePath.Equals(pattern, StringComparison.OrdinalIgnoreCase) ||
               filePath.EndsWith($"/{pattern}", StringComparison.OrdinalIgnoreCase) ||
               filePath.Contains($"/{pattern}/");
    }

    /// <summary>
    /// Simple wildcard matching (* and ? patterns).
    /// </summary>
    private static bool WildcardMatch(string text, string pattern)
    {
        // Convert glob pattern to regex
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        return System.Text.RegularExpressions.Regex.IsMatch(
            text, regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}