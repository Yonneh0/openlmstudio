namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Configuration for a connected MCP (Model Context Protocol) server.
/// </summary>
public class McpServerConfig
{
    /// <summary>Unique name/identifier for this MCP server.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Connection URI for the MCP server.</summary>
    public string Uri { get; set; } = string.Empty;

    /// <summary>Whether this server is currently connected.</summary>
    public bool IsConnected { get; set; }

    /// <summary>List of tools available on this server.</summary>
    public List<McpToolInfo> Tools { get; set; } = new();

    /// <summary>List of resources available on this server.</summary>
    public List<McpResourceInfo> Resources { get; set; } = new();

    /// <summary>
    /// Information about a tool available on an MCP server.
    /// </summary>
    public class McpToolInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, object> InputSchema { get; set; } = new();
    }

    /// <summary>
    /// Information about a resource available on an MCP server.
    /// </summary>
    public class McpResourceInfo
    {
        public string Uri { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}