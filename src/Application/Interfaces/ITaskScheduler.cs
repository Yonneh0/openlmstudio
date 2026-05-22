using OpenLMStudio.Domain.Models;
using TaskStatus = OpenLMStudio.Domain.Models.TaskStatus;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Manages an ordered task queue across all branches with priority-aware scheduling,
/// batch task injection, dependency resolution, and auto-start on dependency satisfaction.
/// </summary>
public interface ITaskScheduler
{
    /// <summary>
    /// Injects a batch of tasks into a branch, resolving cross-branch dependencies.
    /// Auto-starts tasks whose dependencies are all satisfied.
    /// </summary>
    Task InjectTasksAsync(Guid branchId, IEnumerable<AgenticTask> tasks, CancellationToken ct = default);

    /// <summary>
    /// Gets the current ordered task queue, sorted by priority then creation time.
    /// </summary>
    Task<List<AgenticTask>> GetScheduledTasksAsync(Guid branchId, CancellationToken ct = default);

    /// <summary>
    /// Gets the full task queue across all branches.
    /// </summary>
    Task<List<AgenticTask>> GetAllScheduledTasksAsync(CancellationToken ct = default);

    /// <summary>
    /// Updates task status and optionally marks it complete.
    /// </summary>
    Task UpdateTaskStatusAsync(Guid taskId, TaskStatus newStatus, string? errorMessage = null, CancellationToken ct = default);

    /// <summary>
    /// Registers tasks from a branch for in-memory caching by a scheduler wrapper.
    /// </summary>
    void RegisterBranch(Guid branchId, List<AgenticTask> tasks);

    /// <summary>
    /// Updates task progress percentage.
    /// </summary>
    Task UpdateTaskProgressAsync(Guid taskId, int progress, CancellationToken ct = default);

    /// <summary>
    /// Registers a tool call result for a task.
    /// </summary>
    Task RegisterToolCallAsync(Guid taskId, AgentToolCallRecord record, CancellationToken ct = default);

    /// <summary>
    /// Gets tasks that are ready to run (pending with all dependencies satisfied).
    /// </summary>
    Task<List<AgenticTask>> GetReadyTasksAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks and auto-starts tasks whose dependencies just completed.
    /// Should be called after a task completes.
    /// </summary>
    Task CheckAndStartDependentTasksAsync(Guid completedTaskId, CancellationToken ct = default);

    /// <summary>
    /// Abandons a branch and all its pending tasks.
    /// </summary>
    Task AbandonBranchAsync(Guid branchId, CancellationToken ct = default);

    /// <summary>
    /// Pauses a branch (all tasks in it).
    /// </summary>
    Task PauseBranchAsync(Guid branchId, CancellationToken ct = default);

    /// <summary>
    /// Resumes a paused branch.
    /// </summary>
    Task ResumeBranchAsync(Guid branchId, CancellationToken ct = default);
}