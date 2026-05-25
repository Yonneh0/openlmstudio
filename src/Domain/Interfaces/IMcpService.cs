namespace OpenLMStudio.Domain.Interfaces;

using Models;

/// <summary>
/// Service for interacting with MCP (Model Context Protocol) servers.
/// Provides tool invocation and resource access.
/// </summary>
public interface IMcpService
{
    /// <summary>
    /// Uses a tool provided by a connected MCP server.
    /// </summary>
    /// <param name="serverName">Name of the MCP server.</param>
    /// <param name="toolName">Name of the tool to execute.</param>
    /// <param name="arguments">JSON object containing the tool's input parameters.</param>
    /// <returns>Tool result with text output and optionally images.</returns>
    Task<ToolResult> UseToolAsync(string serverName, string toolName, string arguments);

    /// <summary>
    /// Accesses a resource provided by a connected MCP server.
    /// </summary>
    /// <param name="serverName">Name of the MCP server.</param>
    /// <param name="uri">URI identifying the resource.</param>
    /// <returns>Resource content as text.</returns>
    Task<ToolResult> AccessResourceAsync(string serverName, string uri);

    /// <summary>
    /// Loads documentation about creating MCP servers.
    /// </summary>
    /// <returns>MCP documentation as text.</returns>
    Task<ToolResult> LoadDocumentationAsync();
}