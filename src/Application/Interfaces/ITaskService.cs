using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Interface for managing agentic tasks — creation, execution, dependency tracking, and persistence.
/// </summary>
public interface ITaskService : IDisposable
{
    /// <summary>
    /// Gets all tasks, optionally filtered by status.
    /// </summary>
    IReadOnlyList<Task> GetTasks();

    /// <summary>
    /// Creates a new task with the given description and optional dependencies.
    /// </summary>
    Task<Task> CreateTaskAsync(string description, List<Guid>? dependencies = null, TaskPriority priority = TaskPriority.Normal);

    /// <summary>
    /// Starts execution of a task by launching the agent with the appropriate context.
    /// </summary>
    Task StartTaskAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Pauses execution of a running task.
    /// </summary>
    Task PauseTaskAsync(Guid taskId);

    /// <summary>
    /// Resumes a paused task.
    /// </summary>
    Task ResumeTaskAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Aborts/cancels a running task.
    /// </summary>
    Task AbortTaskAsync(Guid taskId);

    /// <summary>
    /// Gets the result of a completed task.
    /// </summary>
    Task<AgentTaskResult?> GetTaskResultAsync(Guid taskId);

    /// <summary>
    /// Registers a handler for task status changes.
    /// </summary>
    event EventHandler<TaskStateChangedEventArgs>? TaskStateChanged;
}

/// <summary>
/// Event arguments for task state changes.
/// </summary>
public record TaskStateChangedEventArgs(
    Guid TaskId,
    TaskStatus OldStatus,
    TaskStatus NewStatus,
    string? Message = null);