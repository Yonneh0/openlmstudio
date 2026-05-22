namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Interface for game panel control, allowing infrastructure services to activate games without
/// directly referencing the Avalonia UI component.
/// </summary>
public interface IGamesPanel
{
    /// <summary>
    /// Activates a game by name (case-insensitive).
    /// Use "none" to close the current game.
    /// </summary>
    void ActivateGame(string gameName);
}