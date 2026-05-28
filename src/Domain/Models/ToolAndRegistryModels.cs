namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Represents a tool that an agent can invoke.
/// </summary>
public record ToolDefinition
{
    /// <summary>Unique name of the tool (e.g., "write_to_file").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Human-readable description of what the tool does.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Required parameter names for this tool.</summary>
    public List<string> RequiredParameters { get; init; } = new();

    /// <summary>Parameter schema: name → type description.</summary>
    public Dictionary<string, string> ParameterSchema { get; init; } = new();

    /// <summary>Whether this tool is available in the current mode (PLAN vs ACT).</summary>
    public ToolAvailability Availability { get; set; } = ToolAvailability.All;
}

/// <summary>
/// Determines which modes a tool is available in.
/// </summary>
public enum ToolAvailability
{
    /// <summary>Available in all modes.</summary>
    All,
    /// <summary>Only available in PLAN MODE.</summary>
    PlanOnly,
    /// <summary>Only available in ACT MODE.</summary>
    ActOnly,
}

/// <summary>
/// Result from a tool execution.
/// </summary>
public record ToolResult
{
    /// <summary>Whether the tool execution succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Tool output as text (for display).</summary>
    public string? Output { get; init; }

    /// <summary>Error message if the tool failed.</summary>
    public string? Error { get; init; }

    /// <summary>Images returned by the tool (e.g., screenshots).</summary>
    public List<string>? Images { get; init; }

    /// <summary>Files modified by this tool execution.</summary>
    public List<string>? ModifiedFiles { get; init; }

    /// <summary>Duration of tool execution in milliseconds.</summary>
    public double DurationMs { get; init; }

    /// <summary>
    /// Creates a successful tool result.
    /// </summary>
    public static ToolResult Ok(string output, List<string>? modifiedFiles = null)
        => new() { Success = true, Output = output, ModifiedFiles = modifiedFiles };

    /// <summary>
    /// Creates a failed tool result.
    /// </summary>
    public static ToolResult Fail(string error)
        => new() { Success = false, Error = error };

    /// <summary>
    /// Creates a tool result with images (e.g., browser screenshots).
    /// </summary>
    public static ToolResult WithImages(string output, List<string> images)
        => new() { Success = true, Output = output, Images = images };
}

/// <summary>
/// Registry of all available tools for the agent.
/// Provides lookup and registration of tool definitions.
/// Note: This is distinct from Infrastructure.Services.ToolRegistry which handles tool discovery/instantiation.
/// </summary>
public class ToolAvailabilityRegistry
{
    private readonly Dictionary<string, ToolDefinition> _tools = new();
    private readonly object _lock = new();

    /// <summary>
    /// Registers a tool in the registry.
    /// </summary>
    public void Register(ToolDefinition tool)
    {
        lock (_lock)
        {
            _tools[tool.Name] = tool;
        }
    }

    /// <summary>
    /// Registers multiple tools at once.
    /// </summary>
    public void RegisterRange(IEnumerable<ToolDefinition> tools)
    {
        lock (_lock)
        {
            foreach (var tool in tools)
                _tools[tool.Name] = tool;
        }
    }

    /// <summary>
    /// Gets a tool definition by name.
    /// </summary>
    public ToolDefinition? GetTool(string name)
    {
        lock (_lock)
        {
            return _tools.TryGetValue(name, out var tool) ? tool : null;
        }
    }

    /// <summary>
    /// Lists all registered tools.
    /// </summary>
    public IReadOnlyCollection<ToolDefinition> ListTools()
    {
        lock (_lock)
        {
            return _tools.Values.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Checks if a tool is available in the current mode.
    /// </summary>
    public bool IsToolAvailable(string toolName, bool isPlanMode)
    {
        lock (_lock)
        {
            if (!_tools.TryGetValue(toolName, out var tool))
                return false;

            return tool.Availability switch
            {
                ToolAvailability.All => true,
                ToolAvailability.PlanOnly => isPlanMode,
                ToolAvailability.ActOnly => !isPlanMode,
                _ => true,
            };
        }
    }

    /// <summary>
    /// Unregisters a tool.
    /// </summary>
    public void Unregister(string toolName)
    {
        lock (_lock)
        {
            _tools.Remove(toolName);
        }
    }

    /// <summary>
    /// Clears all tools from the registry.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _tools.Clear();
        }
    }
}

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

/// <summary>
/// Represents a Puppeteer-controlled browser session.
/// </summary>
public class BrowserSession
{
    /// <summary>Unique identifier for this browser session.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Current URL of the browser.</summary>
    public string? CurrentUrl { get; set; }

    /// <summary>Viewport resolution (width x height).</summary>
    public int ViewportWidth { get; set; } = 1280;

    /// <summary>Viewport resolution (width x height).</summary>
    public int ViewportHeight { get; set; } = 720;

    /// <summary>Whether the browser is currently open.</summary>
    public bool IsOpen { get; set; }

    /// <summary>Last screenshot captured (base64 or file path).</summary>
    public string? LastScreenshot { get; set; }

    /// <summary>Console logs captured from the browser.</summary>
    public List<string> ConsoleLogs { get; set; } = new();

    /// <summary>
    /// Creates a new browser session with default viewport.
    /// </summary>
    public static BrowserSession Create() => new();

    /// <summary>
    /// Closes the browser session.
    /// </summary>
    public void Close()
    {
        IsOpen = false;
        CurrentUrl = null;
        LastScreenshot = null;
        ConsoleLogs.Clear();
    }
}