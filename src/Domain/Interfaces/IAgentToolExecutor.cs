namespace OpenLMStudio.Domain.Interfaces;

using Models;

/// <summary>
/// Orchestrates tool execution for the agent.
/// Routes tool calls to the appropriate service based on tool name.
/// </summary>
public interface IAgentToolExecutor
{
    /// <summary>
    /// Executes a tool by name with the given parameters.
    /// </summary>
    /// <param name="toolName">Name of the tool to execute (e.g., "write_to_file").</param>
    /// <param name="parameters">Dictionary of parameter name → value.</param>
    /// <returns>Tool result with output, errors, and metadata.</returns>
    Task<ToolResult> ExecuteAsync(string toolName, Dictionary<string, object> parameters);

    /// <summary>
    /// Lists all available tools.
    /// </summary>
    IReadOnlyCollection<ToolDefinition> ListAvailableTools();

    /// <summary>
    /// Checks if a tool is available in the current mode.
    /// </summary>
    bool IsToolAvailable(string toolName, bool isPlanMode);
}