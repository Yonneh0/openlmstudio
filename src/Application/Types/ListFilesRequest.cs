namespace OpenLMStudio.Application.Types.Agent;

/// <summary>
/// Request for list_files tool.
/// </summary>
public record ListFilesRequest
{
    /// <summary>Directory path to list contents for.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Whether to list files recursively (default: false).</summary>
    public bool Recursive { get; init; }

    /// <summary>Task progress checklist (optional).</summary>
    public string? TaskProgress { get; init; }
}