namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Interface for auto-approving agent tools and commands.
/// </summary>
public interface IAgentTaskAutoApprover : IDisposable
{
    /// <summary>
    /// Checks if a tool should be auto-approved.
    /// </summary>
    bool ShouldAutoApproveTool(string toolName);

    /// <summary>
    /// Checks if a tool should be auto-approved with a specific path.
    /// </summary>
    bool ShouldAutoApproveToolWithPath(string toolName, string path);

    /// <summary>
    /// Checks if a command should be auto-approved.
    /// </summary>
    bool ShouldAutoApproveCommand(string command);

    /// <summary>
    /// Gets the timeout for a command.
    /// </summary>
    int GetCommandTimeout(string command);

    /// <summary>
    /// Checks if a command is long-running.
    /// </summary>
    bool IsLongRunningCommand(string command);

    /// <summary>
    /// Records a tool approval.
    /// </summary>
    void RecordToolApproval(string toolName, string path);

    /// <summary>
    /// Records a command approval.
    /// </summary>
    void RecordCommandApproval(string command);

    /// <summary>
    /// Resets the auto-approval history.
    /// </summary>
    void Reset();
}