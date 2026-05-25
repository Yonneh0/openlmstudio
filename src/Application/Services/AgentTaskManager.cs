namespace OpenLMStudio.Application.Services;

using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Interfaces;
using OpenLMStudio.Domain.Models;

/// <summary>
/// Central orchestrator for agent task management.
/// Manages the task lifecycle: init, execute, complete, terminate.
/// </summary>
public class AgentTaskManager : IAgentTaskManager
{
    private readonly IAgentTaskCheckpointService _checkpointService;
    private readonly IAgentTaskStateService _stateService;
    private readonly IAgentTaskProgressService _progressService;
    private readonly IAgentTaskAutoApprover _autoApprover;
    private readonly IAgentTaskContextManager _contextManager;
    private readonly IAgentTaskHookService _hookService;
    private readonly IAgentToolExecutor _toolExecutor;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<AgentTaskManager> _logger;
    private readonly object _lock = new();

    private AgentTaskState? _currentState;
    private AgentTaskSettings? _currentSettings;
    private Guid? _currentTaskId;
    private bool _isDisposed;

    public AgentTaskManager(
        IAgentTaskCheckpointService checkpointService,
        IAgentTaskStateService stateService,
        IAgentTaskProgressService progressService,
        IAgentTaskAutoApprover autoApprover,
        IAgentTaskContextManager contextManager,
        IAgentTaskHookService hookService,
        IAgentToolExecutor toolExecutor,
        IFileSystem? fileSystem = null,
        ILogger<AgentTaskManager>? logger = null)
    {
        _checkpointService = checkpointService;
        _stateService = stateService;
        _progressService = progressService;
        _autoApprover = autoApprover;
        _contextManager = contextManager;
        _hookService = hookService;
        _toolExecutor = toolExecutor;
        _fileSystem = fileSystem ?? new FileSystem();
        _logger = logger ?? NullLogger<AgentTaskManager>.Instance;
    }

    public AgentTaskState? CurrentState => _currentState;
    public AgentTaskSettings? CurrentSettings => _currentSettings;
    public Guid? CurrentTaskId => _currentTaskId;

    public async Task<Guid> InitTaskAsync(string taskString, List<string>? images = null, List<string>? files = null,
        HistoryItem? historyItem = null, AgentTaskSettings? taskSettings = null)
    {
        lock (_lock)
        {
            _currentTaskId = Guid.NewGuid();
            _currentSettings = taskSettings ?? AgentTaskSettings.CreateDefault();
            _currentSettings.TaskId = _currentTaskId.Value;
            _currentState = new AgentTaskState
            {
                TaskId = _currentTaskId.Value,
                Ulid = _currentSettings.Ulid,
                Mode = _currentSettings.Mode,
                AgentState = AgentStateExtended.Planning,
                TaskProgress = new AgentTaskProgress(),
            };
        }

        try
        {
            // Save initial state
            await _stateService.SaveStateAsync(_currentTaskId.Value, _currentState);

            // Save initial checkpoint
            await _checkpointService.SaveTaskCheckpointAsync(_currentTaskId.Value);

            // Add initial checklist item
            await _progressService.AddChecklistItemAsync(_currentTaskId.Value, taskString);

            // Update progress
            await _progressService.UpdateProgressAsync(_currentTaskId.Value, 0);
            await _progressService.UpdateCurrentStepAsync(_currentTaskId.Value, "Planning");

            // Run init hook
            await _hookService.RunStateChangeHookAsync(_currentTaskId.Value, "Idle", "Planning");

            _logger?.LogInformation("Initialized task {TaskId} with description: {TaskString}", _currentTaskId.Value, taskString);

            return _currentTaskId.Value;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error initializing task with description: {TaskString}", taskString);
            throw;
        }
    }

    public async Task CancelTaskAsync()
    {
        if (_currentTaskId == null || _currentState == null)
            return;

        lock (_lock)
        {
            _currentState.AgentState = AgentStateExtended.Aborting;
            _currentState.Abort = true;
        }

        try
        {
            // Save state
            await _stateService.SaveStateAsync(_currentTaskId.Value, _currentState);

            // Run abort hook
            await _hookService.RunStateChangeHookAsync(_currentTaskId.Value, "Acting", "Aborting");

            _logger?.LogInformation("Cancelled task {TaskId}", _currentTaskId.Value);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error cancelling task {TaskId}", _currentTaskId.Value);
            throw;
        }
    }

