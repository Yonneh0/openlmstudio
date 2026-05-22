using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Manages ordered task queue across branches with priority-aware scheduling,
/// batch task injection, and dependency resolution.
/// </summary>
public interface ITaskScheduler
{
    /// <summary>
    /// Gets the next task to execute based on priority and dependency satisfaction.
    /// </summary>
    Task<Task?> GetNextTaskAsync(Guid branchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all tasks across all branches, ordered by priority and dependency satisfaction.
    /// </summary>
    Task<List<Task>> GetAllOrderedTasksAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Injects multiple tasks at once into a branch, respecting dependency ordering.
    /// </summary>
    Task<List<Task>> InjectTasksAsync(Guid branchId, IEnumerable<Task> tasks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a task as complete and starts any tasks whose dependencies are now satisfied.
    /// </summary>
    Task OnTaskCompletedAsync(Task task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a task as failed and handles dependent task updates.
    /// </summary>
    Task OnTaskFailedAsync(Task task, string errorMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all pending tasks for a branch that are ready to start (dependencies satisfied).
    /// </summary>
    Task<List<Task>> GetReadyTasksAsync(Guid branchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets blocked tasks in a branch (waiting for dependencies).
    /// </summary>
    Task<List<Task>> GetBlockedTasksAsync(Guid branchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses all tasks in a branch.
    /// </summary>
    Task PauseBranchAsync(Guid branchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes all tasks in a branch.
    /// </summary>
    Task ResumeBranchAsync(Guid branchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Abandons a branch and all its tasks.
    /// </summary>
    Task AbandonBranchAsync(Guid branchId, CancellationToken cancellationToken = default);
}