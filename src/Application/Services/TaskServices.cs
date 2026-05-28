namespace OpenLMStudio.Application.Services;

using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

// ============================================================================
// AgentTaskAutoApprover (originally AgentTaskAutoApprover.cs)
// ============================================================================

/// <summary>
/// Handles auto-approval of agent tools and commands.
/// </summary>
public class AgentTaskAutoApprover : IAgentTaskAutoApprover
{
    private readonly AgentAutoApprovalSettings _settings;
    private readonly HashSet<string> _approvedTools;
    private readonly HashSet<string> _approvedPaths;
    private readonly HashSet<string> _approvedCommands;
    private readonly object _lock = new();

    public AgentTaskAutoApprover(AgentAutoApprovalSettings? settings = null)
    {
        _settings = settings ?? new AgentAutoApprovalSettings();
        _approvedTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _approvedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _approvedCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public bool ShouldAutoApproveTool(string toolName)
    {
        if (!_settings.Enabled)
            return false;

        lock (_lock)
        {
            return toolName switch
            {
                "write_to_file" => _settings.AutoApproveFileOperations,
                "replace_in_file" => _settings.AutoApproveFileOperations,
                "read_file" => _settings.AutoApproveFileOperations,
                "search_files" => _settings.AutoApproveFileOperations,
                "list_files" => _settings.AutoApproveFileOperations,
                "execute_command" => _settings.AutoApproveCommandExecution,
                "browser_action" => _settings.AutoApproveBrowserActions,
                "use_mcp_tool" => _settings.AutoApproveCommandExecution,
                "access_mcp_resource" => _settings.AutoApproveCommandExecution,
                "load_mcp_documentation" => _settings.AutoApproveCommandExecution,
                "plan_mode_respond" => _settings.AutoApproveCommandExecution,
                "act_mode_respond" => _settings.AutoApproveCommandExecution,
                "attempt_completion" => _settings.AutoApproveCommandExecution,
                "new_task" => _settings.AutoApproveCommandExecution,
                "use_skill" => _settings.AutoApproveCommandExecution,
                "use_subagents" => _settings.AutoApproveCommandExecution,
                "apply_patch" => _settings.AutoApproveFileOperations,
                "generate_explanation" => _settings.AutoApproveCommandExecution,
                "web_fetch" => _settings.AutoApproveCommandExecution,
                "web_search" => _settings.AutoApproveCommandExecution,
                "ask_followup_question" => _settings.AutoApproveCommandExecution,
                _ => _approvedTools.Contains(toolName),
            };
        }
    }

    public bool ShouldAutoApproveToolWithPath(string toolName, string path)
    {
        if (!ShouldAutoApproveTool(toolName))
            return false;

        lock (_lock)
        {
            return _approvedPaths.Contains(path);
        }
    }

    public bool ShouldAutoApproveCommand(string command)
    {
        if (!_settings.Enabled || !_settings.AutoApproveCommandExecution)
            return false;

        lock (_lock)
        {
            return _approvedCommands.Contains(command);
        }
    }

    public int GetCommandTimeout(string command)
    {
        var isLongRunning = IsLongRunningCommand(command);
        return isLongRunning ? _settings.DefaultTimeout * 3 : _settings.DefaultTimeout;
    }

    public bool IsLongRunningCommand(string command)
    {
        var lowerCommand = command.ToLowerInvariant();
        return lowerCommand.Contains("dotnet") ||
               lowerCommand.Contains("npm") ||
               lowerCommand.Contains("yarn") ||
               lowerCommand.Contains("build") ||
               lowerCommand.Contains("test") ||
               lowerCommand.Contains("run") ||
               lowerCommand.Contains("install") ||
               lowerCommand.Contains("publish");
    }

    public void RecordToolApproval(string toolName, string path)
    {
        lock (_lock)
        {
            _approvedTools.Add(toolName);
            if (!string.IsNullOrEmpty(path))
                _approvedPaths.Add(path);
        }
    }

    public void RecordCommandApproval(string command)
    {
        lock (_lock)
        {
            _approvedCommands.Add(command);
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _approvedTools.Clear();
            _approvedPaths.Clear();
            _approvedCommands.Clear();
        }
    }

    public void Dispose()
    {
        Reset();
    }
}

// ============================================================================
// AgentTaskCheckpointService (originally AgentTaskCheckpointService.cs)
// ============================================================================

/// <summary>
/// Manages task checkpoints with file-based storage.
/// </summary>
public class AgentTaskCheckpointService : IAgentTaskCheckpointService
{
    private readonly IFileSystem _fileSystem;
    private readonly string _checkpointDirectory;
    private readonly ConcurrentDictionary<Guid, List<TaskCheckpoint>> _inMemoryCheckpoints;
    private readonly ILogger<AgentTaskCheckpointService> _logger;
    private readonly object _lock = new();

