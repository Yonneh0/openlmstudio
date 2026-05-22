namespace OpenLMStudio.Domain.Models;

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