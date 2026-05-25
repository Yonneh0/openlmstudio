namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Registry of all available tools for the agent.
/// Provides lookup and registration of tool definitions.
/// </summary>
public class ToolRegistry
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