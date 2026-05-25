namespace OpenLMStudio.Application.Types.Agent;

/// <summary>
/// Request for use_mcp_tool tool.
/// </summary>
public record UseMcpToolRequest
{
    /// <summary>Name of the MCP server.</summary>
    public string ServerName { get; init; } = string.Empty;

    /// <summary>Name of the tool to execute.</summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>JSON object containing the tool's input parameters.</summary>
    public string Arguments { get; init; } = string.Empty;

    /// <summary>Task progress checklist (optional).</summary>
    public string? TaskProgress { get; init; }
}