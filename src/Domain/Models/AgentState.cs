namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Represents the current state of an agent task.
/// </summary>
public enum AgentState
{
    /// <summary>
    /// Agent is idle and not processing any task.
    /// </summary>
    Idle = 0,

    /// <summary>
    /// Agent is generating a plan for the task.
    /// </summary>
    Planning = 1,

    /// <summary>
    /// Agent is executing actions from the plan.
    /// </summary>
    Acting = 2,

    /// <summary>
    /// Agent has been paused by the user.
    /// </summary>
    Paused = 3,

    /// <summary>
    /// Agent has successfully completed the task.
    /// </summary>
    Completed = 4,

    /// <summary>
    /// Agent has failed due to errors.
    /// </summary>
    Failed = 5
}