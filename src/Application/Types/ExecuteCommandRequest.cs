namespace OpenLMStudio.Application.Types.Agent;

/// <summary>
/// Request for execute_command tool.
/// </summary>
public record ExecuteCommandRequest
{
    /// <summary>The CLI command to execute.</summary>
    public string Command { get; init; } = string.Empty;

    /// <summary>Whether the command requires explicit user approval.</summary>
    public bool RequiresApproval { get; init; }

    /// <summary>Timeout in seconds (optional).</summary>
    public int? Timeout { get; init; }

    /// <summary>Task progress checklist (optional).</summary>
    public string? TaskProgress { get; init; }
}