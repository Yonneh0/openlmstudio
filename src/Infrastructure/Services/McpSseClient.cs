using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

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
                    ["clientInfo"] = new { name = "OpenLMStudio", commit = GetGitCommitShort() }
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

    private static string GetGitCommitShort()
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
                    var name = GetJsonStringValue(toolObj, "name");
                    var description = GetJsonStringValue(toolObj, "description");

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

        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
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

    private static string? GetJsonStringValue(System.Text.Json.JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
            return prop.ValueKind == System.Text.Json.JsonValueKind.String ? prop.GetString() : null;
        return null;
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