    /// <summary>
    /// Cached JSON serialization options for consistent formatting.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public AgentTaskCheckpointService(
        IFileSystem? fileSystem = null,
        string? checkpointDirectory = null,
        ILogger<AgentTaskCheckpointService>? logger = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
        _checkpointDirectory = checkpointDirectory ?? Path.Combine(Path.GetTempPath(), "OpenLMStudio", "checkpoints");
        _inMemoryCheckpoints = new ConcurrentDictionary<Guid, List<TaskCheckpoint>>();
        _logger = logger ?? NullLogger<AgentTaskCheckpointService>.Instance;
    }

    public void Dispose()
    {
        _inMemoryCheckpoints.Clear();
    }

    public async Task SaveCheckpointAsync(Guid taskId, TaskCheckpoint checkpoint, CancellationToken ct = default)
    {
        try
        {
            var taskDir = GetTaskDirectory(taskId);
            _fileSystem.Directory.CreateDirectory(taskDir);

            var checkpoints = _inMemoryCheckpoints.GetOrAdd(taskId, _ => new List<TaskCheckpoint>());
            int version;
            lock (_lock)
            {
                version = checkpoints.Count + 1;
            }
            checkpoint.Version = version;
            checkpoints.Add(checkpoint);

            var checkpointPath = Path.Combine(taskDir, $"checkpoint_{checkpoint.Version}.json");
            var json = JsonSerializer.Serialize(checkpoint, _jsonOptions);
            await _fileSystem.File.WriteAllTextAsync(checkpointPath, json, ct);

            _logger?.LogDebug("Checkpoint saved for task {TaskId} version {Version}", taskId, checkpoint.Version);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving checkpoint for task {TaskId}", taskId);
            throw;
        }
    }

    public async Task<TaskCheckpoint?> GetLatestCheckpointAsync(Guid taskId, CancellationToken ct = default)
    {
        try
        {
            var checkpoints = await GetCheckpointsAsync(taskId, ct);
            return checkpoints.OrderByDescending(c => c.Version).FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting latest checkpoint for task {TaskId}", taskId);
            return null;
        }
    }

    public async Task<IReadOnlyList<TaskCheckpoint>> GetCheckpointsAsync(Guid taskId, CancellationToken ct = default)
    {
        try
        {
            var taskDir = GetTaskDirectory(taskId);
            if (!_fileSystem.Directory.Exists(taskDir))
                return new List<TaskCheckpoint>().AsReadOnly();

            var checkpoints = new List<TaskCheckpoint>();

            var files = _fileSystem.Directory.GetFiles(taskDir, "*.json");
            foreach (var file in files)
            {
                try
                {
                    var json = await _fileSystem.File.ReadAllTextAsync(file, ct);
                    var checkpoint = JsonSerializer.Deserialize<TaskCheckpoint>(json);
                    if (checkpoint != null)
                        checkpoints.Add(checkpoint);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error reading checkpoint file {File}", file);
                }
            }

            return checkpoints.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting checkpoints for task {TaskId}", taskId);
            return new List<TaskCheckpoint>().AsReadOnly();
        }
    }

