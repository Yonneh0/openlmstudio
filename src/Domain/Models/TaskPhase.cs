namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Represents the current phase of a task's lifecycle.
/// </summary>
public enum TaskPhase
{
    /// <summary>Planning the approach and strategy.</summary>
    Planning = 1,

    /// <summary>Executing the planned actions.</summary>
    Acting = 2,

    /// <summary>Reviewing results and validating correctness.</summary>
    Reviewing = 3,

    /// <summary>Task is complete and finalizing.</summary>
    Completed = 4,

    /// <summary>Task has failed.</summary>
    Failed = 5,
}