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