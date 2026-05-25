namespace OpenLMStudio.Application.Types.Agent;

/// <summary>
/// Request for search_files tool.
/// </summary>
public record SearchFilesRequest
{
    /// <summary>Directory path to search in.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Regular expression pattern (uses Rust regex syntax).</summary>
    public string Regex { get; init; } = string.Empty;

    /// <summary>Glob pattern to filter files (e.g., "*.ts").</summary>
    public string? FilePattern { get; init; }

    /// <summary>Task progress checklist (optional).</summary>
    public string? TaskProgress { get; init; }
}