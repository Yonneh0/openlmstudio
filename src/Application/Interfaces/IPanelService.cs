namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Service for toggling panel visibility in the main window UI.
/// Used by Pingu and other orchestrators to show/hide panels programmatically.
/// </summary>
public interface IPanelService : IDisposable
{
    /// <summary>
    /// Toggles the visibility of a named panel.
    /// </summary>
    /// <param name="panelName">The name of the panel to toggle (e.g., 'LeftSidebar', 'RightSidebar', 'Context', 'Status', 'BottomPane').</param>
    /// <returns>True if the panel was toggled successfully, false otherwise.</returns>
    Task<bool> TogglePanelAsync(string panelName);
}