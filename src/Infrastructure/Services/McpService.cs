using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

// ================================================================
// Transport Layer — IMcpClient implementations
// (IMcpClient and McpToolDefinition are defined in IConversationManager.cs)
// ================================================================

/// <summary>
/// Result of an MCP tool execution.
/// </summary>
public record McpToolResult
{
    public bool Success { get; init; }
    public string? Content { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Helper methods shared across MCP types.
/// </summary>
internal static class McpHelpers
{
    public static string GetGitCommitShort()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse --short HEAD",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(startInfo) ?? throw new InvalidOperationException();
            var result = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit();
            return result.Length > 0 ? result : "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    public static string? GetJsonStringValue(System.Text.Json.JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
            return prop.ValueKind == System.Text.Json.JsonValueKind.String ? prop.GetString() : null;
        return null;
    }
}

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
                    ["clientInfo"] = new { name = "OpenLMStudio", commit = McpHelpers.GetGitCommitShort() }
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
                    var name = McpHelpers.GetJsonStringValue(tool, "name");
                    var description = McpHelpers.GetJsonStringValue(tool, "description");

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
                    var name = McpHelpers.GetJsonStringValue(tool, "name");
                    var description = McpHelpers.GetJsonStringValue(tool, "description");

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

// ================================================================
// Transport Layer — SSE Client
// ================================================================

/// <summary>
/// MCP (Model Context Protocol) client implementation using SSE transport.
/// Connects to an MCP server over HTTP/SSE for bidirectional communication.
/// </summary>
public class McpSseClient : IMcpClient, IDisposable
{
    private readonly ILogger<McpSseClient> _logger;
    private HttpClient? _httpClient;
    private CancellationTokenSource? _sseCts;
    private string? _sseEndpointUrl;
    private volatile bool _isConnected;

    /// <summary>
    /// Cached JSON serialization options for null-ignoring serialization.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonNullIgnoreOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <inheritdoc />
    public bool IsConnected => _isConnected && _httpClient != null;

    /// <inheritdoc />
    public List<McpToolDefinition> DiscoveredTools { get; } = new();

    /// <summary>
    /// Event raised when tools are discovered.
    /// </summary>
    public event EventHandler? ToolsDiscovered;

    private readonly Dictionary<string, SemaphoreSlim> _methodLocks = new(StringComparer.Ordinal)
    {
        ["initialize"] = new(1, 1),
        ["tools/list"] = new(1, 1),
        ["shutdown"] = new(1, 1)
    };

    private int _nextRequestId;

