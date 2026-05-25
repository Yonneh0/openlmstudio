namespace OpenLMStudio.Application.Services;

using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

/// <summary>
/// Manages agent task hooks for lifecycle events.
/// </summary>
public class AgentTaskHookService : IAgentTaskHookService
{
    private readonly ConcurrentDictionary<string, List<Func<CancellationToken, Task>>> _hooks;
    private readonly ConcurrentDictionary<string, List<Func<object, CancellationToken, Task>>> _parameterizedHooks;
    private readonly ConcurrentDictionary<Guid, string> _activeHookExecutions;
    private readonly ILogger<AgentTaskHookService> _logger;
    private readonly object _lock = new();

    public AgentTaskHookService(ILogger<AgentTaskHookService>? logger = null)
    {
        _hooks = new ConcurrentDictionary<string, List<Func<CancellationToken, Task>>>();
        _parameterizedHooks = new ConcurrentDictionary<string, List<Func<object, CancellationToken, Task>>>();
        _activeHookExecutions = new ConcurrentDictionary<Guid, string>();
        _logger = logger ?? NullLogger<AgentTaskHookService>.Instance;
    }

    public void Dispose()
    {
        _hooks.Clear();
        _parameterizedHooks.Clear();
        _activeHookExecutions.Clear();
    }

    public async Task RunTaskCompleteHookAsync(Guid taskId, CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Running TaskComplete hook for task {TaskId}", taskId);

            if (_hooks.TryGetValue("TaskComplete", out var hooks))
            {
                foreach (var hook in hooks)
                {
                    await hook(ct);
                }
            }

            // Default TaskComplete behavior
            _logger?.LogInformation("TaskComplete hook completed for task {TaskId}", taskId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error running TaskComplete hook for task {TaskId}", taskId);
            throw;
        }
    }

    public async Task<HookResult> RunUserPromptSubmitHookAsync(Guid taskId, string userContent, CancellationToken ct = default)
    {
        try
        {
            _logger?.LogDebug("Running UserPromptSubmit hook for task {TaskId}", taskId);

            var result = new HookResult(true, userContent, null);

            if (_parameterizedHooks.TryGetValue("UserPromptSubmit", out var hooks))
            {
                foreach (var hook in hooks)
                {
                    await hook(userContent, ct);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error running UserPromptSubmit hook for task {TaskId}", taskId);
            return new HookResult(false, null, ex.Message);
        }
    }

    public async Task RunToolCallHookAsync(Guid taskId, string toolName, Dictionary<string, object> parameters, CancellationToken ct = default)
    {
        try
        {
            _logger?.LogDebug("Running ToolCall hook for task {TaskId} tool {ToolName}", taskId, toolName);

            if (_hooks.TryGetValue("ToolCall", out var hooks))
            {
                foreach (var hook in hooks)
                {
                    await hook(ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error running ToolCall hook for task {TaskId}", taskId);
            throw;
        }
    }

    public async Task RunStateChangeHookAsync(Guid taskId, string oldState, string newState, CancellationToken ct = default)
    {
        try
        {
            _logger?.LogDebug("Running StateChange hook for task {TaskId}: {OldState} -> {NewState}", taskId, oldState, newState);

            if (_hooks.TryGetValue("StateChange", out var hooks))
            {
                foreach (var hook in hooks)
                {
                    await hook(ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error running StateChange hook for task {TaskId}", taskId);
            throw;
        }
    }

    public void RegisterHook(string hookName, Func<CancellationToken, Task> callback)
    {
        var hooks = _hooks.GetOrAdd(hookName, _ => new List<Func<CancellationToken, Task>>());
        lock (_lock)
        {
            hooks.Add(callback);
        }
    }

    public void RegisterHook<T>(string hookName, Func<T, CancellationToken, Task> callback)
    {
        var parameterizedHooks = _parameterizedHooks.GetOrAdd(hookName, _ => new List<Func<object, CancellationToken, Task>>());
        lock (_lock)
        {
            parameterizedHooks.Add((obj, ct) => callback((T)obj, ct));
        }
    }

    public void UnregisterHook(string hookName)
    {
        _hooks.TryRemove(hookName, out _);
        _parameterizedHooks.TryRemove(hookName, out _);
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
}