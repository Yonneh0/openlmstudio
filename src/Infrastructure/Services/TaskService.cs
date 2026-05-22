using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Manages agentic tasks — creation, execution via IAgent, dependency tracking, and persistence.
/// </summary>
public class TaskService : ITaskService
{
    private readonly ILogger<TaskService>? _logger;
    private readonly IAgent? _agent;
    private readonly ITaskContextStore? _contextStore;
    private readonly ITaskContextInheritor? _contextInheritor;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<Guid, AgenticTask> _tasks = new();
    private readonly Dictionary<Guid, IAgent> _agentInstances = new();
    private readonly Dictionary<Guid, CancellationTokenSource> _activeCts = new();
    private readonly object _lock = new();

    public TaskService(
        ILogger<TaskService>? logger,
        IAgent? agent,
        ITaskContextStore? contextStore,
        ITaskContextInheritor? contextInheritor,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _agent = agent;
        _contextStore = contextStore;
        _contextInheritor = contextInheritor;
        _serviceProvider = serviceProvider;
    }

    public IReadOnlyList<AgenticTask> GetTasks()
    {
        lock (_lock)
            return _tasks.Values.ToList().AsReadOnly();
    }

    public async Task<AgenticTask> CreateTaskAsync(string description, List<Guid>? dependencies = null, TaskPriority priority = TaskPriority.Normal)
    {
        var task = new AgenticTask
        {
            Description = description,
            Dependencies = dependencies ?? new List<Guid>(),
            Priority = priority
        };

        lock (_lock)
            _tasks[task.Id] = task;

        _logger?.LogInformation("Task created: {TaskId} - {Description}", task.Id, description);
        return task;
    }

    public async Task StartTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        AgenticTask? task;
        IAgent agentInstance;
        CancellationTokenSource cts;

        lock (_lock)
        {
            if (!_tasks.TryGetValue(taskId, out task) || task == null)
                throw new KeyNotFoundException($"Task {taskId} not found.");

            if (task.Status != Domain.Models.TaskStatus.Pending && task.Status != Domain.Models.TaskStatus.Paused)
                throw new InvalidOperationException($"Task {taskId} is in status {task.Status}, cannot start.");

            // Check dependencies
            var pendingDeps = task.Dependencies
                .Where(d => _tasks.TryGetValue(d, out var dep) && dep.Status != Domain.Models.TaskStatus.Completed)
                .ToList();
            if (pendingDeps.Any())
                throw new InvalidOperationException($"Task {taskId} has pending dependencies: {string.Join(", ", pendingDeps)}");

            task.Status = Domain.Models.TaskStatus.Running;
            task.StartedAt = DateTime.UtcNow;

            cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _activeCts[taskId] = cts;

            // Clone the shared agent for this task
            var scope = _serviceProvider.CreateScope();
            agentInstance = scope.ServiceProvider.GetRequiredService<IAgent>();
            _agentInstances[taskId] = agentInstance;
        }