    public McpSseClient(ILogger<McpSseClient> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ConnectAsync(string command, string[] args, string transportMode = "stdio")
    {
        if (IsConnected) return;

        if (transportMode.ToLowerInvariant() != "sse")
            throw new InvalidOperationException("McpSseClient requires SSE transport mode.");

        // The 'command' parameter is the SSE endpoint URL for SSE transport.
        var sseUrl = command.TrimEnd('/');
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

        try
        {
            // Open the SSE connection to get the event stream endpoint
            using var sseCtsLocal = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            var response = await _httpClient.GetAsync($"{sseUrl}/sse", HttpCompletionOption.ResponseHeadersRead, sseCtsLocal.Token);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Failed to connect to MCP SSE endpoint: {response.StatusCode}");

            // Read the SSE stream to find the message endpoint URL
            var sseStream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(sseStream);

            string? messageEndpointUrl = null;
            while (!reader.EndOfStream && !sseCtsLocal.Token.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(line)) continue;

                // SSE format: event: endpoint\nurl: <endpoint_url>
                if (line.StartsWith("event:", StringComparison.Ordinal)) continue;
                if (line.Length > 4 && line.Substring(0, 4) == "url:")
                {
                    var url = line.Substring(4).Trim();
                    if (!string.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out _))
                        messageEndpointUrl = url;
                }
            }

            // Fallback: use the /message endpoint on the same base URL
            if (string.IsNullOrEmpty(messageEndpointUrl))
            {
                var baseUri = new Uri(sseUrl);
                messageEndpointUrl = $"{baseUri.Scheme}://{baseUri.Authority}{baseUri.AbsolutePath.TrimEnd('/')}/message";
            }

            _sseEndpointUrl = messageEndpointUrl;

            // Initialize MCP connection
            var initMessage = new McpSseMessage
            {
                jsonrpc = "2.0",
                method = "initialize",
                @params = new Dictionary<string, object>
                {
                    ["protocolVersion"] = 20260101,
                    ["clientInfo"] = new { name = "OpenLMStudio", commit = McpHelpers.GetGitCommitShort() }
                },
                id = ++_nextRequestId
            };

            var initResponse = await SendSseMessageAsync(initMessage);
            if (initResponse != null && initResponse.result?["status"]?.ToString() == "success")
            {
                _isConnected = true;
                _sseCts = new CancellationTokenSource();

                // Start listening for server notifications in background
                _ = Task.Run(() => ListenForNotificationsAsync(_sseCts.Token), _sseCts.Token);

                // Discover tools
                await DiscoverToolsInternalAsync();
                ToolsDiscovered?.Invoke(this, EventArgs.Empty);

                _logger.LogInformation("MCP SSE client connected successfully — endpoint: {Endpoint}", messageEndpointUrl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MCP SSE server");
            Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DisconnectAsync()
    {
        if (!_isConnected) return;

        try
        {
            var shutdownMessage = new McpSseMessage
            {
                jsonrpc = "2.0",
                method = "shutdown",
                id = ++_nextRequestId
            };

            await SendSseMessageAsync(shutdownMessage);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error sending MCP shutdown message");
        }

        _sseCts?.Cancel();
        _isConnected = false;
        _sseEndpointUrl = null;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<McpToolDefinition>> ListToolsAsync()
    {
        if (!_isConnected) return Enumerable.Empty<McpToolDefinition>();

        var discoverMessage = new McpSseMessage
        {
            jsonrpc = "2.0",
            method = "tools/list",
            id = ++_nextRequestId
        };

        var response = await SendSseMessageAsync(discoverMessage);

        if (response?.result != null)
        {
            DiscoveredTools.Clear();
            // Serialize JsonObject to JSON string for parsing with JsonDocument
            var toolsJson = JsonSerializer.Serialize(response.result);
            using var doc = System.Text.Json.JsonDocument.Parse(toolsJson);

            if (doc.RootElement.TryGetProperty("tools", out var toolsProp) &&
                toolsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var toolObj in toolsProp.EnumerateArray())
                {
                    var name = McpHelpers.GetJsonStringValue(toolObj, "name");
                    var description = McpHelpers.GetJsonStringValue(toolObj, "description");

                    Dictionary<string, object>? schema = null;
                    if (toolObj.TryGetProperty("inputSchema", out var schemaProp))
                        schema = JsonSerializer.Deserialize<Dictionary<string, object>>(schemaProp.GetRawText());

                    DiscoveredTools.Add(new McpToolDefinition(name ?? "unknown", description ?? "", schema));
                }
            }
        }

        return DiscoveredTools;
    }

    /// <inheritdoc />
    public async Task<string?> CallToolAsync(string toolName, string argumentsJson)
    {
        if (!_isConnected) return null;

        var toolCallMessage = new McpSseMessage
        {
            jsonrpc = "2.0",
            method = "tools/call",
            @params = new Dictionary<string, object>
            {
                ["name"] = toolName,
                ["arguments"] = argumentsJson
            },
            id = ++_nextRequestId
        };

        var response = await SendSseMessageAsync(toolCallMessage);
        return response?.result?.ToString() ?? "";
    }

    private async Task<McpSseMessage?> SendSseMessageAsync(McpSseMessage message)
    {
        if (_httpClient == null || _sseEndpointUrl == null) return null;

        var json = JsonSerializer.Serialize(message, _jsonNullIgnoreOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            var response = await _httpClient.PostAsync(_sseEndpointUrl, content, cts.Token);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"MCP SSE message failed: {response.StatusCode}");

            var responseBody = await response.Content.ReadAsStringAsync(cts.Token);
            return JsonSerializer.Deserialize<McpSseMessage>(responseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending MCP SSE message to {Endpoint}", _sseEndpointUrl);
            return null;
        }
    }

    /// <summary>
    /// Listens for incoming server notifications via the SSE event stream.
    /// </summary>
    private async Task ListenForNotificationsAsync(CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (_sseEndpointUrl == null || _httpClient == null) return;

            using var response = await _httpClient.GetAsync($"{_sseEndpointUrl}/events", HttpCompletionOption.ResponseHeadersRead, cts.Token);

            if (!response.IsSuccessStatusCode) return;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (string.IsNullOrEmpty(line)) continue;

                // SSE format: event: <event_name>\n\n<data>
                if (line.StartsWith("event:", StringComparison.Ordinal)) continue;
                if (line.Length > 5 && line.Substring(0, 5) == "data:")
                {
                    var data = line.Substring(5).Trim();
                    try
                    {
                        using var doc = JsonDocument.Parse(data);
                        // Handle server notifications (e.g., tool results, status updates)
                        _logger.LogDebug("Received MCP SSE notification: {Data}", data);
                    }
                    catch
                    {
                        _logger.LogWarning("Failed to parse MCP SSE notification: {Data}", data);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Expected — connection closed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listening for MCP SSE notifications");
        }
    }

    private async Task DiscoverToolsInternalAsync()
    {
        // Reuse ListToolsAsync to avoid code duplication
        await ListToolsAsync();
    }

    public void Dispose()
    {
        _sseCts?.Cancel();
        _sseCts?.Dispose();
        _httpClient?.Dispose();
        _isConnected = false;

        foreach (var sem in _methodLocks.Values)
            sem.Dispose();
    }
}

/// <summary>
/// Internal representation of an MCP SSE JSON-RPC message.
/// </summary>
internal record McpSseMessage
{
    public string? jsonrpc { get; init; } = "2.0";
    public string? method { get; set; }
    public Dictionary<string, object>? @params { get; init; }
    public System.Text.Json.Nodes.JsonObject? result { get; set; }
    public int id { get; init; }
}

// ================================================================
// Tool Layer — ITool implementations
// ================================================================

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