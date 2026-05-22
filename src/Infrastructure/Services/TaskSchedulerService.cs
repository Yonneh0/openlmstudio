using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models;
using TaskStatus = OpenLMStudio.Domain.Models.TaskStatus;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Represents a task scheduler with access control for the shared task tree.
/// - Pingu: admin (full read/write/delete, priority manipulation)
/// - User: view/manipulate (read/write, no delete)
/// - Hosted AIs (image, text, etc.): read-only
/// </summary>
public class TaskSchedulerService : ITaskSchedulerService
{
    private readonly ILogger<TaskSchedulerService>? _logger;
    private readonly ConcurrentDictionary<Guid, AgenticTask> _tasks = new();
    private readonly ConcurrentDictionary<Guid, List<Guid>> _dependents = new();
    private readonly object _lock = new();

    public TaskSchedulerService(ILogger<TaskSchedulerService>? logger)
    {
        _logger = logger;
    }

    public void AddTask(AgenticTask task)
    {
        lock (_lock)
        {
            _tasks[task.Id] = task;
            _dependents[task.Id] = new List<Guid>();
            foreach (var depId in task.Dependencies)
            {
                if (_dependents.TryGetValue(depId, out var deps))
                    deps.Add(task.Id);
            }
            _logger?.LogDebug("Added task {TaskId} to scheduler (deps: {Count})", task.Id, task.Dependencies.Count);
        }
    }

    public bool UpdateTask(AgenticTask task, TaskSchedulerAccess access)
    {
        if (access == TaskSchedulerAccess.ReadOnly)
            return false;

        lock (_lock)
        {
            if (!_tasks.TryGetValue(task.Id, out var existing))
                return false;

            if (access == TaskSchedulerAccess.Write)
            {
                existing.Status = task.Status;
                existing.Summary = task.Summary ?? existing.Summary;
                existing.ErrorMessage = task.ErrorMessage ?? existing.ErrorMessage;
                existing.CompletedAt = task.CompletedAt;
            }

            _logger?.LogDebug("Updated task {TaskId} with access {Access}", task.Id, access);
            return true;
        }
    }

    public bool DeleteTask(Guid taskId, TaskSchedulerAccess access)
    {
        if (access != TaskSchedulerAccess.Admin)
            return false;

        lock (_lock)
        {
            if (_tasks.TryRemove(taskId, out _))
            {
                _dependents.TryRemove(taskId, out _);
                // Unlink from parent dependents
                foreach (var deps in _dependents.Values)
                    deps.Remove(taskId);
                _logger?.LogDebug("Deleted task {TaskId}", taskId);
                return true;
            }
            return false;
        }
    }

    public bool SetPriority(Guid taskId, TaskPriority priority, TaskSchedulerAccess access)
    {
        if (access != TaskSchedulerAccess.Admin)
            return false;

        lock (_lock)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                task.Priority = priority;
                _logger?.LogDebug("Set priority for task {TaskId} to {Priority}", taskId, priority);
                return true;
            }
            return false;
        }
    }

    public bool AddDependency(Guid taskId, Guid dependencyId, TaskSchedulerAccess access)
    {
        if (access != TaskSchedulerAccess.Admin)
            return false;

        lock (_lock)
        {
            if (_tasks.TryGetValue(taskId, out var task) && _tasks.TryGetValue(dependencyId, out _))
            {
                if (!task.Dependencies.Contains(dependencyId))
                {
                    task.Dependencies.Add(dependencyId);
                    if (_dependents.TryGetValue(dependencyId, out var deps))
                        deps.Add(taskId);
                }
                return true;
            }
            return false;
        }
    }

    public IReadOnlyList<AgenticTask> GetReadyTasks()
    {
        lock (_lock)
        {
            return _tasks.Values
                .Where(t => t.Status == TaskStatus.Pending)
                .Where(t => t.Dependencies.All(d => _tasks.TryGetValue(d, out var dep) && dep.Status == TaskStatus.Completed))
                .OrderByDescending(t => t.Priority)
                .ThenBy(t => t.CreatedAt)
                .ToList()
                .AsReadOnly();
        }
    }

    public IReadOnlyList<AgenticTask> GetAllTasks()
    {
        lock (_lock)
        {
            return _tasks.Values
                .OrderByDescending(t => t.Priority)
                .ThenBy(t => t.CreatedAt)
                .ToList()
                .AsReadOnly();
        }
    }

    public IReadOnlyList<AgenticTask> GetDependents(Guid taskId)
    {
        lock (_lock)
        {
            if (_dependents.TryGetValue(taskId, out var deps))
                return deps.Select(d => _tasks[d]).Where(t => t != null).ToList().AsReadOnly();
            return Array.Empty<AgenticTask>().AsReadOnly();
        }
    }

    public bool TryGetTask(Guid taskId, out AgenticTask? task)
    {
        lock (_lock)
            return _tasks.TryGetValue(taskId, out task);
    }

    public void MarkCompleted(Guid taskId)
    {
        lock (_lock)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                task.Status = TaskStatus.Completed;
                task.CompletedAt = DateTime.UtcNow;
            }
        }
    }

    public void MarkFailed(Guid taskId, string? errorMessage = null)
    {
        lock (_lock)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                task.Status = TaskStatus.Failed;
                task.ErrorMessage = errorMessage;
            }
        }
    }
}

/// <summary>
/// Access levels for the task scheduler tree.
/// </summary>
public enum TaskSchedulerAccess
{
    /// <summary>Read-only — hosted AIs (text models, image models, etc.)</summary>
    ReadOnly,
    /// <summary>Write — user can create/edit tasks</summary>
    Write,
    /// <summary>Admin — Pingu can do everything including delete, re-prioritize, manage dependencies</summary>
    Admin
}

/// <summary>
/// Service interface for the task scheduler with access control.
/// </summary>
public interface ITaskSchedulerService
{
    void AddTask(AgenticTask task);
    bool UpdateTask(AgenticTask task, TaskSchedulerAccess access);
    bool DeleteTask(Guid taskId, TaskSchedulerAccess access);
    bool SetPriority(Guid taskId, TaskPriority priority, TaskSchedulerAccess access);
    bool AddDependency(Guid taskId, Guid dependencyId, TaskSchedulerAccess access);
    IReadOnlyList<AgenticTask> GetReadyTasks();
    IReadOnlyList<AgenticTask> GetAllTasks();
    IReadOnlyList<AgenticTask> GetDependents(Guid taskId);
    bool TryGetTask(Guid taskId, out AgenticTask? task);
    void MarkCompleted(Guid taskId);
    void MarkFailed(Guid taskId, string? errorMessage = null);
}