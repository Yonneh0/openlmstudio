namespace OpenLMStudio.Application.Types.Agent;

/// <summary>
/// Request for access_mcp_resource tool.
/// </summary>
public record AccessMcpResourceRequest
{
    /// <summary>Name of the MCP server.</summary>
    public string ServerName { get; init; } = string.Empty;

    /// <summary>URI identifying the resource.</summary>
    public string Uri { get; init; } = string.Empty;

    /// <summary>Task progress checklist (optional).</summary>
    public string? TaskProgress { get; init; }
}