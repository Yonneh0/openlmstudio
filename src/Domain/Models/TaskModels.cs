// Brought to you by Carls' Jr.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenLMStudio.Domain.Models;

// ============================================================================
// Enums
// ============================================================================

/// <summary>
/// Priority level for task scheduling.
/// </summary>
public enum TaskPriority
{
    Low = 0,
    Medium = 1,
    Normal = 2,
    High = 3,
    Critical = 4,
}

/// <summary>
/// Represents the current state of an agent task.
/// </summary>
public enum AgentState
{
    /// <summary>Agent is idle and not processing any task.</summary>
    Idle = 0,

    /// <summary>Agent is generating a plan for the task.</summary>
    Planning = 1,

    /// <summary>Agent is executing actions from the plan.</summary>
    Acting = 2,

    /// <summary>Agent has been paused by the user.</summary>
    Paused = 3,

    /// <summary>Agent has successfully completed the task.</summary>
    Completed = 4,

    /// <summary>Agent has failed due to errors.</summary>
    Failed = 5
}

/// <summary>
/// Extended agent states for the task management system.
/// </summary>
public enum AgentStateExtended
{
    /// <summary>Agent is idle and not processing any task.</summary>
    Idle = 0,

    /// <summary>Agent is generating a plan for the task.</summary>
    Planning = 1,

    /// <summary>Agent is executing actions from the plan.</summary>
    Acting = 2,

    /// <summary>Agent has been paused by the user.</summary>
    Paused = 3,

    /// <summary>Agent has successfully completed the task.</summary>
    Completed = 4,

    /// <summary>Agent has failed due to errors.</summary>
    Failed = 5,

    /// <summary>Agent is aborting current operations.</summary>
    Aborting = 6,

    /// <summary>Agent is waiting for user approval.</summary>
    WaitingForApproval = 7,

    /// <summary>Agent is awaiting a plan response.</summary>
    AwaitingPlanResponse = 8,
}

/// <summary>
/// Status of a task branch.
/// </summary>
public enum TaskBranchStatus
{
    /// <summary>Branch is actively processing tasks.</summary>
    Active,
    /// <summary>Branch has been paused by the user.</summary>
    Paused,
    /// <summary>All tasks in this branch are completed.</summary>
    Completed,
    /// <summary>Branch has been abandoned (tasks failed or were cancelled).</summary>
    Abandoned
}

/// <summary>
/// Types of checkpoints in the agent task system.
/// </summary>
public enum CheckpointType
{
    /// <summary>Checkpoint saved after each tool call.</summary>
    Tool,

    /// <summary>Checkpoint saved after each task step.</summary>
    Task,

    /// <summary>Checkpoint saved after task completion.</summary>
    Completion,
}

// ============================================================================
// TaskEntity
// ============================================================================

/// <summary>
/// Represents an agentic task with description, dependencies, status, and progress.
/// </summary>
public class TaskEntity
{
    /// <summary>
    /// Unique identifier for this task.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Human-readable title for the task.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of what the task should accomplish.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// List of task IDs that this task depends on.
    /// </summary>
    public List<Guid> Dependencies { get; set; } = new();

    /// <summary>
    /// Current status of the task.
    /// </summary>
    public TaskStatus Status { get; set; } = TaskStatus.Pending;

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    /// Priority level of the task.
    /// </summary>
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;

    /// <summary>
    /// Maximum number of iterations the agent can execute for this task.
    /// </summary>
    public int MaxIterations { get; set; } = 50;

    /// <summary>
    /// Current iteration count.
    /// </summary>
    public int CurrentIteration { get; set; }

    /// <summary>
    /// The chat session ID associated with this task, if any.
    /// </summary>
    public Guid? ChatId { get; set; }

    /// <summary>
    /// Task-specific context for the agent.
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// List of available tools for this task.
    /// </summary>
    public List<string> AvailableTools { get; set; } = new();

    /// <summary>
    /// Result summary of the task.
    /// </summary>
    public string? ResultSummary { get; set; }

    /// <summary>
    /// Error message if the task failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Parent task ID, if this task is a sub-task.
    /// </summary>
    public Guid? ParentTaskId { get; set; }

    /// <summary>
    /// Timestamp when the task was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the task started execution.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Timestamp when the task completed or failed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Whether the task is currently active.
    /// </summary>
    public bool IsActive => Status == TaskStatus.Running;
}

// ============================================================================
// TaskBranch
// ============================================================================

