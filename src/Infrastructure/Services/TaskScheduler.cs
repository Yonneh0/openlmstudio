using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Manages ordered task queue across branches with priority-aware scheduling,
/// batch task injection, and dependency resolution.
/// </summary>
public class TaskScheduler : ITaskScheduler
{
    private readonly ITaskService _taskService;
    private readonly ITaskBranchStore? _branchStore;
    private readonly ILogger<TaskScheduler>? _logger;
    private readonly Dictionary<Guid, List<AgenticTask>> _branchTasks = new();
    private readonly object _lock = new();

    public TaskScheduler(
        ITaskService taskService,
        ITaskBranchStore? branchStore,
        ILogger<TaskScheduler>? logger)
    {
        _taskService = taskService;
        _branchStore = branchStore;
        _logger = logger;
    }

    public Task<AgenticTask?> GetNextTaskAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_branchTasks.TryGetValue(branchId, out var tasks))
                return Task.FromResult<AgenticTask?>(null);

            var readyTasks = tasks
                .Where(t => t.Status == Domain.Models.TaskStatus.Pending)
                .Where(t => t.Dependencies.All(d => tasks.Any(tt => tt.Id == d && tt.Status == Domain.Models.TaskStatus.Completed)))
                .OrderByDescending(t => t.Priority)
                .ThenBy(t => t.CreatedAt)
                .ToList();

            return Task.FromResult(readyTasks.FirstOrDefault());
        }
    }

    public Task<List<AgenticTask>> GetAllOrderedTasksAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var allTasks = _branchTasks.Values.SelectMany(t => t).ToList();
            return Task.FromResult(allTasks
                .OrderByDescending(t => t.Priority)
                .ThenBy(t => t.CreatedAt)
                .ToList());
        }
    }

    public Task<List<AgenticTask>> InjectTasksAsync(Guid branchId, IEnumerable<AgenticTask> tasks, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_branchTasks.ContainsKey(branchId))
                _branchTasks[branchId] = new List<AgenticTask>();

            var added = tasks.ToList();
            _branchTasks[branchId].AddRange(added);

            // Update branch if store exists
            _branchStore?.UpdateBranchTasksAsync(branchId, _branchTasks[branchId].Select(t => t.Id).ToList()).ConfigureAwait(false);

            _logger?.LogInformation("Injected {Count} tasks into branch {BranchId}", added.Count, branchId);
            return Task.FromResult(added);
        }
    }

    public Task OnTaskCompletedAsync(AgenticTask task, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            // Find and unblock dependent tasks
            foreach (var branch in _branchTasks.Values)
            {
                foreach (var dependentTask in branch.Where(t => t.Dependencies.Contains(task.Id) && t.Status == Domain.Models.TaskStatus.Pending))
                {
                    var depsSatisfied = dependentTask.Dependencies.All(d => branch.Any(tt => tt.Id == d && tt.Status == Domain.Models.TaskStatus.Completed));
                    if (depsSatisfied)
                    {
                        _logger?.LogDebug("Unblocked dependent task {TaskId}", dependentTask.Id);
                    }
                }
            }

            // Update branch status
            UpdateBranchStatus(task.BranchId);
        }
        return Task.CompletedTask;
    }

    public Task OnTaskFailedAsync(AgenticTask task, string errorMessage, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            foreach (var branch in _branchTasks.Values)
            {
                foreach (var dependentTask in branch.Where(t => t.Dependencies.Contains(task.Id) && t.Status == Domain.Models.TaskStatus.Pending))
                {
                    dependentTask.Status = Domain.Models.TaskStatus.Failed;
                    dependentTask.ErrorMessage = $"Dependency {task.Id} failed: {errorMessage}";
                    _logger?.LogWarning("Failed dependent task {TaskId} due to dependency failure", dependentTask.Id);
                }
            }

            UpdateBranchStatus(task.BranchId);
        }
        return Task.CompletedTask;
    }

    public Task<List<AgenticTask>> GetReadyTasksAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_branchTasks.TryGetValue(branchId, out var tasks))
                return Task.FromResult(new List<AgenticTask>());

            return Task.FromResult(tasks
                .Where(t => t.Status == Domain.Models.TaskStatus.Pending)
                .Where(t => t.Dependencies.All(d => tasks.Any(tt => tt.Id == d && tt.Status == Domain.Models.TaskStatus.Completed)))
                .OrderByDescending(t => t.Priority)
                .ToList());
        }
    }

    public Task<List<AgenticTask>> GetBlockedTasksAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_branchTasks.TryGetValue(branchId, out var tasks))
                return Task.FromResult(new List<AgenticTask>());

            return Task.FromResult(tasks
                .Where(t => t.Status == Domain.Models.TaskStatus.Pending)
                .Where(t => t.Dependencies.Any(d => !tasks.Any(tt => tt.Id == d && tt.Status == Domain.Models.TaskStatus.Completed)))
                .OrderByDescending(t => t.Priority)
                .ToList());
        }
    }

    public Task PauseBranchAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_branchTasks.TryGetValue(branchId, out var tasks))
            {
                foreach (var task in tasks.Where(t => t.Status == Domain.Models.TaskStatus.Running))
                    task.Status = Domain.Models.TaskStatus.Paused;
            }
        }
        return Task.CompletedTask;
    }

    public Task ResumeBranchAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_branchTasks.TryGetValue(branchId, out var tasks))
            {
                foreach (var task in tasks.Where(t => t.Status == Domain.Models.TaskStatus.Paused))
                    task.Status = Domain.Models.TaskStatus.Running;
            }
        }
        return Task.CompletedTask;
    }

    public Task AbandonBranchAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_branchTasks.TryGetValue(branchId, out var tasks))
            {
                foreach (var task in tasks)
                {
                    if (task.Status == Domain.Models.TaskStatus.Running || task.Status == Domain.Models.TaskStatus.Pending)
                        task.Status = Domain.Models.TaskStatus.Cancelled;
                }
            }
            _branchTasks.Remove(branchId);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Registers tasks from a branch for scheduling.
    /// </summary>
    public void RegisterBranch(Guid branchId, List<AgenticTask> tasks)
    {
        lock (_lock)
        {
            _branchTasks[branchId] = tasks;
        }
    }

    private void UpdateBranchStatus(Guid branchId)
    {
        if (_branchStore == null || !_branchTasks.TryGetValue(branchId, out var tasks))
            return;

        if (tasks.Any(t => t.Status is Domain.Models.TaskStatus.Running or Domain.Models.TaskStatus.Pending))
        {
            _branchStore.UpdateBranchStatusAsync(branchId, TaskBranchStatus.Active).Wait();
        }
        else if (tasks.All(t => t.Status == Domain.Models.TaskStatus.Completed))
        {
            _branchStore.UpdateBranchStatusAsync(branchId, TaskBranchStatus.Completed).Wait();
        }
        else if (tasks.Any(t => t.Status == Domain.Models.TaskStatus.Failed))
        {
            _branchStore.UpdateBranchStatusAsync(branchId, TaskBranchStatus.Abandoned).Wait();
        }
    }
}

/// <summary>
/// Storage interface for task branches.
/// </summary>
public interface ITaskBranchStore
{
    Task CreateBranchAsync(TaskBranch branch, CancellationToken cancellationToken = default);
    Task UpdateBranchStatusAsync(Guid branchId, TaskBranchStatus status, CancellationToken cancellationToken = default);
    Task UpdateBranchTasksAsync(Guid branchId, List<Guid> taskIds, CancellationToken cancellationToken = default);
    Task<TaskBranch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default);
    Task<List<TaskBranch>> GetAllBranchesAsync(CancellationToken cancellationToken = default);
    Task DeleteBranchAsync(Guid branchId, CancellationToken cancellationToken = default);
}