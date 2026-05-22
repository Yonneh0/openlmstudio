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
    Task<AgenticTask?> GetNextTaskAsync(Guid branchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all tasks across all branches, ordered by priority and dependency satisfaction.
    /// </summary>
    Task<List<AgenticTask>> GetAllOrderedTasksAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Injects multiple tasks at once into a branch, respecting dependency ordering.
    /// </summary>
    Task<List<AgenticTask>> InjectTasksAsync(Guid branchId, IEnumerable<AgenticTask> tasks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a task as complete and starts any tasks whose dependencies are now satisfied.
    /// </summary>
    Task OnTaskCompletedAsync(AgenticTask task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a task as failed and handles dependent task updates.
    /// </summary>
    Task OnTaskFailedAsync(AgenticTask task, string errorMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all pending tasks for a branch that are ready to start (dependencies satisfied).
    /// </summary>
    Task<List<AgenticTask>> GetReadyTasksAsync(Guid branchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets blocked tasks in a branch (waiting for dependencies).
    /// </summary>
    Task<List<AgenticTask>> GetBlockedTasksAsync(Guid branchId, CancellationToken cancellationToken = default);

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