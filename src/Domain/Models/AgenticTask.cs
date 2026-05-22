namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Records a tool call made during agent execution.
/// </summary>
public record AgentToolCallRecord(
    string ToolName,
    Dictionary<string, object> Parameters,
    string Result,
    bool Success,
    double DurationMs,
    DateTime Timestamp);

/// <summary>
/// Represents an agentic task with description, dependencies, status tracking, and progress.
/// </summary>
public record AgenticTask
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Description { get; init; } = string.Empty;
    public List<Guid> Dependencies { get; init; } = new();
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    public int Progress { get; init; }
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    public int MaxIterations { get; init; } = 50;
    public string? Summary { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? ContextSnapshotId { get; init; }
    public Guid? ParentTaskId { get; init; }
    public Guid BranchId { get; init; } = default;
    public TaskPhase Phase { get; init; } = TaskPhase.Analysis;
    public string Instructions { get; init; } = string.Empty;
    public string? ValidationCriteria { get; init; }
    public string? OutputFields { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<AgentToolCallRecord> ToolCalls { get; set; } = new();
    public bool AutoCompleted { get; init; }
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

// Note: TaskPhase enum is defined in TaskPhase.cs
