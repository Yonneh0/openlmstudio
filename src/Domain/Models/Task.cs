namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Represents an agentic task with description, dependencies, status tracking, and progress.
/// </summary>
public class Task
{
    /// <summary>
    /// Unique identifier for this task (GUID).
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Human-readable description of the task.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// List of task IDs that must complete before this task can start.
    /// </summary>
    public List<Guid> Dependencies { get; set; } = new();

    /// <summary>
    /// Current status of the task.
    /// </summary>
    public TaskStatus Status { get; set; } = TaskStatus.Pending;

    /// <summary>
    /// Percentage complete (0-100).
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    /// Priority level of the task (higher = more urgent).
    /// </summary>
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;

    /// <summary>
    /// Maximum iterations the agent is allowed to execute for this task.
    /// </summary>
    public int MaxIterations { get; set; } = 50;

    /// <summary>
    /// Summary of the task result after completion.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Error message if the task failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Task context snapshot ID for storing/retrieving agentic context.
    /// </summary>
    public Guid? ContextSnapshotId { get; set; }

    /// <summary>
    /// Parent task ID if this is a subtask.
    /// </summary>
    public Guid? ParentTaskId { get; set; }

    /// <summary>
    /// Branch ID this task belongs to (for task grouping).
    /// </summary>
    public Guid BranchId { get; set; } = Guid.Empty;

    /// <summary>
    /// Current phase of the task.
    /// </summary>
    public TaskPhase Phase { get; set; } = TaskPhase.Analysis;

    /// <summary>
    /// Detailed agent instructions for completing this task.
    /// </summary>
    public string Instructions { get; set; } = string.Empty;

    /// <summary>
    /// AI-verified completion criteria for the task.
    /// </summary>
    public string? ValidationCriteria { get; set; }

    /// <summary>
    /// Structured output schema for task completion (JSON schema).
    /// </summary>
    public string? OutputFields { get; set; }

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
    /// List of tool calls made during agent execution.
    /// </summary>
    public List<AgentToolCallRecord> ToolCalls { get; set; } = new();

    /// <summary>
    /// Whether this task was automatically completed by the agent.
    /// </summary>
    public bool AutoCompleted { get; set; }
}

/// <summary>
/// Status of an agentic task.
/// </summary>
public enum TaskStatus
{
    /// <summary>Task is pending and waiting for dependencies to complete.</summary>
    Pending,
    /// <summary>Task is actively being processed.</summary>
    Running,
    /// <summary>Task has been paused by the user.</summary>
    Paused,
    /// <summary>Task completed successfully.</summary>
    Completed,
    /// <summary>Task failed with an error.</summary>
    Failed,
    /// <summary>Task was cancelled by the user.</summary>
    Cancelled
}

/// <summary>
/// Priority level for an agentic task.
/// </summary>
public enum TaskPriority
{
    /// <summary>Low priority — executed when resources are available.</summary>
    Low,
    /// <summary>Normal priority — standard scheduling.</summary>
    Normal,
    /// <summary>High priority — executed before normal tasks.</summary>
    High,
    /// <summary>Critical priority — interrupts lower-priority tasks.</summary>
    Critical
}