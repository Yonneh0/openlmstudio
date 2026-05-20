namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Interface for Pingu automation actions and animations.
/// </summary>
public interface IPinguAutomation
{
    /// <summary>
    /// Enters control mode, greying out UI and starting animated Pingu walking.
    /// </summary>
    Task EnterControlModeAsync();

    /// <summary>
    /// Handles drag-to-pause VM management.
    /// </summary>
    Task HandleDragToPauseAsync(DragEvent e);

    /// <summary>
    /// Performs an action with associated avatar animation.
    /// </summary>
    Task PerformActionAsync(string action, Element? target = null);
}

/// <summary>
/// Represents a drag event in the Pingu automation system.
/// </summary>
public class DragEvent
{
    public string? Target { get; set; }
}

/// <summary>
/// Represents a UI element target for automation.
/// </summary>
public class Element
{
    public string? VmId { get; set; }
}