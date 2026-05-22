// Brought to you by Carls' Jr.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Interface for CRUD operations on agentic tasks.
/// </summary>
public interface ITaskRepository : IDisposable
{
    /// <summary>
    /// Creates a new task and persists it.
    /// </summary>
    Task<Domain.Models.TaskEntity> CreateTaskAsync(Domain.Models.TaskEntity task, CancellationToken ct = default);

    /// <summary>
    /// Gets a task by its ID.
    /// </summary>
    Task<Domain.Models.TaskEntity?> GetTaskAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing task with new values.
    /// </summary>
    Task UpdateTaskAsync(Domain.Models.TaskEntity task, CancellationToken ct = default);

    /// <summary>
    /// Deletes a task by its ID.
    /// </summary>
    Task DeleteTaskAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Lists all tasks, optionally filtered by status.
    /// </summary>
    Task<IReadOnlyList<Domain.Models.TaskEntity>> ListTasksAsync(Domain.Models.TaskStatus? filterStatus = null, CancellationToken ct = default);

    /// <summary>
    /// Lists child tasks for a parent task.
    /// </summary>
    Task<IReadOnlyList<Domain.Models.TaskEntity>> ListChildTasksAsync(Guid parentTaskId, CancellationToken ct = default);

    /// <summary>
    /// Finds tasks that are ready to run (pending/queued and all dependencies satisfied).
    /// </summary>
    Task<IReadOnlyList<Domain.Models.TaskEntity>> FindReadyTasksAsync(CancellationToken ct = default);

    /// <summary>
    /// Updates task status and progress atomically.
    /// </summary>
    Task UpdateStatusAsync(Guid taskId, Domain.Models.TaskStatus newStatus, int? newProgress = null, CancellationToken ct = default);
}