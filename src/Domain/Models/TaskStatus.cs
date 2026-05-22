namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Current status of a task.
/// </summary>
public enum TaskStatus
{
    /// <summary>Task is pending execution.</summary>
    Pending = 0,

    /// <summary>Task is currently running.</summary>
    Running = 1,

    /// <summary>Task is paused.</summary>
    Paused = 2,

    /// <summary>Task has failed.</summary>
    Failed = 3,

    /// <summary>Task has been cancelled.</summary>
    Cancelled = 4,

    /// <summary>Task has completed successfully.</summary>
    Completed = 5,
}