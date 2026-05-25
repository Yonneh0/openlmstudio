namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Result from a tool execution.
/// </summary>
public record ToolResult
{
    /// <summary>Whether the tool execution succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Tool output as text (for display).</summary>
    public string? Output { get; init; }

    /// <summary>Error message if the tool failed.</summary>
    public string? Error { get; init; }

    /// <summary>Images returned by the tool (e.g., screenshots).</summary>
    public List<string>? Images { get; init; }

    /// <summary>Files modified by this tool execution.</summary>
    public List<string>? ModifiedFiles { get; init; }

    /// <summary>Duration of tool execution in milliseconds.</summary>
    public double DurationMs { get; init; }

    /// <summary>
    /// Creates a successful tool result.
    /// </summary>
    public static ToolResult Ok(string output, List<string>? modifiedFiles = null)
        => new() { Success = true, Output = output, ModifiedFiles = modifiedFiles };

    /// <summary>
    /// Creates a failed tool result.
    /// </summary>
    public static ToolResult Fail(string error)
        => new() { Success = false, Error = error };

    /// <summary>
    /// Creates a tool result with images (e.g., browser screenshots).
    /// </summary>
    public static ToolResult WithImages(string output, List<string> images)
        => new() { Success = true, Output = output, Images = images };
}