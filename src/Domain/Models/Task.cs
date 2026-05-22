// Brought to you by Carls' Jr.
using System;
using System.Collections.Generic;

namespace OpenLMStudio.Domain.Models;

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