    public async Task ResumeTaskFromHistoryAsync(HistoryItem historyItem)
    {
        if (_currentTaskId == null)
            return;

        try
        {
            // Load state from history
            var state = await _stateService.GetStateAsync(_currentTaskId.Value);
            if (state != null)
            {
                state.AgentState = AgentStateExtended.Planning;
                state.UserMessageContent = historyItem.Summary ?? historyItem.Messages[0].Content;
                await _stateService.SaveStateAsync(_currentTaskId.Value, state);
            }

            // Update progress
            await _progressService.UpdateCurrentStepAsync(_currentTaskId.Value, "Resuming from history");

            _logger?.LogInformation("Resumed task {TaskId} from history item {HistoryItemId}", _currentTaskId.Value, historyItem.Id);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error resuming task {TaskId} from history", _currentTaskId.Value);
            throw;
        }
    }

    public async Task ExecuteToolLoopAsync()
    {
        if (_currentTaskId == null || _currentState == null)
            return;

        lock (_lock)
        {
            _currentState.AgentState = AgentStateExtended.Acting;
            _currentState.IsAwaitingPlanResponse = false;
        }

        try
        {
            // Update state
            await _stateService.UpdateAgentStateAsync(_currentTaskId.Value, AgentStateExtended.Acting);

            // Update progress
            await _progressService.UpdateCurrentStepAsync(_currentTaskId.Value, "Executing");

            // Run tool call hook
            await _hookService.RunToolCallHookAsync(_currentTaskId.Value, "execute_tool", new Dictionary<string, object>());

            _logger?.LogInformation("Executing tool loop for task {TaskId}", _currentTaskId.Value);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing tool loop for task {TaskId}", _currentTaskId.Value);
            throw;
        }
    }

    public async Task CompleteTaskAsync(string result, string? command = null)
    {
        if (_currentTaskId == null || _currentState == null)
            return;

        lock (_lock)
        {
            _currentState.AgentState = AgentStateExtended.Completed;
            _currentState.DoubleCheckCompletionPending = true;
        }

        try
        {
            // Update progress
            await _progressService.UpdateProgressAsync(_currentTaskId.Value, 100);
            await _progressService.UpdateCurrentStepAsync(_currentTaskId.Value, "Complete");

            // Save completion checkpoint
            await _checkpointService.SaveCompletionCheckpointAsync(_currentTaskId.Value);

            // Run TaskComplete hook
            await _hookService.RunTaskCompleteHookAsync(_currentTaskId.Value);

            // Run state change hook
            await _hookService.RunStateChangeHookAsync(_currentTaskId.Value, "Acting", "Completed");

            _logger?.LogInformation("Completed task {TaskId} with result: {Result}", _currentTaskId.Value, result);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error completing task {TaskId}", _currentTaskId.Value);
            throw;
        }
    }

    public async Task TerminateTaskAsync()
    {
        if (_currentTaskId == null)
            return;

        try
        {
            // Save final state
            if (_currentState != null)
            {
                await _stateService.SaveStateAsync(_currentTaskId.Value, _currentState);
            }

            // Release lock (clear state)
            await _stateService.ResetStateAsync(_currentTaskId.Value);

            _logger?.LogInformation("Terminated task {TaskId}", _currentTaskId.Value);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error terminating task {TaskId}", _currentTaskId.Value);
            throw;
        }
    }

    public async Task UpdateTaskHistoryAsync(HistoryItem historyItem)
    {
        if (_currentTaskId == null)
            return;

        try
        {
            await _hookService.RunStateChangeHookAsync(_currentTaskId.Value, "Planning", "Acting");
            _logger?.LogInformation("Updated task history for task {TaskId}", _currentTaskId.Value);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating task history for task {TaskId}", _currentTaskId.Value);
            throw;
        }
    }

    public async Task SwitchToActModeAsync()
    {
        if (_currentTaskId == null || _currentState == null)
            return;

        lock (_lock)
        {
            _currentState.Mode = "Act";
            _currentState.AgentState = AgentStateExtended.Acting;
            _currentState.IsAwaitingPlanResponse = false;
        }

        try
        {
            await _stateService.SaveStateAsync(_currentTaskId.Value, _currentState);
            await _stateService.UpdateAgentStateAsync(_currentTaskId.Value, AgentStateExtended.Acting);

            _logger?.LogInformation("Switched task {TaskId} to Act mode", _currentTaskId.Value);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error switching task {TaskId} to Act mode", _currentTaskId.Value);
            throw;
        }
    }

