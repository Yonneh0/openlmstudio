using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// MCP resource accessor for accessing resources from connected MCP servers by URI.
/// </summary>
public class McpResourceAccessor : ITool, IDisposable
{
    private readonly ILogger<McpResourceAccessor>? _logger;
    private readonly IMcpClient _mcpClient;
    private bool _disposed;

    public string Name => "MCPResourceAccess";
    public string Description => "Reads a resource from the connected MCP server by URI.";

    /// <summary>
    /// Creates a new MCP resource accessor instance.
    /// </summary>
    public McpResourceAccessor(ILogger<McpResourceAccessor>? logger, IMcpClient mcpClient)
    {
        _logger = logger;
        _mcpClient = mcpClient ?? throw new ArgumentNullException(nameof(mcpClient));
    }

    /// <inheritdoc />
    public async Task<bool> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (parameters == null || _disposed) return false;

        try
        {
            var resourceUri = TryGetString(parameters, "ResourceUri");
            if (string.IsNullOrEmpty(resourceUri))
            {
                _logger?.LogWarning("MCPResourceAccess called without ResourceUri parameter.");
                return false;
            }

            // Access the MCP resource via the connected server
            var result = await _mcpClient.CallToolAsync("__access_resource", 
                $"{{\"uri\": \"{resourceUri}\"}}");

            return !string.IsNullOrEmpty(result);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MCPResourceAccess failed for URI: {ResourceUri}", TryGetString(parameters, "ResourceUri") ?? "(unknown)");
            return false;
        }
    }

    /// <inheritdoc />
    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new Dictionary<string, ToolParameterSchema>
    {
        ["ResourceUri"] = new ToolParameterSchema("string", true),  // Required — URI of the MCP resource to access
        ["MimeType"] = new ToolParameterSchema("string", false)     // Optional — desired MIME type for the resource
    };

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            // No unmanaged resources to dispose.
        }
    }

    private static string? TryGetString(Dictionary<string, object> dict, string key) =>
        dict.TryGetValue(key, out var value) ? Convert.ToString(value) : null;
}