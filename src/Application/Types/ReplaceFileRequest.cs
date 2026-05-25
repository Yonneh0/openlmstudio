namespace OpenLMStudio.Application.Types.Agent;

/// <summary>
/// Request for replace_in_file tool.
/// </summary>
public record ReplaceFileRequest
{
    /// <summary>Relative path to the file.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>One or more SEARCH/REPLACE blocks.</summary>
    public string Diff { get; init; } = string.Empty;

    /// <summary>Task progress checklist (optional).</summary>
    public string? TaskProgress { get; init; }
}