namespace OpenLMStudio.Domain.Interfaces;

using Models;

/// <summary>
/// Executes CLI commands on the system.
/// Supports cross-platform command execution via System.Diagnostics.Process.
/// </summary>
public interface ICommandExecutor
{
    /// <summary>
    /// Executes a CLI command on the system.
    /// </summary>
    /// <param name="command">The CLI command to execute.</param>
    /// <param name="requiresApproval">Whether the command requires explicit user approval.</param>
    /// <param name="timeoutSeconds">Timeout in seconds (default: 60).</param>
    /// <param name="workingDirectory">Working directory for command execution.</param>
    /// <returns>Tool result with command output.</returns>
    Task<ToolResult> ExecuteAsync(string command, bool requiresApproval, int? timeoutSeconds = null, string? workingDirectory = null);

    /// <summary>
    /// Cancels the currently running command.
    /// </summary>
    Task<bool> CancelAsync();
}
