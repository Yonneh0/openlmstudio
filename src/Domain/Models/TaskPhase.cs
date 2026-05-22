namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Represents the current phase of a task's lifecycle.
/// </summary>
public enum TaskPhase
{
    /// <summary>Setting up project environment and dependencies.</summary>
    ProjectSetup,
    /// <summary>Analyzing the problem and planning the approach.</summary>
    Analysis,
    /// <summary>Executing the planned actions.</summary>
    Execution,
    /// <summary>Reviewing results and validating correctness.</summary>
    Review,
    /// <summary>Task is complete and finalizing.</summary>
    Completion
}