/// <summary>
/// Represents a branch of related tasks in the agent task hierarchy.
/// Supports nested branches (e.g., "forensic-analysis" → "decompile" → "surface-scan").
/// </summary>
public class TaskBranch
{
    /// <summary>
    /// Unique identifier for this branch.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Human-readable name for this branch.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the branch purpose.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Parent branch ID if this is a nested sub-branch.
    /// </summary>
    public Guid? ParentBranchId { get; set; }

    /// <summary>
    /// Current status of the branch.
    /// </summary>
    public TaskBranchStatus Status { get; set; } = TaskBranchStatus.Active;

    /// <summary>
    /// List of task IDs belonging to this branch.
    /// </summary>
    public List<Guid> TaskIds { get; set; } = new();

    /// <summary>
    /// Timestamp when the branch was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the branch was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Child branches of this branch.
    /// </summary>
    public List<TaskBranch> ChildBranches { get; set; } = new();

    /// <summary>
    /// Whether this branch is a root branch (no parent).
    /// </summary>
    public bool IsRoot => ParentBranchId == null;
}

// ============================================================================
// TaskCheckpoint
// ============================================================================

/// <summary>
/// Represents a checkpoint saved during agent task execution.
/// </summary>
public class TaskCheckpoint
{
    /// <summary>Unique identifier for this checkpoint.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Task ID this checkpoint belongs to.</summary>
    public Guid TaskId { get; set; }

    /// <summary>Checkpoint version.</summary>
    public int Version { get; set; }

    /// <summary>Checkpoint timestamp.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Checkpoint type (Tool, Task, or Completion).</summary>
    public CheckpointType Type { get; set; }

    /// <summary>Name of the tool that created this checkpoint.</summary>
    public string? ToolName { get; set; }

    /// <summary>Tool parameters at the time of checkpoint.</summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>Tool result at the time of checkpoint.</summary>
    public string? Result { get; set; }

    /// <summary>Agent state at the time of checkpoint.</summary>
    public AgentStateExtended AgentState { get; set; } = AgentStateExtended.Idle;

    /// <summary>Agent task state at the time of checkpoint.</summary>
    public AgentTaskState? AgentTaskState { get; set; }

    /// <summary>Whether this checkpoint was saved during an attempt_completion.</summary>
    public bool IsCompletionCheckpoint { get; set; }

    /// <summary>Completion message timestamp, if this is a completion checkpoint.</summary>
    public DateTime? CompletionMessageTimestamp { get; set; }

