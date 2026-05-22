using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using AgenticTask = OpenLMStudio.Domain.Models.AgenticTask;
using AgentToolCallRecord = OpenLMStudio.Domain.Models.AgentToolCallRecord;
using TaskPriority = OpenLMStudio.Domain.Models.TaskPriority;
using TaskStatus = OpenLMStudio.Domain.Models.TaskStatus;
using TaskPhase = OpenLMStudio.Domain.Models.TaskPhase;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Manages ordered task queue across branches with priority-aware scheduling,
/// batch task injection, dependency resolution, and auto-start on dependency satisfaction.
/// Delegates to TaskSchedulerService for SQLite persistence.
/// </summary>
public class TaskScheduler : ITaskScheduler
{
    private readonly TaskSchedulerService _persistenceService;
    private readonly ILogger<TaskScheduler>? _logger;
    private readonly Dictionary<Guid, List<AgenticTask>> _branchTasks = new();
    private readonly object _lock = new();

    public TaskScheduler(
        TaskSchedulerService persistenceService,
        ILogger<TaskScheduler>? logger)
    {
        _persistenceService = persistenceService;
        _logger = logger;
    }

    public async Task InjectTasksAsync(Guid branchId, IEnumerable<AgenticTask> tasks, CancellationToken ct = default)
    {
        // Persist to SQLite
        await _persistenceService.InjectTasksAsync(branchId, tasks, ct);

        // Update in-memory cache
        lock (_lock)
        {
            var added = tasks.ToList();
            if (!_branchTasks.ContainsKey(branchId))
                _branchTasks[branchId] = new List<AgenticTask>();

            _branchTasks[branchId].AddRange(added);
            _logger?.LogInformation("Injected {Count} tasks into branch {BranchId}", added.Count, branchId);
        }
    }

    public async Task<List<AgenticTask>> GetScheduledTasksAsync(Guid branchId, CancellationToken ct = default)
    {
        return await _persistenceService.GetScheduledTasksAsync(branchId, ct);
    }

    public async Task<List<AgenticTask>> GetAllScheduledTasksAsync(CancellationToken ct = default)
    {
        return await _persistenceService.GetAllScheduledTasksAsync(ct);
    }

    public async Task UpdateTaskStatusAsync(Guid taskId, TaskStatus newStatus, string? errorMessage = null, CancellationToken ct = default)
    {
        await _persistenceService.UpdateTaskStatusAsync(taskId, newStatus, errorMessage, ct);

        lock (_lock)
        {
            foreach (var branch in _branchTasks.Values)
            {
                foreach (var task in branch.Where(t => t.Id == taskId))
                {
                    task.Status = (TaskStatus)newStatus;
                    if (errorMessage != null)
                        task.ErrorMessage = errorMessage;
                }
            }
        }
    }

    public async Task UpdateTaskProgressAsync(Guid taskId, int progress, CancellationToken ct = default)
    {
        await _persistenceService.UpdateTaskProgressAsync(taskId, progress, ct);
    }

    public async Task RegisterToolCallAsync(Guid taskId, AgentToolCallRecord record, CancellationToken ct = default)
    {
        await _persistenceService.RegisterToolCallAsync(taskId, record, ct);
    }

    public async Task<List<AgenticTask>> GetReadyTasksAsync(CancellationToken ct = default)
    {
        return await _persistenceService.GetReadyTasksAsync(ct);
    }

    public async Task CheckAndStartDependentTasksAsync(Guid completedTaskId, CancellationToken ct = default)
    {
        await _persistenceService.CheckAndStartDependentTasksAsync(completedTaskId, ct);
    }

    public async Task AbandonBranchAsync(Guid branchId, CancellationToken ct = default)
    {
        await _persistenceService.AbandonBranchAsync(branchId, ct);

        lock (_lock)
        {
            if (_branchTasks.TryGetValue(branchId, out var tasks))
            {
                foreach (var task in tasks)
                {
                    if (task.Status is TaskStatus.Running or TaskStatus.Pending)
                        task.Status = TaskStatus.Cancelled;
                }
            }
            _branchTasks.Remove(branchId);
        }
    }

    public async Task PauseBranchAsync(Guid branchId, CancellationToken ct = default)
    {
        await _persistenceService.PauseBranchAsync(branchId, ct);

        lock (_lock)
        {
            if (_branchTasks.TryGetValue(branchId, out var tasks))
            {
                foreach (var task in tasks.Where(t => t.Status == TaskStatus.Running))
                    task.Status = TaskStatus.Paused;
            }
        }
    }

    public async Task ResumeBranchAsync(Guid branchId, CancellationToken ct = default)
    {
        await _persistenceService.ResumeBranchAsync(branchId, ct);

        lock (_lock)
        {
            if (_branchTasks.TryGetValue(branchId, out var tasks))
            {
                foreach (var task in tasks.Where(t => t.Status == TaskStatus.Paused))
                    task.Status = TaskStatus.Running;
            }
        }
    }

    /// <summary>
    /// Registers tasks from a branch for in-memory caching.
    /// </summary>
    public void RegisterBranch(Guid branchId, List<AgenticTask> tasks)
    {
        lock (_lock)
        {
            _branchTasks[branchId] = tasks;
        }
    }
}