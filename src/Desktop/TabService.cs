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
        "Chat", "Server", "Models", "Devices", "Context", "Image Generation"
    ];

    public TabService() { }

    public string ActiveTab
    {
        get
        {
            // Try MainTabControl first
            var tabControl = Window.Find<TabControl>("MainTabControl");
            if (tabControl?.SelectedItem is TabItem selected && selected.Header is string header)
                return header.ToString();

            // Fall back to _activeTab field from MainWindow
            try
            {
                var activeTabField = typeof(MainWindow).GetField("_activeTab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (activeTabField?.GetValue(Window) is string activeTab)
                    return activeTab;
            }
            catch { /* Ignore reflection errors */ }

            return "Chat";
        }
    }

    /// <summary>
    /// Switches the active tab using the WrapPanel + ToggleButton approach in MainWindow.
    /// </summary>
    private bool SwitchTabViaToggleButton(string tabName)
    {
        var tabs = new (string tabName, string buttonName)[]
        {
            ("Chat", "ChatTab"),
            ("Server", "ServerTab"),
            ("Models", "ModelsTab"),
            ("Devices", "DevicesTab"),
            ("Context", "ContextTab"),
            ("Pingu", "PinguTab"),
            ("ImageGen", "ImageGenTab"),
            ("Image Generation", "ImageGenTab"),
        };

        var target = tabs.FirstOrDefault(t => t.tabName.Equals(tabName, System.StringComparison.Ordinal));
        if (target.buttonName == null)
            return false;

        var button = Window.Find<ToggleButton>(target.buttonName);
        if (button != null)
        {
            button.IsChecked = true;
            return true;
        }

        return false;
    }

    public async Task<bool> SwitchTabAsync(string tabName)
    {
        if (string.IsNullOrWhiteSpace(tabName))
            return false;

        // Try MainTabControl first
        var tabControl = Window.Find<TabControl>("MainTabControl");
        if (tabControl != null)
        {
            foreach (var child in tabControl.Items)
            {
                if (child is TabItem tab && tab.Header is string header && header.Equals(tabName, StringComparison.Ordinal))
                {
                    tabControl.SelectedItem = tab;
                    return true;
                }
            }
        }

        // Fall back to ToggleButton approach
        return SwitchTabViaToggleButton(tabName);
    }

    public IReadOnlyList<string> GetAvailableTabs() => _availableTabs.AsReadOnly();

    public void Dispose() { }
}