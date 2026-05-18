using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// MCP tool caller for invoking tools from connected MCP servers within the agent harness.
/// </summary>
public class McpToolCaller : ITool, IDisposable
{
    private readonly ILogger<McpToolCaller>? _logger;
    private readonly IMcpClient _mcpClient;
    private bool _disposed;

    public string Name => "MCPToolCall";
    public string Description => "Invokes a tool from the connected MCP server by name with JSON arguments.";

    /// <summary>
    /// Creates a new MCP tool caller instance.
    /// </summary>
    public McpToolCaller(ILogger<McpToolCaller>? logger, IMcpClient mcpClient)
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
            var toolName = TryGetString(parameters, "ToolName");
            if (string.IsNullOrEmpty(toolName))
            {
                _logger?.LogWarning("MCPToolCall called without ToolName parameter.");
                return false;
            }

            string argsJson = "{}";
            if (parameters.TryGetValue("Arguments", out var argsValue) && argsValue is string jsonArgs)
                argsJson = jsonArgs;

            // Call the MCP tool via the connected server
            var result = await _mcpClient.CallToolAsync(toolName, argsJson);

            return !string.IsNullOrEmpty(result);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MCPToolCall failed for tool: {ToolName}", TryGetString(parameters, "ToolName") ?? "(unknown)");
            return false;
        }
    }

    /// <inheritdoc />
    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new Dictionary<string, ToolParameterSchema>
    {
        ["ToolName"] = new ToolParameterSchema("string", true),  // Required — name of the MCP tool to invoke
        ["Arguments"] = new ToolParameterSchema("string", false) // Optional — JSON string of arguments for the tool
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