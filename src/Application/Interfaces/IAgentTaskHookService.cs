namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Interface for managing agent task hooks.
/// </summary>
public interface IAgentTaskHookService : IDisposable
{
    /// <summary>
    /// Runs the TaskComplete hook.
    /// </summary>
    Task RunTaskCompleteHookAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Runs the UserPromptSubmit hook.
    /// </summary>
    Task<HookResult> RunUserPromptSubmitHookAsync(Guid taskId, string userContent, CancellationToken ct = default);

    /// <summary>
    /// Runs a tool call hook.
    /// </summary>
    Task RunToolCallHookAsync(Guid taskId, string toolName, Dictionary<string, object> parameters, CancellationToken ct = default);

    /// <summary>
    /// Runs a state change hook.
    /// </summary>
    Task RunStateChangeHookAsync(Guid taskId, string oldState, string newState, CancellationToken ct = default);

    /// <summary>
    /// Registers a hook callback.
    /// </summary>
    void RegisterHook(string hookName, Func<CancellationToken, Task> callback);

    /// <summary>
    /// Registers a parameterized hook callback.
    /// </summary>
    void RegisterHook<T>(string hookName, Func<T, CancellationToken, Task> callback);

    /// <summary>
    /// Unregisters a hook.
    /// </summary>
    void UnregisterHook(string hookName);

    /// <summary>
    /// Gets the active hook execution.
    /// </summary>
    string? GetActiveHookExecution(Guid taskId);

    /// <summary>
    /// Sets the active hook execution.
    /// </summary>
    void SetActiveHookExecution(Guid taskId, string executionId);

    /// <summary>
    /// Clears the active hook execution.
    /// </summary>
    void ClearActiveHookExecution(Guid taskId);
}

/// <summary>
/// Result of a hook execution.
/// </summary>
public record HookResult(
    bool Success,
    string? Output,
    string? Error);