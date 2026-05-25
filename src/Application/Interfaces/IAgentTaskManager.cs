using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Interface for the central agent task manager.
/// Manages the task lifecycle: init, execute, complete, terminate.
/// </summary>
public interface IAgentTaskManager : IDisposable
{
    /// <summary>
    /// Gets the current task state.
    /// </summary>
    AgentTaskState? CurrentState { get; }

    /// <summary>
    /// Gets the current task settings.
    /// </summary>
    AgentTaskSettings? CurrentSettings { get; }

    /// <summary>
    /// Gets the current task ID.
    /// </summary>
    Guid? CurrentTaskId { get; }

    /// <summary>
    /// Initializes a new task.
    /// </summary>
    /// <param name="taskString">Task description string.</param>
    /// <param name="images">Optional images for the task.</param>
    /// <param name="files">Optional files for the task.</param>
    /// <param name="historyItem">Optional history item to resume from.</param>
    /// <param name="taskSettings">Optional task settings.</param>
    /// <returns>The new task ID.</returns>
    Task<Guid> InitTaskAsync(string taskString, List<string>? images = null, List<string>? files = null,
        HistoryItem? historyItem = null, AgentTaskSettings? taskSettings = null);

    /// <summary>
    /// Cancels the current task.
    /// </summary>
    Task CancelTaskAsync();

    /// <summary>
    /// Resumes a task from history.
    /// </summary>
    /// <param name="historyItem">The history item to resume from.</param>
    Task ResumeTaskFromHistoryAsync(HistoryItem historyItem);

    /// <summary>
    /// Executes the tool loop.
    /// </summary>
    Task ExecuteToolLoopAsync();

    /// <summary>
    /// Completes the task using attempt_completion.
    /// </summary>
    /// <param name="result">The completion result.</param>
    /// <param name="command">Optional command to execute.</param>
    Task CompleteTaskAsync(string result, string? command = null);

    /// <summary>
    /// Terminates the current task.
    /// </summary>
    Task TerminateTaskAsync();

    /// <summary>
    /// Updates task history.
    /// </summary>
    Task UpdateTaskHistoryAsync(HistoryItem historyItem);

    /// <summary>
    /// Switches to Act mode.
    /// </summary>
    Task SwitchToActModeAsync();

    /// <summary>
    /// Switches to Plan mode.
    /// </summary>
    Task SwitchToPlanModeAsync();

    /// <summary>
    /// Executes a command tool.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="timeoutSeconds">Timeout in seconds.</param>
    /// <param name="options">Optional execution options.</param>
    Task<(bool rejected, string result)> ExecuteCommandToolAsync(string command, int timeoutSeconds = 30, Dictionary<string, object>? options = null);

    /// <summary>
    /// Cancels a running command tool.
    /// </summary>
    Task<bool> CancelRunningCommandToolAsync();

    /// <summary>
    /// Checks if the latest task completion has new changes.
    /// </summary>
    Task<bool> DoesLatestTaskCompletionHaveNewChangesAsync();

    /// <summary>
    /// Updates the focus chain list from a tool response.
    /// </summary>
    Task UpdateFCListFromToolResponseAsync(AgentTaskProgress progress);

    /// <summary>
    /// Says a message and creates a missing parameter error.
    /// </summary>
    Task<string> SayAndCreateMissingParamErrorAsync(string toolName, string parameterName, string? relativePath);

    /// <summary>
    /// Removes the last partial message if it exists with a specific type.
    /// </summary>
    Task RemoveLastPartialMessageIfExistsWithTypeAsync(string messageType, string askOrSay);

    /// <summary>
    /// Applies the latest browser settings.
    /// </summary>
    Task ApplyLatestBrowserSettingsAsync();
}

/// <summary>
/// Represents a history item for task resumption.
/// </summary>
public record HistoryItem(
    Guid Id,
    string Title,
    IReadOnlyList<HistoryMessage> Messages,
    DateTime CreatedAt,
    string? Summary = null);

/// <summary>
/// Represents a message in a history item.
/// </summary>
public record HistoryMessage(
    string Role,
    string Content,
    DateTime Timestamp);