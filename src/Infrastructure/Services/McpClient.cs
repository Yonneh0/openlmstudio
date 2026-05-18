using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// MCP (Model Context Protocol) client implementation using stdio transport.
/// </summary>
public class McpStdioClient : IMcpClient, IDisposable
{
    private readonly ILogger<McpStdioClient> _logger;
    private Process? _process;
    private volatile bool _isConnected;

    /// <summary>
    /// Event raised when tools are discovered.
    /// </summary>
    public event EventHandler? ToolsDiscovered;

    /// <inheritdoc />
    public bool IsConnected => _isConnected && _process != null && !_process.HasExited;

    /// <inheritdoc />
    public List<McpToolDefinition> DiscoveredTools { get; } = new();

    /// <summary>
    /// Creates a new MCP stdio client.
    /// </summary>
    public McpStdioClient(ILogger<McpStdioClient> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ConnectAsync(string command, string[] args, string transportMode = "stdio")
    {
        if (IsConnected) return;

        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (args != null && args.Length > 0)
        {
            startInfo.Arguments = string.Join(" ", args);
        }

        _process = new Process { StartInfo = startInfo };

        try
        {
            _process.Start();

            // Wait a moment for the process to initialize
            await Task.Delay(1000);

            if (_process.HasExited)
            {
                _logger.LogError("MCP server process exited unexpectedly");
                return;
            }

            // Initialize MCP connection with capabilities
            var initMessage = new McpMessage
            {
                Method = "initialize",
                Params = new Dictionary<string, object>
                {
                    ["protocolVersion"] = 20260101,
                    ["clientInfo"] = new { name = "OpenLMStudio", version = "0.1.0" }
                }
            };

            var response = await SendMcpMessageAsync(initMessage);

            if (response != null && response.Result?["status"]?.ToString() == "success")
            {
                _isConnected = true;

                // Discover available tools
                await DiscoverToolsInternal();

                ToolsDiscovered?.Invoke(this, EventArgs.Empty);
                _logger.LogInformation("MCP client connected successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MCP server");
        }
    }

    /// <inheritdoc />
    public async Task DisconnectAsync()
    {
        if (_isConnected && _process != null && !_process.HasExited)
        {
            var shutdownMessage = new McpMessage
            {
                Method = "shutdown"
            };

            await SendMcpMessageAsync(shutdownMessage);

            try
            {
                _process.Kill();
                _process.WaitForExit(5000);
            }
            catch
            {
                // Ignore if process can't be killed gracefully
            }
        }

        _isConnected = false;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<McpToolDefinition>> ListToolsAsync()
    {
        var discoverMessage = new McpMessage
        {
            Method = "tools/list"
        };

        var response = await SendMcpMessageAsync(discoverMessage);

        if (response?.Result is System.Text.Json.Nodes.JsonObject toolsObj)
        {
            // Parse the tools array using JsonDocument and GetRawValue() for .NET 8+ compatibility
            var resultJson = JsonSerializer.Serialize(toolsObj);
            using var doc = System.Text.Json.JsonDocument.Parse(resultJson);

            DiscoveredTools.Clear();

            if (doc.RootElement.TryGetProperty("tools", out var toolsProp) &&
                toolsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var tool in toolsProp.EnumerateArray())
                {
                    var name = GetJsonStringValue(tool, "name");
                    var description = GetJsonStringValue(tool, "description");

                    Dictionary<string, object>? schema = null;
                    if (tool.TryGetProperty("inputSchema", out var schemaProp) &&
                        schemaProp.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        // Serialize the JsonElement back to JSON string for .NET 8+ compatibility
                        var rawJson = JsonSerializer.Serialize(schemaProp);
                        schema = JsonSerializer.Deserialize<Dictionary<string, object>>(rawJson);
                    }

                    var toolName = name ?? "unknown";
                    var toolDescription = description ?? "";
                    DiscoveredTools.Add(new McpToolDefinition(toolName, toolDescription, schema));
                }
            }
        }

        return DiscoveredTools;
    }

    /// <summary>
    /// Extracts a string value from a JsonElement property by name.
    /// </summary>
    private static string? GetJsonStringValue(System.Text.Json.JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
            return prop.ValueKind == System.Text.Json.JsonValueKind.String ? prop.GetString() : null;
        return null;
    }

    /// <inheritdoc />
    public async Task<string?> CallToolAsync(string toolName, string argumentsJson)
    {
        if (!_isConnected || _process == null || _process.HasExited)
            return null;

        var toolCallMessage = new McpMessage
        {
            Method = "tools/call",
            Params = new Dictionary<string, object>
            {
                ["name"] = toolName,
                ["arguments"] = argumentsJson
            }
        };

        var response = await SendMcpMessageAsync(toolCallMessage);

        if (response == null)
            return null;

        return response.Result?.ToString() ?? "";
    }

    /// <summary>
    /// Discovers available tools from the MCP server.
    /// </summary>
    private async Task DiscoverToolsInternal()
    {
        var discoverMessage = new McpMessage
        {
            Method = "tools/list"
        };

        var response = await SendMcpMessageAsync(discoverMessage);

        if (response?.Result is System.Text.Json.Nodes.JsonObject toolsObj)
        {
            // Parse the tools array using JsonDocument and GetRawValue() for .NET 8+ compatibility
            var resultJson = JsonSerializer.Serialize(toolsObj);
            using var doc = System.Text.Json.JsonDocument.Parse(resultJson);

            DiscoveredTools.Clear();

            if (doc.RootElement.TryGetProperty("tools", out var toolsProp) &&
                toolsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var tool in toolsProp.EnumerateArray())
                {
                    var name = GetJsonStringValue(tool, "name");
                    var description = GetJsonStringValue(tool, "description");

                    Dictionary<string, object>? schema = null;
                    if (tool.TryGetProperty("inputSchema", out var schemaProp) &&
                        schemaProp.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        // Serialize the JsonElement back to JSON string for .NET 8+ compatibility
                        var rawJson = JsonSerializer.Serialize(schemaProp);
                        schema = JsonSerializer.Deserialize<Dictionary<string, object>>(rawJson);
                    }

                    var toolName = name ?? "unknown";
                    var toolDescription = description ?? "";
                    DiscoveredTools.Add(new McpToolDefinition(toolName, toolDescription, schema));
                }
            }
        }
    }

    private async Task<McpMessage?> SendMcpMessageAsync(McpMessage message)
    {
        if (_process?.StandardInput == null) return null;

        var json = JsonSerializer.Serialize(message);

        try
        {
            await _process.StandardInput.WriteLineAsync(json);
            await _process.StandardInput.FlushAsync();

            var responseText = await _process.StandardOutput.ReadLineAsync();

            if (responseText != null)
            {
                return JsonSerializer.Deserialize<McpMessage>(responseText);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending MCP message");
            return null;
        }
    }

    public void Dispose()
    {
        if (_isConnected && _process != null && !_process.HasExited)
        {
            try
            {
                var shutdownMessage = new McpMessage { Method = "shutdown" };
                SendMcpMessageAsync(shutdownMessage).Wait();

                _process.Kill();
                _process.WaitForExit(1000);
            }
            catch { /* Ignore disposal errors */ }
        }

        _process?.Dispose();
    }
}

/// <summary>
/// Internal representation of an MCP JSON-RPC message.
/// </summary>
internal record McpMessage
{
    public string? Method { get; set; }
    public Dictionary<string, object>? Params { get; init; }
    public System.Text.Json.Nodes.JsonObject? Result { get; set; }
    public int Id { get; set; } = 1;
}

/// <summary>
/// Result of an MCP tool execution.
/// </summary>
public record McpToolResult
{
    public bool Success { get; init; }
    public string? Content { get; init; }
    public string? Error { get; init; }
}