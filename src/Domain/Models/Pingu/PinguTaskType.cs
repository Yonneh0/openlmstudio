namespace OpenLMStudio.Domain.Models.Pingu;

/// <summary>
/// Types of tasks Pingu can be assigned to orchestrate.
/// </summary>
public enum PinguTaskType
{
    /// <summary>
    /// Pingu is acting as the general task orchestrator for the agent harness.
    /// </summary>
    TaskOrchestration,

    /// <summary>
    /// Pingu is controlling the UI — switching tabs, toggling panels, clicking buttons, managing windows.
    /// </summary>
    UIControl,

    /// <summary>
    /// Pingu is managing model lifecycle — loading, unloading, switching models.
    /// </summary>
    ModelManagement,

    /// <summary>
    /// Pingu is playing a built-in game (Minesweeper, Tetris, Snake, Jezzball, Solitaire).
    /// </summary>
    GamePlay,

    /// <summary>
    /// Pingu is wandering around the UI aimlessly, exploring panels, clicking buttons, observing behavior.
    /// </summary>
    Wandering,

    /// <summary>
    /// Pingu is acting as a simple assistant — direct Q&A without orchestration duties.
    /// </summary>
    UserAssistant,

    /// <summary>
    /// Pingu is running a model (inference, chat, image generation, etc.).
    /// </summary>
    ModelRun,

    /// <summary>
    /// Pingu is performing a multi-step workflow combining multiple task types.
    /// </summary>
    Workflow,
}