    public async Task SwitchToPlanModeAsync()
    {
        if (_currentTaskId == null || _currentState == null)
            return;

        lock (_lock)
        {
            _currentState.Mode = "Plan";
            _currentState.AgentState = AgentStateExtended.Planning;
            _currentState.IsAwaitingPlanResponse = true;
        }

        try
        {
            await _stateService.SaveStateAsync(_currentTaskId.Value, _currentState);
            await _stateService.UpdateAgentStateAsync(_currentTaskId.Value, AgentStateExtended.Planning);

            _logger?.LogInformation("Switched task {TaskId} to Plan mode", _currentTaskId.Value);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error switching task {TaskId} to Plan mode", _currentTaskId.Value);
            throw;
        }
    }

    public async Task<(bool rejected, string result)> ExecuteCommandToolAsync(string command, int timeoutSeconds = 30, Dictionary<string, object>? options = null)
    {
        if (_currentTaskId == null)
            return (true, string.Empty);

        try
        {
            // Check auto-approval
            if (_autoApprover.ShouldAutoApproveCommand(command))
            {
                _autoApprover.RecordCommandApproval(command);
            }

            // Execute via tool executor
            var result = await _toolExecutor.ExecuteCommandAsync(command, timeoutSeconds, options);

            // Save checkpoint
            var parameters = options ?? new Dictionary<string, object>();
            parameters["command"] = command;
            await _checkpointService.SaveToolCheckpointAsync(_currentTaskId.Value, "execute_command", parameters, result);

            return (false, result);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing command tool for task {TaskId}", _currentTaskId.Value);
            return (true, ex.Message);
        }
    }

    public async Task<bool> CancelRunningCommandToolAsync()
    {
        if (_currentTaskId == null)
            return false;

        try
        {
            var cancelled = await _toolExecutor.CancelRunningCommandAsync();
            _logger?.LogInformation("Cancelled running command tool for task {TaskId}", _currentTaskId.Value);
            return cancelled;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error cancelling running command tool for task {TaskId}", _currentTaskId.Value);
            return false;
        }
    }

    public async Task<bool> DoesLatestTaskCompletionHaveNewChangesAsync()
    {
        if (_currentTaskId == null)
            return false;

        try
        {
            var latestCheckpoint = await _checkpointService.GetLatestCompletionCheckpointAsync(_currentTaskId.Value);
            return latestCheckpoint?.IsCompletionCheckpoint == true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking latest task completion changes for task {TaskId}", _currentTaskId.Value);
            return false;
        }
    }

    public async Task UpdateFCListFromToolResponseAsync(AgentTaskProgress progress)
    {
        if (_currentTaskId == null)
            return;

        try
        {
            if (progress != null)
            {
                await _progressService.UpdateProgressAsync(_currentTaskId.Value, progress.CalculateChecklistPercentage());
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating FC list from tool response for task {TaskId}", _currentTaskId.Value);
        }
    }

    public async Task<string> SayAndCreateMissingParamErrorAsync(string toolName, string parameterName, string? relativePath)
    {
        if (_currentTaskId == null)
            return string.Empty;

        try
        {
            var message = $"Missing parameter '{parameterName}' for tool '{toolName}'";
            if (!string.IsNullOrEmpty(relativePath))
            {
                message += $" (path: {relativePath})";
            }

            _logger?.LogWarning(message);
            return message;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error creating missing parameter error for task {TaskId}", _currentTaskId.Value);
            return ex.Message;
        }
    }

    public async Task RemoveLastPartialMessageIfExistsWithTypeAsync(string messageType, string askOrSay)
    {
        if (_currentTaskId == null)
            return;

        try
        {
            _logger?.LogDebug("Removing last partial message of type {MessageType} for task {TaskId}", messageType, _currentTaskId.Value);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error removing last partial message for task {TaskId}", _currentTaskId.Value);
        }
    }

    public async Task ApplyLatestBrowserSettingsAsync()
    {
        if (_currentSettings?.BrowserSettings != null)
        {
            _logger?.LogDebug("Applied browser settings: {Width}x{Height}", _currentSettings.BrowserSettings.Width, _currentSettings.BrowserSettings.Height);
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            _currentState?.Reset();
            _checkpointService.Dispose();
            _stateService.Dispose();
            _progressService.Dispose();
            _contextManager.Dispose();
            _hookService.Dispose();
        }
    }
}
