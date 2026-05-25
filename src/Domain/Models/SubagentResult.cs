namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Result from a parallel subagent execution.
/// </summary>
public class SubagentResult
{
    /// <summary>Index of this subagent (1-5).</summary>
    public int Index { get; set; }

    /// <summary>The prompt given to this subagent.</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>The result produced by this subagent.</summary>
    public string? Result { get; set; }

    /// <summary>Error message if this subagent failed.</summary>
    public string? Error { get; set; }

    /// <summary>Whether this subagent succeeded.</summary>
    public bool Success => Error == null;

    /// <summary>Duration of the subagent execution in milliseconds.</summary>
    public double DurationMs { get; set; }
}