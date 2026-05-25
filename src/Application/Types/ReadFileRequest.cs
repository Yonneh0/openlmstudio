namespace OpenLMStudio.Application.Types.Agent;

/// <summary>
/// Request for read_file tool.
/// </summary>
public record ReadFileRequest
{
    /// <summary>Relative path to the file.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>1-based line number to start reading from (default: 1).</summary>
    public int StartLine { get; init; } = 1;

    /// <summary>1-based line number to stop reading at (default: startLine + 1000).</summary>
    public int EndLine { get; init; } = 1001;

    /// <summary>Task progress checklist (optional).</summary>
    public string? TaskProgress { get; init; }
}