    public async Task<TaskCheckpoint?> GetLatestToolCheckpointAsync(Guid taskId, CancellationToken ct = default)
    {
        var checkpoints = await GetCheckpointsAsync(taskId, ct);
        return checkpoints
            .Where(c => c.Type == CheckpointType.Tool)
            .OrderByDescending(c => c.Version)
            .FirstOrDefault();
    }

    public async Task<TaskCheckpoint?> GetLatestCompletionCheckpointAsync(Guid taskId, CancellationToken ct = default)
    {
        var checkpoints = await GetCheckpointsAsync(taskId, ct);
        return checkpoints
            .Where(c => c.Type == CheckpointType.Completion)
            .OrderByDescending(c => c.Version)
            .FirstOrDefault();
    }

    public async Task DeleteCheckpointsAsync(Guid taskId, CancellationToken ct = default)
    {
        try
        {
            var taskDir = GetTaskDirectory(taskId);
            if (_fileSystem.Directory.Exists(taskDir))
            {
                _fileSystem.Directory.Delete(taskDir, true);
            }

            _inMemoryCheckpoints.TryRemove(taskId, out _);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting checkpoints for task {TaskId}", taskId);
            throw;
        }
    }

    public async Task SaveToolCheckpointAsync(Guid taskId, string toolName, Dictionary<string, object> parameters, string result, CancellationToken ct = default)
    {
        var checkpoint = TaskCheckpoint.CreateToolCheckpoint(taskId, toolName, parameters, result);
        await SaveCheckpointAsync(taskId, checkpoint, ct);
    }

    public async Task SaveTaskCheckpointAsync(Guid taskId, CancellationToken ct = default)
    {
        var checkpoint = TaskCheckpoint.CreateTaskCheckpoint(taskId);
        await SaveCheckpointAsync(taskId, checkpoint, ct);
    }

    public async Task SaveCompletionCheckpointAsync(Guid taskId, CancellationToken ct = default)
    {
        var checkpoint = TaskCheckpoint.CreateCompletionCheckpoint(taskId);
        await SaveCheckpointAsync(taskId, checkpoint, ct);
    }

    private string GetTaskDirectory(Guid taskId)
    {
        return Path.Combine(_checkpointDirectory, taskId.ToString("N"));
    }
}

// ============================================================================
// AgentTaskContextManager (originally AgentTaskContextManager.cs)
// ============================================================================

/// <summary>
/// Manages agent task context (truncation, summarization, file read cache).
/// </summary>
public class AgentTaskContextManager : IAgentTaskContextManager
{
    private readonly IFileSystem _fileSystem;
    private readonly string _contextDirectory;
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, string>> _fileReadCaches;
    private readonly ConcurrentDictionary<Guid, ContextBudget> _contextBuckets;
    private readonly ILogger<AgentTaskContextManager> _logger;
    private readonly object _lock = new();

