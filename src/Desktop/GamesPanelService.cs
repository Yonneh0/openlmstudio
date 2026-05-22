// Brought to you by Carls' Jr.
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Desktop.Controls;

namespace OpenLMStudio.Desktop;

/// <summary>
/// Concrete implementation of IGamesPanel that wraps the Avalonia GamesPanel control.
/// </summary>
public class GamesPanelService : IGamesPanel
{
    private readonly GamesPanel _control;

    public GamesPanelService()
    {
        _control = new GamesPanel();
    }

    public void ActivateGame(string gameName)
    {
        _control.ActivateGame(gameName);
    }
}