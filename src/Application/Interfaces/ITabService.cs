namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Service for switching tabs in the main window UI.
/// Used by Pingu and other orchestrators to navigate the application programmatically.
/// </summary>
public interface ITabService : IDisposable
{
    /// <summary>
    /// Gets the currently active tab name.
    /// </summary>
    string ActiveTab { get; }

    /// <summary>
    /// Switches to the specified tab.
    /// </summary>
    /// <param name="tabName">The name of the tab to switch to (e.g., "Chat", "Server", "Models", "Devices", "Context", "Agent").</param>
    /// <returns>True if the tab was successfully switched, false otherwise.</returns>
    Task<bool> SwitchTabAsync(string tabName);

    /// <summary>
    /// Gets a list of all available tab names.
    /// </summary>
    IReadOnlyList<string> GetAvailableTabs();
}