namespace OpenLMStudio.Application.Services;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenLMStudio.Application.Interfaces;

/// <summary>
/// Manages agent task hooks for lifecycle events.
/// </summary>
public class AgentTaskHookService : IAgentTaskHookService
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, List<Delegate>>> _hooks;
    private readonly ConcurrentDictionary<Guid, string> _activeHookExecutions;
    private readonly ILogger<AgentTaskHookService> _logger;
    private readonly object _lock = new();

    public AgentTaskHookService(
        ILogger<AgentTaskHookService>? logger = null)
    {
        _hooks = new ConcurrentDictionary<Guid, ConcurrentDictionary<string, List<Delegate>>>();
        _activeHookExecutions = new ConcurrentDictionary<Guid, string>();
        _logger = logger ?? NullLogger<AgentTaskHookService>.Instance;
    }

    public void Dispose()
    {
        foreach (var hooks in _hooks)
        {
            hooks.Value.Clear();
        }
        _hooks.Clear();
        _activeHookExecutions.Clear();
    }

    public async Task RunTaskCompleteHookAsync(Guid taskId, CancellationToken ct = default)
    {
        try
        {
            var taskHooks = _hooks.GetOrAdd(taskId, _ => new ConcurrentDictionary<string, List<Delegate>>());
            if (taskHooks.TryGetValue("TaskComplete", out var callbacks))
            {
                foreach (var callback in callbacks)
                {
                    if (callback is Func<CancellationToken, Task> func)
                    {
                        await func(ct);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error running TaskComplete hook for task {TaskId}", taskId);
        }
    }

    public async Task<HookResult> RunUserPromptSubmitHookAsync(Guid taskId, string userContent, CancellationToken ct = default)
    {
        try
        {
            var taskHooks = _hooks.GetOrAdd(taskId, _ => new ConcurrentDictionary<string, List<Delegate>>());
            if (taskHooks.TryGetValue("UserPromptSubmit", out var callbacks))
            {
                foreach (var callback in callbacks)
                {
                    if (callback is Func<string, CancellationToken, Task> func)
                    {
                        await func(userContent, ct);
                    }
                }
            }
            return new HookResult(true, userContent, null);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error running UserPromptSubmit hook for task {TaskId}", taskId);
            return new HookResult(false, userContent, ex.Message);
        }
    }

    public async Task RunToolCallHookAsync(Guid taskId, string toolName, Dictionary<string, object> parameters, CancellationToken ct = default)
    {
        try
        {
            var taskHooks = _hooks.GetOrAdd(taskId, _ => new ConcurrentDictionary<string, List<Delegate>>());
            if (taskHooks.TryGetValue("ToolCall", out var callbacks))
            {
                foreach (var callback in callbacks)
                {
                    if (callback is Func<string, Dictionary<string, object>, CancellationToken, Task> func)
                    {
                        await func(toolName, parameters, ct);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error running ToolCall hook for task {TaskId}, tool {ToolName}", taskId, toolName);
        }
    }

    public async Task RunStateChangeHookAsync(Guid taskId, string oldState, string newState, CancellationToken ct = default)
    {
        try
        {
            var taskHooks = _hooks.GetOrAdd(taskId, _ => new ConcurrentDictionary<string, List<Delegate>>());
            if (taskHooks.TryGetValue("StateChange", out var callbacks))
            {
                foreach (var callback in callbacks)
                {
                    if (callback is Func<string, string, CancellationToken, Task> func)
                    {
                        await func(oldState, newState, ct);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error running StateChange hook for task {TaskId}", taskId);
        }
    }

    public void RegisterHook(string hookName, Func<CancellationToken, Task> callback)
    {
        var taskId = Guid.NewGuid();
        RegisterHookForTask(taskId, hookName, callback);
    }

    public void RegisterHook<T>(string hookName, Func<T, CancellationToken, Task> callback)
    {
        var taskId = Guid.NewGuid();
        RegisterHookForTask(taskId, hookName, callback);
    }

    public void UnregisterHook(string hookName)
    {
        var taskId = Guid.NewGuid();
        UnregisterHookForTask(taskId, hookName);
    }

    public string? GetActiveHookExecution(Guid taskId)
    {
        return _activeHookExecutions.TryGetValue(taskId, out var executionId) ? executionId : null;
    }

    public void SetActiveHookExecution(Guid taskId, string executionId)
    {
        _activeHookExecutions[taskId] = executionId;
    }

    public void ClearActiveHookExecution(Guid taskId)
    {
        _activeHookExecutions.TryRemove(taskId, out _);
    }

    private void RegisterHookForTask(Guid taskId, string hookName, Delegate callback)
    {
        lock (_lock)
        {
            var taskHooks = _hooks.GetOrAdd(taskId, _ => new ConcurrentDictionary<string, List<Delegate>>());
            var callbacks = taskHooks.GetOrAdd(hookName, _ => new List<Delegate>());
            callbacks.Add(callback);
        }
    }

    private void UnregisterHookForTask(Guid taskId, string hookName)
    {
        if (_hooks.TryRemove(taskId, out var taskHooks))
        {
            taskHooks.TryRemove(hookName, out _);
        }
    }
}