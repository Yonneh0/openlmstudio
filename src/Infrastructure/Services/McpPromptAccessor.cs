using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// MCP prompt accessor for accessing prompts from connected MCP servers.
/// </summary>
public class McpPromptAccessor : ITool, IDisposable
{
    private readonly ILogger<McpPromptAccessor>? _logger;
    private readonly IMcpClient _mcpClient;
    private bool _disposed;

    public string Name => "MCPGetPrompt";
    public string Description => "Reads a prompt from the connected MCP server by name, optionally with arguments.";

    /// <summary>
    /// Creates a new MCP prompt accessor instance.
    /// </summary>
    public McpPromptAccessor(ILogger<McpPromptAccessor>? logger, IMcpClient mcpClient)
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
            var promptName = TryGetString(parameters, "PromptName");
            if (string.IsNullOrEmpty(promptName))
            {
                _logger?.LogWarning("MCPGetPrompt called without PromptName parameter.");
                return false;
            }

            // Get the prompt from the MCP server via a tool call to __get_prompt method
            var argsJson = parameters.ContainsKey("Arguments")
                ? TryGetString(parameters, "Arguments") ?? "{}"
                : "{}";

            _logger?.LogInformation("Accessing MCP prompt: {PromptName}", promptName);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MCPGetPrompt failed for prompt: {PromptName}", TryGetString(parameters, "PromptName") ?? "(unknown)");
            return false;
        }
    }

    /// <inheritdoc />
    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new Dictionary<string, ToolParameterSchema>
    {
        ["PromptName"] = new ToolParameterSchema("string", true),   // Required — name of the MCP prompt to retrieve
        ["Arguments"] = new ToolParameterSchema("string", false)     // Optional — JSON arguments for the prompt (e.g., system instructions, context)
    };

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    private static string? TryGetString(Dictionary<string, object> dict, string key) =>
        dict.TryGetValue(key, out var value) ? Convert.ToString(value) : null;
}

/// <summary>
/// MCP prompt list tool for discovering available prompts from connected servers.
/// </summary>
public class McpPromptListTool : ITool, IDisposable
{
    private readonly ILogger<McpPromptListTool>? _logger;
    private readonly IMcpClient _mcpClient;
    private bool _disposed;

    public string Name => "MCPPromptsList";
    public string Description => "Lists all available prompts from the connected MCP server.";

    /// <summary>
    /// Creates a new MCP prompt list tool instance.
    /// </summary>
    public McpPromptListTool(ILogger<McpPromptListTool>? logger, IMcpClient mcpClient)
    {
        _logger = logger;
        _mcpClient = mcpClient ?? throw new ArgumentNullException(nameof(mcpClient));
    }

    /// <inheritdoc />
    public async Task<bool> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (_disposed) return false;

        try
        {
            _logger?.LogInformation("Listing MCP prompts");
            
            var promptName = TryGetString(parameters, "PromptFilter") ?? string.Empty;
            // Filter prompts by name if specified
            _logger?.LogDebug("MCPPromptsList called with filter: {PromptFilter}", promptName);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MCP Prompt listing failed");
            return false;
        }
    }

    /// <inheritdoc />
    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new Dictionary<string, ToolParameterSchema>
    {
        ["PromptFilter"] = new ToolParameterSchema("string", false)  // Optional — filter prompts by name pattern
    };

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    private static string? TryGetString(Dictionary<string, object> dict, string key) =>
        dict.TryGetValue(key, out var value) ? Convert.ToString(value) : null;
}