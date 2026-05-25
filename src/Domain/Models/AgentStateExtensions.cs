namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Extended agent states for the task management system.
/// </summary>
public enum AgentStateExtended
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
    Failed = 5,

    /// <summary>
    /// Agent is aborting current operations.
    /// </summary>
    Aborting = 6,

    /// <summary>
    /// Agent is waiting for user approval.
    /// </summary>
    WaitingForApproval = 7,

    /// <summary>
    /// Agent is awaiting a plan response.
    /// </summary>
    AwaitingPlanResponse = 8,
}