        // Build context: inherit from parent if exists
        IReadOnlyList<ContextSegment>? initialContext = null;
        if (_contextInheritor != null && task.ParentTaskId.HasValue)
        {
            try
            {
                var snapshot = await _contextInheritor.CreateChildInheritanceAsync(task.ParentTaskId.Value, taskId);
                if (snapshot != null)
                {
                    initialContext = snapshot.CompressedContext;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to inherit context from parent task {ParentTaskId}", task.ParentTaskId.Value);
            }
        }

        var request = new AgentTaskRequest(
            taskId,
            task.Description,
            null!,
            initialContext,
            task.MaxIterations);

        // Run agent execution on background thread
        _ = Task.Run(async () =>
        {
            try
            {
                var result = await agentInstance.ExecuteAsync(request, cts.Token);

                lock (_lock)
                {
                    task.Status = Domain.Models.TaskStatus.Completed;
                    task.Summary = result.Summary;
                    task.CompletedAt = DateTime.UtcNow;
                    task.ToolCalls = result.ToolCalls.ToList();
                }

                NotifyStateChanged(taskId, Domain.Models.TaskStatus.Running, Domain.Models.TaskStatus.Completed, result.Summary);
                _logger?.LogInformation("Task completed: {TaskId}", taskId);
            }
            catch (OperationCanceledException)
            {
                lock (_lock)
                {
                    task.Status = Domain.Models.TaskStatus.Cancelled;
                    task.CompletedAt = DateTime.UtcNow;
                }
                NotifyStateChanged(taskId, Domain.Models.TaskStatus.Running, Domain.Models.TaskStatus.Cancelled, "Task cancelled.");
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    task.Status = Domain.Models.TaskStatus.Failed;
                    task.ErrorMessage = ex.Message;
                    task.CompletedAt = DateTime.UtcNow;
                }
                NotifyStateChanged(taskId, Domain.Models.TaskStatus.Running, Domain.Models.TaskStatus.Failed, ex.Message);
                _logger?.LogError(ex, "Task failed: {TaskId}", taskId);
            }
            finally
            {
                lock (_lock)
                {
                    cts?.Cancel();
                    cts?.Dispose();
                    _activeCts.Remove(taskId);
                    agentInstance.Dispose();
                    _agentInstances.Remove(taskId);
                }
            }
        }, cts.Token);
    }

    public Task PauseTaskAsync(Guid taskId)
    {
        IAgent? agentInstance;
        AgenticTask? task;
        lock (_lock)
        {
            if (!_agentInstances.TryGetValue(taskId, out agentInstance))
                throw new KeyNotFoundException($"Task {taskId} not found.");
            if (!_tasks.TryGetValue(taskId, out task) || task == null)
                throw new KeyNotFoundException($"Task {taskId} not found.");

            task.Status = Domain.Models.TaskStatus.Paused;
        }

        return agentInstance.PauseAsync();
    }

    public Task ResumeTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        IAgent? agentInstance;
        AgenticTask? task;
        lock (_lock)
        {
            if (!_agentInstances.TryGetValue(taskId, out agentInstance))
                throw new KeyNotFoundException($"Task {taskId} not found.");
            if (!_tasks.TryGetValue(taskId, out task) || task == null)
                throw new KeyNotFoundException($"Task {taskId} not found.");

            task.Status = Domain.Models.TaskStatus.Running;
        }

        return agentInstance.ResumeAsync(ct);
    }

    public async Task AbortTaskAsync(Guid taskId)
    {
        CancellationTokenSource? cts;
        IAgent? agentInstance;
        AgenticTask? task;
        lock (_lock)
        {
            cts = _activeCts.GetValueOrDefault(taskId);
            _agentInstances.TryGetValue(taskId, out agentInstance);
            _tasks.TryGetValue(taskId, out task);
        }

        cts?.Cancel();

        if (agentInstance != null)
            await agentInstance.AbortAsync();

        lock (_lock)
        {
            if (task != null)
            {
                task.Status = Domain.Models.TaskStatus.Cancelled;
                task.CompletedAt = DateTime.UtcNow;
            }
        }

        NotifyStateChanged(taskId, Domain.Models.TaskStatus.Running, Domain.Models.TaskStatus.Cancelled, "Task aborted.");
    }

    public async Task<AgentTaskResult?> GetTaskResultAsync(Guid taskId)
    {
        AgenticTask? task;
        lock (_lock)
        {
            _tasks.TryGetValue(taskId, out task);
        }

        if (task == null || task.Status != Domain.Models.TaskStatus.Completed)
            return null;

        IAgent? agentInstance;
        lock (_lock)
        {
            _agentInstances.TryGetValue(taskId, out agentInstance);
        }

        if (agentInstance == null)
            return null;

        return new AgentTaskResult(
            taskId,
            Domain.Models.AgentState.Completed,
            task.ToolCalls.AsReadOnly(),
            task.Summary ?? string.Empty);
    }

    public event EventHandler<TaskStateChangedEventArgs>? TaskStateChanged;

    private void NotifyStateChanged(Guid taskId, Domain.Models.TaskStatus oldStatus, Domain.Models.TaskStatus newStatus, string? message = null)
    {
        TaskStateChanged?.Invoke(this, new TaskStateChangedEventArgs(taskId, oldStatus, newStatus, message));
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var cts in _activeCts.Values)
                cts?.Dispose();
            _activeCts.Clear();
            foreach (var agent in _agentInstances.Values)
                agent?.Dispose();
            _agentInstances.Clear();
        }
    }
}