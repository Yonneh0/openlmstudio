// Brought to you by Carls' Jr.
using System;
using System.Collections.Generic;
using System.Linq;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Desktop;

/// <summary>
/// Concrete implementation of IPanelService that toggles visibility of panels in MainWindow.
/// Uses the static MainWindow reference set by App after construction.
/// </summary>
public class PanelService : IPanelService
{
    private static MainWindow? _window;
    private readonly Dictionary<string, string> _panelNames = new()
    {
        ["LeftSidebar"] = "LeftPanel",
        ["RightSidebar"] = "RightPanel",
        ["Context"] = "RightContextContent",
        ["Status"] = "StatusBar",
        ["BottomPane"] = "BottomPane",
        ["Agent"] = "AgentTabContent",
        ["ImageGeneration"] = "ImageGenerationTabContent",
        ["Pingu"] = "RightPinguContent",
        ["Games"] = "RightGamesContent",
        ["Chat"] = "ChatTabContent",
        ["Server"] = "ServerTabContent",
        ["Models"] = "ModelsTabContent",
        ["Devices"] = "DevicesTabContent",
        ["PinguTab"] = "PinguTabContent",
    };

    public static void SetWindow(MainWindow window) => _window = window;

    private MainWindow Window => _window ?? throw new InvalidOperationException("PanelService: MainWindow not set");

    public PanelService()
    {
        // Ensure Window is set
        if (_window == null)
            throw new InvalidOperationException("PanelService: MainWindow not set. Call SetWindow() before using PanelService.");
    }

    public bool IsPanelVisible(string panelName)
    {
        if (_panelNames.TryGetValue(panelName, out var name))
        {
            var panel = Window.Find<Panel>(name);
            return panel?.IsVisible ?? false;
        }
        return false;
    }

    public async Task<bool> TogglePanelAsync(string panelName)
    {
        if (_panelNames.TryGetValue(panelName, out var name))
        {
            var panel = Window.Find<Panel>(name);
            if (panel == null)
                return false;

            panel.IsVisible = !panel.IsVisible;
            return true;
        }
        return false;
    }

    public async Task<bool> ShowPanelAsync(string panelName)
    {
        if (_panelNames.TryGetValue(panelName, out var name))
        {
            var panel = Window.Find<Panel>(name);
            if (panel == null)
                return false;

            panel.IsVisible = true;
            return true;
        }
        return false;
    }

    public async Task<bool> HidePanelAsync(string panelName)
    {
        if (_panelNames.TryGetValue(panelName, out var name))
        {
            var panel = Window.Find<Panel>(name);
            if (panel == null)
                return false;

            panel.IsVisible = false;
            return true;
        }
        return false;
    }

    public void Dispose() { }
}