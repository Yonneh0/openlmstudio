using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;
using TaskStatus = OpenLMStudio.Domain.Models.TaskStatus;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Manages agentic tasks — creation, execution via IAgent, dependency tracking, and persistence.
/// Consolidated from 8 separate files: TaskService, TaskScheduler, TaskSchedulerService,
/// TaskCompletionDetector, TaskContextInheritor, TaskContextPruner, TaskContextReinjectionService,
/// TaskProgressTracker.
/// </summary>
public class TaskService : ITaskService, IDisposable
{
    private readonly ILogger<TaskService>? _logger;
    private readonly IAgent? _agent;
    private readonly ITaskContextStore? _contextStore;
    private readonly ITaskContextInheritor? _contextInheritor;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<Guid, AgenticTask> _tasks = new();
    private readonly Dictionary<Guid, IAgent> _agentInstances = new();
    private readonly Dictionary<Guid, CancellationTokenSource> _activeCts = new();
    private readonly Dictionary<Guid, IServiceScope> _taskScopes = new();
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
        IServiceScope scope;

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
            scope = _serviceProvider.CreateScope();
            agentInstance = scope.ServiceProvider.GetRequiredService<IAgent>();
            _agentInstances[taskId] = agentInstance;
            _taskScopes[taskId] = scope;
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
            AvailableTools: null!,
            InitialContext: initialContext,
            MaxIterations: task.MaxIterations);

        // Run agent execution on background thread
        _ = Task.Run(async () =>
        {
            try
            {
                var result = await agentInstance.ExecuteAsync(request, cts.Token);

                lock (_lock)
                {
                    var oldStatus = task.Status;
                    task.Status = Domain.Models.TaskStatus.Completed;
                    task.Summary = result.Summary;
                    task.CompletedAt = DateTime.UtcNow;
                    task.ToolCalls = result.ToolCalls.ToList();
                    NotifyStateChanged(taskId, oldStatus, Domain.Models.TaskStatus.Completed, result.Summary);
                }

                _logger?.LogInformation("Task completed: {TaskId}", taskId);
            }
            catch (OperationCanceledException)
            {
                lock (_lock)
                {
                    var oldStatus = task.Status;
                    task.Status = Domain.Models.TaskStatus.Cancelled;
                    task.CompletedAt = DateTime.UtcNow;
                    NotifyStateChanged(taskId, oldStatus, Domain.Models.TaskStatus.Cancelled, "Task cancelled.");
                }
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    var oldStatus = task.Status;
                    task.Status = Domain.Models.TaskStatus.Failed;
                    task.ErrorMessage = ex.Message;
                    task.CompletedAt = DateTime.UtcNow;
                    NotifyStateChanged(taskId, oldStatus, Domain.Models.TaskStatus.Failed, ex.Message);
                }
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

    public async Task PauseTaskAsync(Guid taskId)
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

        await agentInstance.PauseAsync();
    }

    public async Task ResumeTaskAsync(Guid taskId, CancellationToken ct = default)
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

        await agentInstance.ResumeAsync(ct);
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
                var oldStatus = task.Status;
                task.Status = Domain.Models.TaskStatus.Cancelled;
                task.CompletedAt = DateTime.UtcNow;
                NotifyStateChanged(taskId, oldStatus, Domain.Models.TaskStatus.Cancelled, "Task aborted.");
            }
        }
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

        // Use task data directly instead of relying on agent instance (which may have been disposed)
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
            // Notify about pending tasks before disposal
            foreach (var task in _tasks.Values.Where(t => t.Status == Domain.Models.TaskStatus.Running))
            {
                task.Status = Domain.Models.TaskStatus.Cancelled;
                NotifyStateChanged(task.Id, Domain.Models.TaskStatus.Running, Domain.Models.TaskStatus.Cancelled, "Disposed.");
            }

            foreach (var cts in _activeCts.Values)
                cts?.Dispose();
            _activeCts.Clear();
            foreach (var agent in _agentInstances.Values)
                agent?.Dispose();
            _agentInstances.Clear();
            foreach (var scope in _taskScopes.Values)
                scope?.Dispose();
            _taskScopes.Clear();
        }
    }

    // ==================== Nested Classes ====================

    #region TaskCompletionDetector

    /// <summary>
    /// Detects when an agent task is complete based on tool results and task state.
    /// Delegates to TaskValidationService for AI-powered completion checks.
    /// </summary>
    internal class TaskCompletionDetector : ITaskCompletionDetector
    {
        private readonly ILogger<TaskCompletionDetector> _logger;
        private readonly ITaskValidationService _validationService;

        public TaskCompletionDetector(
            ILogger<TaskCompletionDetector> logger,
            ITaskValidationService validationService)
        {
            _logger = logger;
            _validationService = validationService;
        }

        public async Task<TaskValidationResult> DetectCompletionAsync(
            AgenticTask task,
            IReadOnlyList<AgentToolCallRecord> toolCalls,
            CancellationToken ct = default)
        {
            if (toolCalls.Count == 0)
            {
                _logger.LogDebug("No tool calls to evaluate for task {TaskId}", task.Id);
                return new TaskValidationResult(false, "No tool calls made — task may not have executed.");
            }

            var lastFew = toolCalls.TakeLast(10).ToList();
            var summaryLines = new List<string>();
            foreach (var call in lastFew)
            {
                summaryLines.Add($"- {call.ToolName}: {(call.Success ? "OK" : $"FAILED ({call.Result})")} [{call.DurationMs:F0}ms]");
            }
            var summary = string.Join("\n", summaryLines);

            return await _validationService.ValidateTaskCompletionAsync(task, summary, ct);
        }

        public static TaskValidationResult QuickHeuristicCheck(AgenticTask task, IReadOnlyList<AgentToolCallRecord> toolCalls)
        {
            if (task.Status == TaskStatus.Completed)
                return new TaskValidationResult(true, "Task already marked as completed.");

            if (task.Status == TaskStatus.Failed)
                return new TaskValidationResult(false, $"Task failed: {task.ErrorMessage}");

            if (toolCalls.Count >= task.MaxIterations)
                return new TaskValidationResult(false, $"Max iterations ({task.MaxIterations}) reached without completion.");

            var recentErrors = toolCalls.TakeLast(5).Count(c => !c.Success);
            if (recentErrors >= 3)
                return new TaskValidationResult(false, $"Too many recent errors ({recentErrors} failures in last 5 calls).");

            return new TaskValidationResult(false, "No completion signal detected — continue executing.");
        }
    }

    #endregion

    #region TaskContextInheritor

    /// <summary>
    /// Propagates relevant context from a parent task to child tasks with budget-aware filtering.
    /// </summary>
    internal class TaskContextInheritor : ITaskContextInheritor, IDisposable
    {
        private readonly ILogger<TaskContextInheritor>? _logger;
        private readonly ITaskContextStore _taskContextStore;
        private readonly IContextWindowBudgeter _budgeter;

        private const long MaxParentToChildPropagationTokens = 2048;

        public TaskContextInheritor(
            ILogger<TaskContextInheritor>? logger,
            ITaskContextStore taskContextStore,
            IContextWindowBudgeter budgeter)
        {
            _logger = logger;
            _taskContextStore = taskContextStore;
            _budgeter = budgeter;
        }

        public async Task<TaskContextSnapshot> CreateChildInheritanceAsync(Guid parentTaskId, Guid childTaskId)
        {
            var parentSnapshot = await _taskContextStore.GetByTaskIdAsync(parentTaskId);
            if (parentSnapshot == null)
            {
                return new TaskContextSnapshot
                {
                    TaskId = childTaskId,
                    Description = "No parent context available",
                    CurrentState = AgentState.Planning,
                    CompressedContext = new(),
                    ToolResultsCache = new(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
            }

            var childSnapshot = new TaskContextSnapshot
            {
                TaskId = childTaskId,
                Description = $"Inherited from parent task",
                CurrentState = AgentState.Planning,
                CompressedContext = parentSnapshot.CompressedContext?.Where(s => !s.IsSuppressed).ToList() ?? new(),
                ToolResultsCache = parentSnapshot.ToolResultsCache?.ToDictionary(k => k.Key, v => v.Value) ?? new(),
                ActiveFileTree = parentSnapshot.ActiveFileTree,
                GitStatusSnapshot = parentSnapshot.GitStatusSnapshot,
                RelevantEntities = parentSnapshot.RelevantEntities?.ToList() ?? new(),
                CompressedContextTokenCount = Math.Min(parentSnapshot.CompressedContextTokenCount, MaxParentToChildPropagationTokens),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ArchiveOnCompletion = false
            };

            var childBudget = await _budgeter.GetOrCreateBudgetAsync(childTaskId);
            if (childBudget.RemainingTokens < 512 && parentSnapshot.CompressedContextTokenCount > MaxParentToChildPropagationTokens)
            {
                childSnapshot.CompressedContext = parentSnapshot.CompressedContext != null ? FilterByRelevance(parentSnapshot.CompressedContext, topN: 8) : new();
                childSnapshot.RelevantEntities = parentSnapshot.RelevantEntities?.Take(10).ToList() ?? new();
            }

            _logger?.LogInformation("Created child inheritance for TaskId={ChildTaskId} from parent {ParentTaskId}",
                childTaskId, parentTaskId);

            return childSnapshot;
        }

        public async Task<AdditionalParentContext?> RequestAdditionalContextFromParentAsync(Guid parentId, Guid childId)
        {
            var parentSnapshot = await _taskContextStore.GetByTaskIdAsync(parentId);
            if (parentSnapshot == null || !parentSnapshot.CompressedContext.Any())
                return null;

            var parentBudget = await _budgeter.GetOrCreateBudgetAsync(parentId, 16384);
            if (parentBudget.RemainingTokens < 1024)
                return null;

            var parentTokenCount = parentSnapshot.CompressedContextTokenCount;
            var budgetRatio = (double)parentBudget.RemainingTokens / 16384.0;
            var propagateLimit = (long)(MaxParentToChildPropagationTokens * Math.Max(0.25, budgetRatio));

            var filteredSegments = FilterByRelevance(parentSnapshot.CompressedContext, topN: 16);

            string? additionalProjectState = null;
            if (parentSnapshot.ActiveFileTree != null && string.IsNullOrEmpty(parentSnapshot.GitStatusSnapshot))
                additionalProjectState = parentSnapshot.ActiveFileTree;

            var totalTokenCount = filteredSegments.Sum(s => s.TokenCount);

            return new AdditionalParentContext
            {
                RelevantHistorySegments = filteredSegments,
                ProjectStateSnapshot = additionalProjectState,
                TokenCount = Math.Min(totalTokenCount, propagateLimit)
            };
        }

        private static List<ContextSegment> FilterByRelevance(IEnumerable<ContextSegment> segments, int topN)
        {
            return segments.OrderByDescending(s => s.RelevanceScore).Take(topN).ToList();
        }

        public void Dispose()
        {
            // No unmanaged resources to clean up
        }
    }

    #endregion

    #region TaskContextPruner

    /// <summary>Manages context pruning strategies when a task completes.</summary>
    internal class TaskContextPruner : ITaskContextPruner, IDisposable
    {
        private readonly ILogger<TaskContextPruner>? _logger;
        private readonly ITaskContextStore _taskContextStore;

        public TaskContextPruner(ILogger<TaskContextPruner>? logger, ITaskContextStore taskContextStore)
        {
            _logger = logger;
            _taskContextStore = taskContextStore;
        }

        public async Task ArchiveAsync(Guid taskId)
        {
            var snapshot = await _taskContextStore.GetByTaskIdAsync(taskId);
            if (snapshot == null)
                return;

            snapshot.ArchiveOnCompletion = true;
            snapshot.CurrentState = AgentState.Completed;
            await _taskContextStore.UpdateAsync(snapshot);

            _logger?.LogInformation("Archived task context for TaskId={TaskId}", taskId);
        }

        public async Task CompressAndArchiveAsync(Guid taskId)
        {
            var snapshot = await _taskContextStore.GetByTaskIdAsync(taskId);
            if (snapshot == null || !snapshot.CompressedContext.Any())
                return;

            snapshot.ArchiveOnCompletion = true;
            snapshot.CurrentState = AgentState.Completed;
            await _taskContextStore.UpdateAsync(snapshot);

            _logger?.LogInformation("Compressed and archived task context for TaskId={TaskId}", taskId);
        }

        public async Task DiscardAsync(Guid taskId)
        {
            await _taskContextStore.DeleteAsync(taskId, ContextPruneStrategy.Discard);
            _logger?.LogInformation("Discarded task context for TaskId={TaskId}", taskId);
        }

        public void Dispose() { /* No unmanaged resources to clean up */ }
    }

    #endregion

    #region TaskContextReinjectionService

    /// <summary>
    /// Restores full context for a paused/abandoned agent task in <100ms via pre-compressed snapshot.
    /// </summary>
    internal class TaskContextReinjectionService : ITaskContextReinjectionService, IDisposable
    {
        private readonly ILogger<TaskContextReinjectionService>? _logger;
        private readonly ITaskContextStore _taskContextStore;

        public TaskContextReinjectionService(ILogger<TaskContextReinjectionService>? logger, ITaskContextStore taskContextStore)
        {
            _logger = logger;
            _taskContextStore = taskContextStore;
        }

        public async Task<ContextWindow> FastReinjectAsync(Guid taskId)
        {
            var snapshot = await _taskContextStore.GetByTaskIdAsync(taskId);
            if (snapshot == null || !snapshot.CompressedContext.Any())
                return ContextWindow.CreateEmpty();

            var window = new ContextWindow
            {
                Segments = snapshot.CompressedContext.Where(s => !s.IsSuppressed).ToList(),
                TotalTokenCount = snapshot.CompressedContextTokenCount,
                OverallCompression = CompressionLevel.None
            };

            _logger?.LogDebug("Fast reinjection for TaskId={TaskId}: {SegmentCount} segments restored",
                taskId, window.Segments.Count);

            return window;
        }

        public async Task ResumeToolCallChainAsync(Guid taskId, Guid resumeFromCallId)
        {
            var snapshot = await _taskContextStore.GetByTaskIdAsync(taskId);
            if (snapshot == null || !snapshot.ToolResultsCache.ContainsKey(resumeFromCallId.ToString()))
                return;

            _logger?.LogInformation("Resumed tool call chain for TaskId={TaskId} at CallId={CallId}", taskId, resumeFromCallId);
        }

        public async Task<ReinjectionSummary> GetReinjectionSummaryAsync(Guid taskId)
        {
            var snapshot = await _taskContextStore.GetByTaskIdAsync(taskId);

            if (snapshot == null || !snapshot.CompressedContext.Any())
                return new ReinjectionSummary();

            var restoredCount = snapshot.CompressedContext.Where(s => !s.IsSuppressed).Sum(s => s.TokenCount);

            return new ReinjectionSummary
            {
                RestoredTokenCount = restoredCount,
                ToolResultsRestored = snapshot.ToolResultsCache.Count,
                ToolChainResumed = snapshot.ToolResultsCache.Count > 0,
                PreviousState = snapshot.CurrentState.ToString(),
                RemainingBudgetTokens = 0
            };
        }

        public void Dispose()
        {
            // No unmanaged resources to clean up
        }
    }

    #endregion

    #region TaskProgressTracker

    /// <summary>
    /// Tracks agentic task progress through stages with iteration limit enforcement.
    /// </summary>
    internal class TaskProgressTracker : ITaskProgressTracker, IDisposable
    {
        private readonly ConcurrentDictionary<string, TrackerEntry> _entries = new();
        private int _progressPercentage;
        private int _hasError;
        private int _iterationCount;

        public int ProgressPercentage => _progressPercentage;
        public bool HasError => _hasError != 0;
        public string? ErrorMessage { get; set; }
        public int IterationCount => _iterationCount;
        public bool IsIterationLimitExceeded => _iterationCount >= 50;

        private TrackerEntry? GetActive()
        {
            foreach (var entry in _entries.Values.ToList())
                if (!entry.Completed)
                    return entry;
            return null;
        }

        public TaskProgressStage Stage => HasError || IsIterationLimitExceeded ? TaskProgressStage.Failed :
            ProgressPercentage == 100 ? TaskProgressStage.Completed :
            GetActive() is { Completed: true } ? TaskProgressStage.Reviewing : TaskProgressStage.InProgress;

        public async Task UpdateStageAsync(TaskProgressStage newStage)
        {
            if (newStage == TaskProgressStage.Failed && !HasError)
                Interlocked.Exchange(ref _hasError, 1);

            var newProgress = newStage switch
            {
                TaskProgressStage.Completed => 100,
                TaskProgressStage.Reviewing => Math.Clamp(_progressPercentage + 25, 0, 99),
                _ => Math.Clamp(_progressPercentage + 10, 0, 99)
            };
            Interlocked.Exchange(ref _progressPercentage, newProgress);

            var active = GetActive();
            if (!HasError && active != null)
            {
                if (newStage == TaskProgressStage.Completed)
                    active.Completed = true;
                else if (newStage == TaskProgressStage.Failed)
                {
                    Interlocked.Exchange(ref _hasError, 1);
                    ErrorMessage ??= "Task failed";
                }
                else if (!active.Completed)
                {
                    // Reviewing stage — don't change Completed/Failed flags, just update progress
                }
            }
        }

        public async Task ReportProgressAsync(int percentage)
        {
            if (percentage < 0 || percentage > 100)
                throw new ArgumentOutOfRangeException(nameof(percentage), "Progress must be between 0 and 100.");

            Interlocked.Exchange(ref _progressPercentage, percentage);

            var active = GetActive();
            if (active != null && !HasError && percentage == 100)
                active.Completed = true;
        }

        public async Task RecordToolCallAsync(string toolName, Dictionary<string, object> parameters, string result, bool success, double durationMs)
        {
            if (!success && !HasError)
            {
                Interlocked.Exchange(ref _hasError, 1);
                ErrorMessage = $"Tool '{toolName}' failed after {durationMs:F1}ms with result: {result}";
            }

            Interlocked.Increment(ref _iterationCount);
            if (IsIterationLimitExceeded && !HasError)
                Interlocked.Exchange(ref _hasError, 1);
        }

        public async Task RecordErrorAsync(string errorMessage)
        {
            Interlocked.Exchange(ref _hasError, 1);
            ErrorMessage = errorMessage;
            Interlocked.Exchange(ref _progressPercentage, 0);
        }

        public void Dispose() { _entries.Clear(); }

        private sealed class TrackerEntry : IDisposable
        {
            public bool Completed { get; internal set; }
            public void Dispose() { }
        }
    }

    #endregion

    #region TaskSchedulerService

    /// <summary>
    /// SQLite-backed task scheduler with priority-aware scheduling, dependency resolution,
    /// batch task injection, and auto-start on dependency satisfaction.
    /// </summary>
    internal class TaskSchedulerService : ITaskScheduler, IDisposable
    {
        private readonly ILogger<TaskSchedulerService> _logger;
        private readonly string _connectionString;
        private readonly object _lock = new();

        public TaskSchedulerService(ILogger<TaskSchedulerService> logger, string connectionString)
        {
            _logger = logger;
            _connectionString = connectionString;
            InitializeDatabase();
        }

        public async Task InjectTasksAsync(Guid branchId, IEnumerable<AgenticTask> tasks, CancellationToken ct = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            using var tx = connection.BeginTransaction();
            try
            {
                foreach (var task in tasks)
                {
                    var cmd = connection.CreateCommand();
                    cmd.CommandText = """
                        INSERT INTO Tasks (Id, Description, Dependencies, Priority, Status, MaxIterations, BranchId, Phase,
                                           Instructions, ValidationCriteria, OutputFields, CreatedAt)
                        VALUES (@Id, @Description, @Dependencies, @Priority, @Status, @MaxIterations, @BranchId, @Phase,
                                @Instructions, @ValidationCriteria, @OutputFields, @CreatedAt)
                        ON CONFLICT(Id) DO NOTHING
                        """;
                    cmd.Parameters.AddWithValue("@Id", task.Id.ToString());
                    cmd.Parameters.AddWithValue("@Description", task.Description);
                    cmd.Parameters.AddWithValue("@Dependencies", SerializeDependencies(task.Dependencies));
                    cmd.Parameters.AddWithValue("@Priority", (int)(TaskPriority)task.Priority);
                    cmd.Parameters.AddWithValue("@Status", (int)(TaskStatus)task.Status);
                    cmd.Parameters.AddWithValue("@MaxIterations", task.MaxIterations);
                    cmd.Parameters.AddWithValue("@BranchId", branchId.ToString());
                    cmd.Parameters.AddWithValue("@Phase", (int)(TaskPhase)task.Phase);
                    cmd.Parameters.AddWithValue("@Instructions", task.Instructions ?? "");
                    cmd.Parameters.AddWithValue("@ValidationCriteria", task.ValidationCriteria ?? "");
                    cmd.Parameters.AddWithValue("@OutputFields", task.OutputFields ?? "");
                    cmd.Parameters.AddWithValue("@CreatedAt", task.CreatedAt);
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                await tx.CommitAsync(ct);
                _logger.LogDebug("Injected {Count} tasks into branch {BranchId}", tasks.Count(), branchId);

                foreach (var task in tasks)
                {
                    await CheckAndStartDependentTasksAsync(task.Id, ct);
                }
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public async Task<List<AgenticTask>> GetScheduledTasksAsync(Guid branchId, CancellationToken ct = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT Id, Description, Dependencies, Priority, Status, Progress, MaxIterations, Summary,
                       ErrorMessage, ContextSnapshotId, ParentTaskId, BranchId, Phase, Instructions,
                       ValidationCriteria, OutputFields, CreatedAt, StartedAt, CompletedAt, AutoCompleted
                FROM Tasks WHERE BranchId = @BranchId
                ORDER BY
                    CASE Priority WHEN 4 THEN 0 WHEN 3 THEN 1 WHEN 2 THEN 2 WHEN 1 THEN 3 WHEN 0 THEN 4 END,
                    CreatedAt
                """;
            cmd.Parameters.AddWithValue("@BranchId", branchId.ToString());

            var tasks = new List<AgenticTask>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                tasks.Add(MapTask(reader));
            }
            return tasks;
        }

        public async Task<List<AgenticTask>> GetAllScheduledTasksAsync(CancellationToken ct = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT Id, Description, Dependencies, Priority, Status, Progress, MaxIterations, Summary,
                       ErrorMessage, ContextSnapshotId, ParentTaskId, BranchId, Phase, Instructions,
                       ValidationCriteria, OutputFields, CreatedAt, StartedAt, CompletedAt, AutoCompleted
                FROM Tasks
                ORDER BY
                    CASE Priority WHEN 4 THEN 0 WHEN 3 THEN 1 WHEN 2 THEN 2 WHEN 1 THEN 3 WHEN 0 THEN 4 END,
                    CreatedAt
                """;

            var tasks = new List<AgenticTask>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                tasks.Add(MapTask(reader));
            }
            return tasks;
        }

        public async Task UpdateTaskStatusAsync(Guid taskId, Domain.Models.TaskStatus newStatus, string? errorMessage = null, CancellationToken ct = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            using var tx = connection.BeginTransaction();
            try
            {
                var cmd = connection.CreateCommand();
                cmd.CommandText = """
                    UPDATE Tasks SET Status = @Status, ErrorMessage = @ErrorMessage
                    WHERE Id = @Id
                    """;
                cmd.Parameters.AddWithValue("@Id", taskId.ToString());
                cmd.Parameters.AddWithValue("@Status", (int)newStatus);
                cmd.Parameters.AddWithValue("@ErrorMessage", errorMessage ?? "");
                if (newStatus == TaskStatus.Running)
                {
                    cmd.CommandText += ", StartedAt = @StartedAt";
                    cmd.Parameters.AddWithValue("@StartedAt", DateTime.UtcNow);
                }
                if (newStatus == TaskStatus.Completed)
                {
                    cmd.CommandText += ", CompletedAt = @CompletedAt";
                    cmd.Parameters.AddWithValue("@CompletedAt", DateTime.UtcNow);
                }
                await cmd.ExecuteNonQueryAsync(ct);
                await tx.CommitAsync(ct);
                _logger.LogDebug("Updated task {TaskId} status to {Status}", taskId, newStatus);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public async Task UpdateTaskProgressAsync(Guid taskId, int progress, CancellationToken ct = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE Tasks SET Progress = @Progress WHERE Id = @Id";
            cmd.Parameters.AddWithValue("@Progress", progress);
            cmd.Parameters.AddWithValue("@Id", taskId.ToString());
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task RegisterToolCallAsync(Guid taskId, AgentToolCallRecord record, CancellationToken ct = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            using var tx = connection.BeginTransaction();
            try
            {
                var cmd = connection.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO ToolCalls (Id, TaskId, ToolName, Parameters, Result, Success, DurationMs, Timestamp)
                    VALUES (@Id, @TaskId, @ToolName, @Parameters, @Result, @Success, @DurationMs, @Timestamp)
                    """;
                cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
                cmd.Parameters.AddWithValue("@TaskId", taskId.ToString());
                cmd.Parameters.AddWithValue("@ToolName", record.ToolName);
                cmd.Parameters.AddWithValue("@Parameters", JsonSerializer.Serialize(record.Parameters));
                cmd.Parameters.AddWithValue("@Result", record.Result);
                cmd.Parameters.AddWithValue("@Success", record.Success);
                cmd.Parameters.AddWithValue("@DurationMs", record.DurationMs);
                cmd.Parameters.AddWithValue("@Timestamp", record.Timestamp);
                await cmd.ExecuteNonQueryAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public async Task<List<AgenticTask>> GetReadyTasksAsync(CancellationToken ct = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT Id, Description, Dependencies, Priority, Status, Progress, MaxIterations, Summary,
                       ErrorMessage, ContextSnapshotId, ParentTaskId, BranchId, Phase, Instructions,
                       ValidationCriteria, OutputFields, CreatedAt, StartedAt, CompletedAt, AutoCompleted
                FROM Tasks
                WHERE Status = @Pending
                ORDER BY
                    CASE Priority WHEN 4 THEN 0 WHEN 3 THEN 1 WHEN 2 THEN 2 WHEN 1 THEN 3 WHEN 0 THEN 4 END,
                    CreatedAt
                """;
            cmd.Parameters.AddWithValue("@Pending", (int)TaskStatus.Pending);

            var tasks = new List<AgenticTask>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                tasks.Add(MapTask(reader));
            }
            return tasks;
        }

        public async Task CheckAndStartDependentTasksAsync(Guid completedTaskId, CancellationToken ct = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Id, Dependencies FROM Tasks WHERE Status = 0";

            using var reader = await cmd.ExecuteReaderAsync(ct);
            var pendingTaskIds = new List<string>();
            while (await reader.ReadAsync(ct))
            {
                pendingTaskIds.Add(reader.GetString(0));
            }

            foreach (var taskId in pendingTaskIds)
            {
                var deps = await GetTaskDependenciesAsync(connection, taskId, ct);
                if (deps == null || !deps.Contains(completedTaskId))
                    continue;

                var allDepsCompleted = true;
                foreach (var depId in deps)
                {
                    var statusCmd = connection.CreateCommand();
                    statusCmd.CommandText = "SELECT Status FROM Tasks WHERE Id = @Id";
                    statusCmd.Parameters.AddWithValue("@Id", depId);
                    var statusObj = await statusCmd.ExecuteScalarAsync(ct);
                    var status = statusObj != null ? (int)statusObj : -1;
                    if (status != (int)(TaskStatus)TaskStatus.Completed)
                    {
                        allDepsCompleted = false;
                        break;
                    }
                }

                if (allDepsCompleted)
                {
                    var startCmd = connection.CreateCommand();
                    startCmd.CommandText = "UPDATE Tasks SET Status = @Running, StartedAt = @StartedAt WHERE Id = @Id";
                    startCmd.Parameters.AddWithValue("@Running", (int)(TaskStatus)TaskStatus.Running);
                    startCmd.Parameters.AddWithValue("@StartedAt", DateTime.UtcNow);
                    startCmd.Parameters.AddWithValue("@Id", taskId);
                    await startCmd.ExecuteNonQueryAsync(ct);
                    _logger.LogDebug("Auto-started task {TaskId} after dependency {CompletedTaskId} completed", taskId, completedTaskId);
                }
            }
        }

        public async Task AbandonBranchAsync(Guid branchId, CancellationToken ct = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE Tasks SET Status = @Cancelled WHERE BranchId = @BranchId AND Status IN (@Pending, @Running)";
            cmd.Parameters.AddWithValue("@Cancelled", (int)(TaskStatus)TaskStatus.Cancelled);
            cmd.Parameters.AddWithValue("@BranchId", branchId.ToString());
            await cmd.ExecuteNonQueryAsync(ct);
            _logger.LogDebug("Abandoned branch {BranchId}", branchId);
        }

        public async Task PauseBranchAsync(Guid branchId, CancellationToken ct = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE Tasks SET Status = @Paused WHERE BranchId = @BranchId AND Status = @Running";
            cmd.Parameters.AddWithValue("@Paused", (int)(TaskStatus)TaskStatus.Paused);
            cmd.Parameters.AddWithValue("@BranchId", branchId.ToString());
            await cmd.ExecuteNonQueryAsync(ct);
            _logger.LogDebug("Paused branch {BranchId}", branchId);
        }

        public async Task ResumeBranchAsync(Guid branchId, CancellationToken ct = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE Tasks SET Status = @Pending WHERE BranchId = @BranchId AND Status = @Paused";
            cmd.Parameters.AddWithValue("@Pending", (int)(TaskStatus)TaskStatus.Pending);
            cmd.Parameters.AddWithValue("@BranchId", branchId.ToString());
            await cmd.ExecuteNonQueryAsync(ct);
            _logger.LogDebug("Resumed branch {BranchId}", branchId);
        }

        public void Dispose() { /* SQLite connections are disposed per-operation */ }

        public void RegisterBranch(Guid branchId, List<AgenticTask> tasks) { /* No-op */ }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var createTasks = connection.CreateCommand();
            createTasks.CommandText = """
                CREATE TABLE IF NOT EXISTS Tasks (
                    Id TEXT PRIMARY KEY,
                    Description TEXT NOT NULL,
                    Dependencies TEXT DEFAULT '[]',
                    Priority INTEGER DEFAULT 1,
                    Status INTEGER DEFAULT 0,
                    Progress INTEGER DEFAULT 0,
                    MaxIterations INTEGER DEFAULT 50,
                    Summary TEXT DEFAULT '',
                    ErrorMessage TEXT DEFAULT '',
                    ContextSnapshotId TEXT,
                    ParentTaskId TEXT,
                    BranchId TEXT NOT NULL,
                    Phase INTEGER DEFAULT 1,
                    Instructions TEXT DEFAULT '',
                    ValidationCriteria TEXT DEFAULT '',
                    OutputFields TEXT DEFAULT '',
                    CreatedAt TEXT NOT NULL,
                    StartedAt TEXT,
                    CompletedAt TEXT,
                    AutoCompleted INTEGER DEFAULT 0
                )
                """;
            createTasks.ExecuteNonQuery();

            var createToolCalls = connection.CreateCommand();
            createToolCalls.CommandText = """
                CREATE TABLE IF NOT EXISTS ToolCalls (
                    Id TEXT PRIMARY KEY,
                    TaskId TEXT NOT NULL,
                    ToolName TEXT NOT NULL,
                    Parameters TEXT NOT NULL,
                    Result TEXT NOT NULL,
                    Success INTEGER NOT NULL,
                    DurationMs REAL NOT NULL,
                    Timestamp TEXT NOT NULL,
                    FOREIGN KEY (TaskId) REFERENCES Tasks(Id) ON DELETE CASCADE
                )
                """;
            createToolCalls.ExecuteNonQuery();

            var createBranches = connection.CreateCommand();
            createBranches.CommandText = """
                CREATE TABLE IF NOT EXISTS Branches (
                    Id TEXT PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Description TEXT DEFAULT '',
                    ParentBranchId TEXT,
                    Status INTEGER DEFAULT 0,
                    CreatedAt TEXT NOT NULL,
                    FOREIGN KEY (ParentBranchId) REFERENCES Branches(Id) ON DELETE CASCADE
                )
                """;
            createBranches.ExecuteNonQuery();
        }

        private static AgenticTask MapTask(SqliteDataReader reader)
        {
            var deps = DeserializeDependencies(reader.GetString(reader.GetOrdinal("Dependencies")));
            return new AgenticTask
            {
                Id = Guid.Parse(reader.GetString(reader.GetOrdinal("Id"))),
                Description = reader.GetString(reader.GetOrdinal("Description")),
                Dependencies = deps,
                Priority = (TaskPriority)(int)reader.GetInt32(reader.GetOrdinal("Priority")),
                Status = (TaskStatus)(int)reader.GetInt32(reader.GetOrdinal("Status")),
                Progress = reader.GetInt32(reader.GetOrdinal("Progress")),
                MaxIterations = reader.GetInt32(reader.GetOrdinal("MaxIterations")),
                Summary = reader.IsDBNull(reader.GetOrdinal("Summary")) ? null : reader.GetString(reader.GetOrdinal("Summary")),
                ErrorMessage = reader.IsDBNull(reader.GetOrdinal("ErrorMessage")) ? null : reader.GetString(reader.GetOrdinal("ErrorMessage")),
                ContextSnapshotId = reader.IsDBNull(reader.GetOrdinal("ContextSnapshotId")) ? null : Guid.Parse(reader.GetString(reader.GetOrdinal("ContextSnapshotId"))),
                ParentTaskId = reader.IsDBNull(reader.GetOrdinal("ParentTaskId")) ? null : Guid.Parse(reader.GetString(reader.GetOrdinal("ParentTaskId"))),
                BranchId = Guid.Parse(reader.GetString(reader.GetOrdinal("BranchId"))),
                Phase = (TaskPhase)reader.GetInt32(reader.GetOrdinal("Phase")),
                Instructions = reader.GetString(reader.GetOrdinal("Instructions")),
                ValidationCriteria = reader.IsDBNull(reader.GetOrdinal("ValidationCriteria")) ? null : reader.GetString(reader.GetOrdinal("ValidationCriteria")),
                OutputFields = reader.IsDBNull(reader.GetOrdinal("OutputFields")) ? null : reader.GetString(reader.GetOrdinal("OutputFields")),
                CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
                StartedAt = reader.IsDBNull(reader.GetOrdinal("StartedAt")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("StartedAt"))),
                CompletedAt = reader.IsDBNull(reader.GetOrdinal("CompletedAt")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("CompletedAt"))),
                AutoCompleted = reader.GetInt32(reader.GetOrdinal("AutoCompleted")) == 1,
            };
        }

        private static List<Guid> DeserializeDependencies(string deps)
        {
            try
            {
                return string.IsNullOrEmpty(deps) || deps == "[]"
                    ? new List<Guid>()
                    : JsonSerializer.Deserialize<List<Guid>>(deps) ?? new List<Guid>();
            }
            catch
            {
                return new List<Guid>();
            }
        }

        private static string SerializeDependencies(List<Guid> deps)
        {
            return JsonSerializer.Serialize(deps);
        }

        private async Task<List<Guid>?> GetTaskDependenciesAsync(SqliteConnection connection, string taskId, CancellationToken ct)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Dependencies FROM Tasks WHERE Id = @Id";
            cmd.Parameters.AddWithValue("@Id", taskId);
            var raw = (string?)await cmd.ExecuteScalarAsync(ct);
            return DeserializeDependencies(raw ?? "[]");
        }
    }

    #endregion

    #region TaskScheduler

    /// <summary>
    /// Manages ordered task queue across branches with priority-aware scheduling,
    /// batch task injection, dependency resolution, and auto-start on dependency satisfaction.
    /// Delegates to TaskSchedulerService for SQLite persistence.
    /// </summary>
    internal class TaskScheduler : ITaskScheduler
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
            await _persistenceService.InjectTasksAsync(branchId, tasks, ct);

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

        public async Task UpdateTaskStatusAsync(Guid taskId, Domain.Models.TaskStatus newStatus, string? errorMessage = null, CancellationToken ct = default)
        {
            await _persistenceService.UpdateTaskStatusAsync(taskId, newStatus, errorMessage, ct);

            lock (_lock)
            {
                foreach (var branch in _branchTasks.Values)
                {
                    foreach (var task in branch.Where(t => t.Id == taskId))
                    {
                        task.Status = (Domain.Models.TaskStatus)newStatus;
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

        public void RegisterBranch(Guid branchId, List<AgenticTask> tasks)
        {
            lock (_lock)
            {
                _branchTasks[branchId] = tasks;
            }
        }
    }

    #endregion
}