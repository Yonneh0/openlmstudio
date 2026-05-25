namespace OpenLMStudio.Application.Types.Agent;

/// <summary>
/// Request for write_to_file tool.
/// </summary>
public record WriteFileRequest
{
    /// <summary>Relative path to the file (e.g., "src/index.ts").</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>The complete content to write.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>Task progress checklist (optional).</summary>
    public string? TaskProgress { get; init; }
}