namespace OpenLMStudio.Domain.Models;

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
