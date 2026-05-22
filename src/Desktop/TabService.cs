// Brought to you by Carls' Jr.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Desktop;

/// <summary>
/// Concrete implementation of ITabService that delegates to the MainWindow's TabControl.
/// Uses the static MainWindow reference set by App after construction.
/// </summary>
public class TabService : ITabService
{
    private static MainWindow? _window;

    public static void SetWindow(MainWindow window) => _window = window;

    private MainWindow Window => _window ?? throw new InvalidOperationException("TabService: MainWindow not set");

    private readonly string[] _availableTabs =
    [
        "Chat", "Server", "Models", "Devices", "Context", "Agent", "Image Generation"
    ];

    public TabService() { }

    public string ActiveTab
    {
        get
        {
            var tabControl = Window.Find<TabControl>("MainTabControl");
            if (tabControl?.SelectedItem is TabItem selected && selected.Header is string header)
                return header.ToString();
            return "Chat";
        }
    }

    public async Task<bool> SwitchTabAsync(string tabName)
    {
        if (string.IsNullOrWhiteSpace(tabName))
            return false;

        var tabControl = Window.Find<TabControl>("MainTabControl");
        if (tabControl == null)
            return false;

        // Find the matching TabItem by header text
        foreach (var child in tabControl.Items)
        {
            if (child is TabItem tab && tab.Header is string header && header.Equals(tabName, StringComparison.Ordinal))
            {
                tabControl.SelectedItem = tab;
                return true;
            }
        }

        return false;
    }

    public IReadOnlyList<string> GetAvailableTabs() => _availableTabs.AsReadOnly();

    public void Dispose() { }
}