    /// <summary>
    /// Cached JSON serialization options for consistent formatting.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public AgentTaskContextManager(
        IFileSystem? fileSystem = null,
        string? contextDirectory = null,
        ILogger<AgentTaskContextManager>? logger = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
        _contextDirectory = contextDirectory ?? Path.Combine(Path.GetTempPath(), "OpenLMStudio", "context");
        _fileReadCaches = new ConcurrentDictionary<Guid, ConcurrentDictionary<string, string>>();
        _contextBuckets = new ConcurrentDictionary<Guid, ContextBudget>();
        _logger = logger ?? NullLogger<AgentTaskContextManager>.Instance;
    }

    public void Dispose()
    {
        foreach (var cache in _fileReadCaches)
        {
            cache.Value?.Clear();
        }
        _fileReadCaches.Clear();
        _contextBuckets.Clear();
    }

    public async Task TruncateHistoryAsync(Guid taskId, int maxMessages, CancellationToken ct = default)
    {
        try
        {
            var historyPath = GetHistoryFilePath(taskId);
            if (!_fileSystem.File.Exists(historyPath))
                return;

            var history = await _fileSystem.File.ReadAllTextAsync(historyPath, ct);
            var segments = JsonSerializer.Deserialize<List<ContextSegment>>(history);
            if (segments != null && segments.Count > maxMessages)
            {
                var truncated = segments.Skip(segments.Count - maxMessages).ToList();
                var json = JsonSerializer.Serialize(truncated, _jsonOptions);
                await _fileSystem.File.WriteAllTextAsync(historyPath, json, ct);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error truncating history for task {TaskId}", taskId);
        }
    }

    public async Task SummarizeHistoryAsync(Guid taskId, CancellationToken ct = default)
    {
        try
        {
            var summaryPath = GetSummaryFilePath(taskId);
            var historyPath = GetHistoryFilePath(taskId);

            if (!_fileSystem.File.Exists(historyPath))
                return;

            var history = await _fileSystem.File.ReadAllTextAsync(historyPath, ct);
            var segments = JsonSerializer.Deserialize<List<ContextSegment>>(history);
            if (segments != null)
            {
                var summary = $"Summarized {segments.Count} messages";
                await _fileSystem.File.WriteAllTextAsync(summaryPath, summary, ct);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error summarizing history for task {TaskId}", taskId);
        }
    }

    public async Task<string?> GetCachedFileAsync(Guid taskId, string filePath, CancellationToken ct = default)
    {
        var cache = _fileReadCaches.GetOrAdd(taskId, _ => new ConcurrentDictionary<string, string>());
        return cache.TryGetValue(filePath, out var content) ? content : null;
    }

    public async Task SetCachedFileAsync(Guid taskId, string filePath, string content, CancellationToken ct = default)
    {
        var cache = _fileReadCaches.GetOrAdd(taskId, _ => new ConcurrentDictionary<string, string>());
        cache[filePath] = content;
    }

    public async Task ClearFileCacheAsync(Guid taskId, CancellationToken ct = default)
    {
        if (_fileReadCaches.TryRemove(taskId, out var cache))
        {
            cache.Clear();
        }
    }

    public async Task UpdateFileCacheAsync(Guid taskId, Dictionary<string, string> cache, CancellationToken ct = default)
    {
        var existingCache = _fileReadCaches.GetOrAdd(taskId, _ => new ConcurrentDictionary<string, string>());
        foreach (var kvp in cache)
        {
            existingCache[kvp.Key] = kvp.Value;
        }
    }

    public async Task<ContextBudget> GetContextBudgetAsync(Guid taskId, CancellationToken ct = default)
    {
        return _contextBuckets.GetOrAdd(taskId, _ => ContextBudget.CreateDefault());
    }

    public async Task SetContextBudgetAsync(Guid taskId, ContextBudget budget, CancellationToken ct = default)
    {
        _contextBuckets[taskId] = budget;
    }

    private string GetHistoryFilePath(Guid taskId)
    {
        return Path.Combine(_contextDirectory, $"{taskId:N}_history.json");
    }

    private string GetSummaryFilePath(Guid taskId)
    {
        return Path.Combine(_contextDirectory, $"{taskId:N}_summary.txt");
    }
}

// ============================================================================
// AgentTaskHookService (originally AgentTaskHookService.cs)
// ============================================================================

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

// ============================================================================
// AgentTaskProgressService (originally AgentTaskProgressService.cs)
// ============================================================================

/// <summary>
/// Manages agent task progress tracking with file-based storage.
/// </summary>
public class AgentTaskProgressService : IAgentTaskProgressService
{
    private readonly IFileSystem _fileSystem;
    private readonly string _progressDirectory;
    private readonly ConcurrentDictionary<Guid, AgentTaskProgress> _inMemoryProgress;
    private readonly ILogger<AgentTaskProgressService> _logger;
    private readonly object _lock = new();

    /// <summary>
    /// Cached JSON serialization options for consistent formatting.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public AgentTaskProgressService(
        IFileSystem? fileSystem = null,
        string? progressDirectory = null,
        ILogger<AgentTaskProgressService>? logger = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
        _progressDirectory = progressDirectory ?? Path.Combine(Path.GetTempPath(), "OpenLMStudio", "progress");
        _inMemoryProgress = new ConcurrentDictionary<Guid, AgentTaskProgress>();
        _logger = logger ?? NullLogger<AgentTaskProgressService>.Instance;
    }

    public void Dispose()
    {
        _inMemoryProgress.Clear();
    }

    public async Task<AgentTaskProgress?> GetProgressAsync(Guid taskId, CancellationToken ct = default)
    {
        try
        {
            if (_inMemoryProgress.TryGetValue(taskId, out var progress))
                return progress;

            var progressPath = GetProgressFilePath(taskId);
            if (!_fileSystem.File.Exists(progressPath))
                return new AgentTaskProgress();

            var json = await _fileSystem.File.ReadAllTextAsync(progressPath, ct);
            var loadedProgress = JsonSerializer.Deserialize<AgentTaskProgress>(json);

            if (loadedProgress != null)
            {
                _inMemoryProgress[taskId] = loadedProgress;
            }

            return loadedProgress;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting progress for task {TaskId}", taskId);
            return new AgentTaskProgress();
        }
    }

    public async Task UpdateProgressAsync(Guid taskId, int percentage, CancellationToken ct = default)
    {
        var progress = await GetProgressAsync(taskId, ct);
        if (progress != null)
        {
            progress.Percentage = Math.Max(0, Math.Min(100, percentage));
            await SaveProgressAsync(taskId, progress, ct);
        }
    }

    public async Task AddChecklistItemAsync(Guid taskId, string description, bool isCompleted = false, CancellationToken ct = default)
    {
        var progress = await GetProgressAsync(taskId, ct);
        if (progress != null)
        {
            progress.AddChecklistItem(description, isCompleted);
            await SaveProgressAsync(taskId, progress, ct);
        }
    }

    public async Task CompleteChecklistItemAsync(Guid taskId, string description, CancellationToken ct = default)
    {
        var progress = await GetProgressAsync(taskId, ct);
        if (progress != null)
        {
            progress.CompleteChecklistItem(description);
            await SaveProgressAsync(taskId, progress, ct);
        }
    }

    public async Task UpdateCurrentStepAsync(Guid taskId, string step, CancellationToken ct = default)
    {
        var progress = await GetProgressAsync(taskId, ct);
        if (progress != null)
        {
            progress.UpdateCurrentStep(step);
            await SaveProgressAsync(taskId, progress, ct);
        }
    }

    public async Task AddReminderAsync(Guid taskId, string reminder, CancellationToken ct = default)
    {
        var progress = await GetProgressAsync(taskId, ct);
        if (progress != null)
        {
            progress.AddReminder(reminder);
            await SaveProgressAsync(taskId, progress, ct);
        }
    }

    public async Task<int> GetChecklistPercentageAsync(Guid taskId, CancellationToken ct = default)
    {
        var progress = await GetProgressAsync(taskId, ct);
        return progress?.CalculateChecklistPercentage() ?? 0;
    }

    public async Task<bool> ShouldSendReminderAsync(Guid taskId, CancellationToken ct = default)
    {
        var progress = await GetProgressAsync(taskId, ct);
        if (progress == null || progress.Reminders.Count == 0)
            return false;

        var elapsed = DateTime.UtcNow - progress.LastReminderTime;
        return elapsed.TotalMinutes >= progress.ReminderIntervalMinutes;
    }

    private async Task SaveProgressAsync(Guid taskId, AgentTaskProgress progress, CancellationToken ct)
    {
        try
        {
            var progressPath = GetProgressFilePath(taskId);
            var dir = _fileSystem.Path.GetDirectoryName(progressPath);
            if (!string.IsNullOrEmpty(dir))
                _fileSystem.Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(progress, _jsonOptions);
            await _fileSystem.File.WriteAllTextAsync(progressPath, json, ct);

            lock (_lock)
            {
                _inMemoryProgress[taskId] = progress;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving progress for task {TaskId}", taskId);
            throw;
        }
    }

    private string GetProgressFilePath(Guid taskId)
    {
        return Path.Combine(_progressDirectory, $"{taskId:N}.json");
    }
}

// ============================================================================
// AgentTaskStateService (originally AgentTaskStateService.cs)
// ============================================================================

/// <summary>
/// Manages agent task state persistence with file-based storage.
/// </summary>
public class AgentTaskStateService : IAgentTaskStateService
{
    private readonly IFileSystem _fileSystem;
    private readonly string _stateDirectory;
    private readonly ConcurrentDictionary<Guid, AgentTaskState> _inMemoryStates;
    private readonly ILogger<AgentTaskStateService> _logger;
    private readonly object _lock = new();

    /// <summary>
    /// Cached JSON serialization options for consistent formatting.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public AgentTaskStateService(
        IFileSystem? fileSystem = null,
        string? stateDirectory = null,
        ILogger<AgentTaskStateService>? logger = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
        _stateDirectory = stateDirectory ?? Path.Combine(Path.GetTempPath(), "OpenLMStudio", "states");
        _inMemoryStates = new ConcurrentDictionary<Guid, AgentTaskState>();
        _logger = logger ?? NullLogger<AgentTaskStateService>.Instance;
    }

    public void Dispose()
    {
        _inMemoryStates.Clear();
    }

    public async Task<AgentTaskState?> GetStateAsync(Guid taskId, CancellationToken ct = default)
    {
        try
        {
            if (_inMemoryStates.TryGetValue(taskId, out var state))
                return state;

            var statePath = GetStateFilePath(taskId);
            if (!_fileSystem.File.Exists(statePath))
                return null;

            var json = await _fileSystem.File.ReadAllTextAsync(statePath, ct);
            var loadedState = JsonSerializer.Deserialize<AgentTaskState>(json);

            if (loadedState != null)
            {
                _inMemoryStates[taskId] = loadedState;
            }

            return loadedState;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting state for task {TaskId}", taskId);
            return null;
        }
    }

    public async Task SaveStateAsync(Guid taskId, AgentTaskState state, CancellationToken ct = default)
    {
        try
        {
            state.UpdatedAt = DateTime.UtcNow;

            var statePath = GetStateFilePath(taskId);
            var dir = _fileSystem.Path.GetDirectoryName(statePath);
            if (!string.IsNullOrEmpty(dir))
                _fileSystem.Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(state, _jsonOptions);
            await _fileSystem.File.WriteAllTextAsync(statePath, json, ct);

            lock (_lock)
            {
                _inMemoryStates[taskId] = state;
            }

            _logger?.LogDebug("State saved for task {TaskId}", taskId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving state for task {TaskId}", taskId);
            throw;
        }
    }

    public async Task UpdateAgentStateAsync(Guid taskId, AgentStateExtended newState, CancellationToken ct = default)
    {
        var state = await GetStateAsync(taskId, ct);
        if (state != null)
        {
            state.AgentState = newState;
            state.UpdatedAt = DateTime.UtcNow;
            await SaveStateAsync(taskId, state, ct);
        }
    }

    public async Task ResetStateAsync(Guid taskId, CancellationToken ct = default)
    {
        var state = await GetStateAsync(taskId, ct);
        if (state != null)
        {
            state.Reset();
            await SaveStateAsync(taskId, state, ct);
        }
    }

    public async Task<IReadOnlyDictionary<Guid, AgentTaskState>> GetAllStatesAsync(CancellationToken ct = default)
    {
        try
        {
            var result = new Dictionary<Guid, AgentTaskState>();

            foreach (var kvp in _inMemoryStates)
            {
                result[kvp.Key] = kvp.Value;
            }

            return result.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting all states");
            return new Dictionary<Guid, AgentTaskState>().AsReadOnly();
        }
    }

    private string GetStateFilePath(Guid taskId)
    {
        return Path.Combine(_stateDirectory, $"{taskId:N}.json");
    }
}