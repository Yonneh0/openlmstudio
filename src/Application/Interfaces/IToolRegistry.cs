namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Registry for managing available tools during agent execution.
/// </summary>
public interface IToolRegistry : IDisposable
{
    /// <summary>
    /// Gets all registered tools by name.
    /// </summary>
    IReadOnlyDictionary<string, ITool> GetTools();

    /// <summary>
    /// Registers a tool in this registry so the agent can use it during execution.
    /// </summary>
    void Register(ITool tool);

    /// <summary>
    /// Unregisters a tool by name.
    /// </summary>
    bool Unregister(string name);

    /// <summary>
    /// Gets a tool by name, or null if not found.
    /// </summary>
    ITool? GetTool(string name);
}