    /// <summary>
    /// Creates a tool checkpoint.
    /// </summary>
    public static TaskCheckpoint CreateToolCheckpoint(Guid taskId, string toolName, Dictionary<string, object> parameters, string result)
    {
        return new TaskCheckpoint
        {
            TaskId = taskId,
            Type = CheckpointType.Tool,
            ToolName = toolName,
            Parameters = parameters,
            Result = result,
            Timestamp = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Creates a task checkpoint.
    /// </summary>
    public static TaskCheckpoint CreateTaskCheckpoint(Guid taskId)
    {
        return new TaskCheckpoint
        {
            TaskId = taskId,
            Type = CheckpointType.Task,
            Timestamp = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Creates a completion checkpoint.
    /// </summary>
    public static TaskCheckpoint CreateCompletionCheckpoint(Guid taskId)
    {
        return new TaskCheckpoint
        {
            TaskId = taskId,
            Type = CheckpointType.Completion,
            IsCompletionCheckpoint = true,
            CompletionMessageTimestamp = DateTime.UtcNow,
            Timestamp = DateTime.UtcNow,
        };
    }
}

// ============================================================================
// TaskContextSnapshot
// ============================================================================

// Note: ContextSegment is defined in Domain.Models (ChatContext.cs).
// AgentState is also defined in ChatContext.cs — do NOT duplicate here.

/// <summary>
/// Represents a snapshot of context for an agentic task.
/// Contains compressed history, tool results cache, and project state needed to resume from interruption.
/// </summary>
public class TaskContextSnapshot : IDisposable
{
    /// <summary>Unique identifier linking this snapshot to its parent task.</summary>
    public Guid TaskId { get; set; }

    /// <summary>Current task description/goal (for context matching).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Current agent state for the task. (Defined in ChatContext.cs)</summary>
    public AgentState CurrentState { get; set; } = AgentState.Planning;

    /// <summary>Compressed message sequence for quick re-injection.</summary>
    public List<ContextSegment> CompressedContext { get; set; } = new();

    /// <summary>Dictionary of tool call ID → compressed result for quick lookup during resume.</summary>
    public Dictionary<string, ContextSegment> ToolResultsCache { get; set; } = new();

    /// <summary>Active file tree at time of capture (compact representation).</summary>
    public string? ActiveFileTree { get; set; }

    /// <summary>Git status snapshot at time of capture.</summary>
    public string? GitStatusSnapshot { get; set; }

    /// <summary>List of relevant entities: file paths, code definitions, concepts mentioned in current context.</summary>
    public List<string> RelevantEntities { get; set; } = new();

    /// <summary>Tokens consumed by compressed context (for budget tracking).</summary>
    public long CompressedContextTokenCount { get; set; }

    /// <summary>Timestamp when the snapshot was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Last time this snapshot was updated.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Whether this snapshot should be archived (not discarded) on task completion.</summary>
    public bool ArchiveOnCompletion { get; set; } = true;

    /// <summary>The full conversation context + project state snapshot from when an agent analyzed file changes and suggested them.
    /// Includes: AnalyzedChatHistory, ProjectStateAtTimeOfAnalysis, RelevantContextSegments.</summary>
    public AiAnalysisResult? AiAnalysis { get; set; }

    public void Dispose()
    {
        // No unmanaged resources to clean up
    }
}

// ============================================================================
// AgentTaskState
// ============================================================================

/// <summary>
/// Represents the current state of an agent task with all tracking fields.
/// </summary>
public class AgentTaskState
{
    /// <summary>Unique task ID.</summary>
    public Guid TaskId { get; set; }

    /// <summary>Unique task identifier (ULID).</summary>
    public string Ulid { get; set; } = string.Empty;

    /// <summary>Current mode (Plan or Act).</summary>
    public string Mode { get; set; } = "Act";

    /// <summary>Current task progress tracking.</summary>
    public AgentTaskProgress? TaskProgress { get; set; }

    /// <summary>Range of deleted conversation history.</summary>
    public (int? Start, int? End)? ConversationHistoryDeletedRange { get; set; }

    /// <summary>Cache for file reads.</summary>
    public Dictionary<string, string> FileReadCache { get; set; } = new();

    /// <summary>Count of consecutive mistakes.</summary>
    public int ConsecutiveMistakeCount { get; set; }

    /// <summary>Flag indicating if a tool was rejected.</summary>
    public bool DidRejectTool { get; set; }

    /// <summary>Flag indicating if a file was edited.</summary>
    public bool DidEditFile { get; set; }

    /// <summary>Flag indicating if a tool was already used.</summary>
    public bool DidAlreadyUseTool { get; set; }

    /// <summary>Flag indicating if the task is awaiting a plan response.</summary>
    public bool IsAwaitingPlanResponse { get; set; }

    /// <summary>Flag indicating if the task responded to a plan ask by switching mode.</summary>
    public bool DidRespondToPlanAskBySwitchingMode { get; set; }

    /// <summary>Flag indicating if double-check completion is pending.</summary>
    public bool DoubleCheckCompletionPending { get; set; }

    /// <summary>Flag indicating if the task is currently summarizing.</summary>
    public bool CurrentlySummarizing { get; set; }

    /// <summary>Name of the last tool used.</summary>
    public string? LastToolName { get; set; }

    /// <summary>Content of the user message.</summary>
    public string? UserMessageContent { get; set; }

    /// <summary>Active hook execution.</summary>
    public string? ActiveHookExecution { get; set; }

    /// <summary>Flag indicating if the task is aborting.</summary>
    public bool Abort { get; set; }

    /// <summary>Current agent state.</summary>
    public AgentStateExtended AgentState { get; set; } = AgentStateExtended.Idle;

    /// <summary>Timestamp when the state was last updated.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Resets the state to a clean initial state for a new task.
    /// </summary>
    public void Reset()
    {
        Ulid = AgentTaskSettings.GenerateUlid();
        TaskProgress = new AgentTaskProgress();
        ConversationHistoryDeletedRange = null;
        FileReadCache.Clear();
        ConsecutiveMistakeCount = 0;
        DidRejectTool = false;
        DidEditFile = false;
        DidAlreadyUseTool = false;
        IsAwaitingPlanResponse = false;
        DidRespondToPlanAskBySwitchingMode = false;
        DoubleCheckCompletionPending = false;
        CurrentlySummarizing = false;
        LastToolName = null;
        UserMessageContent = null;
        ActiveHookExecution = null;
        Abort = false;
        AgentState = AgentStateExtended.Idle;
        UpdatedAt = DateTime.UtcNow;
    }
}

// ============================================================================
// AgentTaskProgress
// ============================================================================

/// <summary>
/// Represents task progress tracking with checklist and reminders.
/// </summary>
public class AgentTaskProgress
{
    /// <summary>Current progress percentage (0-100).</summary>
    public int Percentage { get; set; }

    /// <summary>Checklist of task items.</summary>
    public List<AgentTaskChecklistItem> Checklist { get; set; } = new();

    /// <summary>Current step description.</summary>
    public string? CurrentStep { get; set; }

    /// <summary>Reminders to send to the user.</summary>
    public List<string> Reminders { get; set; } = new();

    /// <summary>Last reminder timestamp.</summary>
    public DateTime LastReminderTime { get; set; }

    /// <summary>Interval between reminders (in minutes).</summary>
    public int ReminderIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Adds a checklist item.
    /// </summary>
    public void AddChecklistItem(string description, bool isCompleted = false)
    {
        Checklist.Add(new AgentTaskChecklistItem
        {
            Description = description,
            IsCompleted = isCompleted,
            AddedAt = DateTime.UtcNow,
        });
    }

    /// <summary>
    /// Marks a checklist item as completed.
    /// </summary>
    public void CompleteChecklistItem(string description)
    {
        var item = Checklist.FirstOrDefault(i => i.Description == description);
        if (item != null)
        {
            item.IsCompleted = true;
            item.CompletedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Updates the current step.
    /// </summary>
    public void UpdateCurrentStep(string step)
    {
        CurrentStep = step;
    }

    /// <summary>
    /// Adds a reminder.
    /// </summary>
    public void AddReminder(string reminder)
    {
        Reminders.Add(reminder);
        LastReminderTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Calculates the overall completion percentage based on checklist items.
    /// </summary>
    public int CalculateChecklistPercentage()
    {
        if (Checklist.Count == 0)
            return Percentage;

        var completed = Checklist.Count(i => i.IsCompleted);
        var checklistPercentage = (int)((double)completed / Checklist.Count * 100);

        // Weight checklist 70% and manual percentage 30%
        return (int)(checklistPercentage * 0.7 + Percentage * 0.3);
    }
}

/// <summary>
/// A single checklist item in the task progress.
/// </summary>
public class AgentTaskChecklistItem
{
    /// <summary>Description of the checklist item.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Whether the item has been completed.</summary>
    public bool IsCompleted { get; set; }

    /// <summary>When the item was added.</summary>
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the item was completed.</summary>
    public DateTime? CompletedAt { get; set; }
}

// ============================================================================
// AgentTaskSettings
// ============================================================================

/// <summary>
/// Configuration for an agent task.
/// </summary>
public class AgentTaskSettings
{
    /// <summary>Current working directory.</summary>
    public string Cwd { get; set; } = string.Empty;

    /// <summary>Task ID.</summary>
    public Guid TaskId { get; set; }

    /// <summary>Unique task identifier.</summary>
    public string Ulid { get; set; } = string.Empty;

    /// <summary>Current mode (Plan or Act).</summary>
    public string Mode { get; set; } = "Act";

    /// <summary>Whether strict plan mode is enabled.</summary>
    public bool StrictPlanModeEnabled { get; set; }

    /// <summary>Whether YOLO mode is toggled.</summary>
    public bool YoloModeToggled { get; set; }

    /// <summary>Whether double-check completion is enabled.</summary>
    public bool DoubleCheckCompletionEnabled { get; set; }

    /// <summary>VSCode terminal execution mode.</summary>
    public string VscodeTerminalExecutionMode { get; set; } = "default";

    /// <summary>Whether parallel tool calling is enabled.</summary>
    public bool EnableParallelToolCalling { get; set; }

    /// <summary>Whether this is a subagent execution.</summary>
    public bool IsSubagentExecution { get; set; }

    /// <summary>Auto-approval settings.</summary>
    public AgentAutoApprovalSettings AutoApprovalSettings { get; set; } = new();

    /// <summary>Browser settings.</summary>
    public AgentBrowserSettings BrowserSettings { get; set; } = new();

    /// <summary>Focus chain settings.</summary>
    public AgentFocusChainSettings FocusChainSettings { get; set; } = new();

    /// <summary>
    /// Creates default settings for a new task.
    /// </summary>
    public static AgentTaskSettings CreateDefault(string cwd = "")
    {
        return new AgentTaskSettings
        {
            Cwd = cwd,
            TaskId = Guid.NewGuid(),
            Ulid = GenerateUlid(),
            Mode = "Act",
            StrictPlanModeEnabled = false,
            YoloModeToggled = false,
            DoubleCheckCompletionEnabled = true,
            VscodeTerminalExecutionMode = "default",
            EnableParallelToolCalling = true,
            IsSubagentExecution = false,
        };
    }

    /// <summary>Generates a proper ULID using timestamp + random bytes.</summary>
    public static string GenerateUlid()
    {
        var timestamp = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
        var randomBytes = new byte[16];
        var rng = new Random();
        rng.NextBytes(randomBytes);

        var chars = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        var ulid = new char[26];
        var num = timestamp;
        for (var i = 25; i >= 0; i--)
        {
            ulid[i] = chars[(int)(num % 32)];
            num /= 32;
        }
        for (var i = 10; i >= 0; i--)
        {
            var val = randomBytes[i];
            ulid[10 + i] = chars[val % 32];
        }
        return new string(ulid);
    }
}

/// <summary>
/// Auto-approval settings for the agent.
/// </summary>
public class AgentAutoApprovalSettings
{
    /// <summary>Whether auto-approval is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Default timeout for commands (in seconds).</summary>
    public int DefaultTimeout { get; set; } = 30;

    /// <summary>Whether to auto-approve file operations.</summary>
    public bool AutoApproveFileOperations { get; set; } = true;

    /// <summary>Whether to auto-approve command execution.</summary>
    public bool AutoApproveCommandExecution { get; set; } = true;

    /// <summary>Whether to auto-approve browser actions.</summary>
    public bool AutoApproveBrowserActions { get; set; } = true;
}

/// <summary>
/// Browser settings for the agent.
/// </summary>
public class AgentBrowserSettings
{
    /// <summary>Browser width.</summary>
    public int Width { get; set; } = 1280;

    /// <summary>Browser height.</summary>
    public int Height { get; set; } = 720;

    /// <summary>Whether screenshots are enabled.</summary>
    public bool EnableScreenshots { get; set; } = true;
}

/// <summary>
/// Focus chain settings for the agent.
/// </summary>
public class AgentFocusChainSettings
{
    /// <summary>Maximum number of focus chain items.</summary>
    public int MaxItems { get; set; } = 10;

    /// <summary>Whether to prioritize recent items.</summary>
    public bool PrioritizeRecent { get; set; } = true;
}

// ============================================================================
// AgenticTask
// ============================================================================

/// <summary>
/// Represents a discrete unit of work within the agentic task system.
/// </summary>
public record AgenticTask
{
    /// <summary>Unique identifier for this task.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Description of what the task should accomplish.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Task instructions for the agent to follow.</summary>
    public string? Instructions { get; set; }

    /// <summary>Task validation criteria for auto-completion detection.</summary>
    public string? ValidationCriteria { get; set; }

    /// <summary>Expected output fields from the task.</summary>
    public string? OutputFields { get; set; }

    /// <summary>Task dependencies (other task IDs that must complete first).</summary>
    public List<Guid> Dependencies { get; init; } = new();

    /// <summary>Priority level for scheduling.</summary>
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    /// <summary>Current execution status.</summary>
    public TaskStatus Status { get; set; } = TaskStatus.Pending;

    /// <summary>Progress percentage (0-100).</summary>
    public int Progress { get; set; }

    /// <summary>Maximum number of iterations before forcing completion.</summary>
    public int MaxIterations { get; set; } = 50;

    /// <summary>Summary of task outcome.</summary>
    public string? Summary { get; set; }

    /// <summary>Error message if the task failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Context snapshot ID for saving/restoring agent state.</summary>
    public Guid? ContextSnapshotId { get; set; }

    /// <summary>Parent task ID for task hierarchy.</summary>
    public Guid? ParentTaskId { get; set; }

    /// <summary>Branch ID this task belongs to.</summary>
    public Guid BranchId { get; init; } = Guid.NewGuid();

    /// <summary>Current phase of the task.</summary>
    public TaskPhase Phase { get; set; } = TaskPhase.Planning;

    /// <summary>When the task was created.</summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>When the task started execution.</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>When the task completed.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Whether the task was auto-completed by validation.</summary>
    public bool AutoCompleted { get; set; }

    /// <summary>Tool calls made during task execution.</summary>
    public List<AgentToolCallRecord> ToolCalls { get; set; } = new();
}

/// <summary>
/// Records a tool call made by an agent during task execution.
/// </summary>
public record AgentToolCallRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TaskId { get; init; }
    public string ToolName { get; init; } = string.Empty;
    public Dictionary<string, object> Parameters { get; init; } = new();
    public string? Result { get; init; }
    public bool Success { get; init; }
    public double DurationMs { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}