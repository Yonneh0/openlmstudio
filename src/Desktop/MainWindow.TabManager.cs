// Avalonia Window code-behind — Tab navigation partial class
// Brought to you by Carls' Jr.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Input;

namespace OpenLMStudio.Desktop;

public partial class MainWindow
{
    /// <summary>
    /// Shows the specified tab and hides all others.
    /// </summary>
    public void ShowTab(string tabName)
    {
        _activeTab = tabName;

        // Show the selected tab content, hide others
        SetTabVisibility(ChatTabContent, tabName == "Chat");
        SetTabVisibility(ServerTabContent, tabName == "Server");
        SetTabVisibility(ModelsTabContent, tabName == "Models");
        SetTabVisibility(DevicesTabContent, tabName == "Devices");
        SetTabVisibility(ContextTabContent, tabName == "Context");
        SetTabVisibility(PinguTabContent, tabName == "Pingu");
        SetTabVisibility(ImageGenTabContent, tabName == "ImageGen");

        // Update ToggleButton checked state
        SetToggleButtonChecked(ChatTab, tabName == "Chat");
        SetToggleButtonChecked(ServerTab, tabName == "Server");
        SetToggleButtonChecked(ModelsTab, tabName == "Models");
        SetToggleButtonChecked(DevicesTab, tabName == "Devices");
        SetToggleButtonChecked(ContextTab, tabName == "Context");
        SetToggleButtonChecked(PinguTab, tabName == "Pingu");
        SetToggleButtonChecked(ImageGenTab, tabName == "ImageGen");

        // Update server status
        switch (tabName)
        {
            case "Chat":
            case "Server":
                UpdateServerStatus();
                break;
            case "Models":
                RefreshModelListAsync();
                break;
            case "Devices":
                _ = UpdateDeviceStatusAsync();
                break;
            case "Context":
                _ = RefreshContextBudgetAsync();
                UpdateRightSidebarTab("Context");
                break;
        }

        // Update active tab styling
        UpdateActiveTab(tabName);
    }

    private void SetToggleButtonChecked(ToggleButton? button, bool isChecked)
    {
        if (button != null)
            button.IsChecked = isChecked;
    }

    /// <summary>
    /// Switches to the tab at the given zero-based index.
    /// </summary>
    public void SwitchToTab(int index)
    {
        var tabs = new[] { "Chat", "Server", "Models", "Devices", "Context", "Pingu", "ImageGen" };
        if (index >= 0 && index < tabs.Length)
            ShowTab(tabs[index]);
    }

    /// <summary>
    /// Handles ToggleButton click changes — updates styling and right sidebar.
    /// </summary>
    private void OnLeftTabControlSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // No longer needed since we use ToggleButtons with direct click handlers.
        // Kept for compatibility with any remaining TabControl-based code.
    }

    private void SetTabVisibility(StackPanel? panel, bool visible)
    {
        if (panel != null)
            panel.IsVisible = visible;
    }

    /// <summary>
    /// Updates the styling of tab TextBlocks based on which ToggleButton is currently checked.
    /// Uses the ToggleButton.IsChecked property instead of string comparison for robustness.
    /// </summary>
    private void UpdateActiveTab(string activeTabName)
    {
        // Map of ToggleButton to its corresponding StackPanel (tab content)
        var tabMap = new (ToggleButton Button, StackPanel Panel)[]
        {
            (ChatTab!, ChatTabContent!),
            (ServerTab!, ServerTabContent!),
            (ModelsTab!, ModelsTabContent!),
            (DevicesTab!, DevicesTabContent!),
            (ContextTab!, ContextTabContent!),
            (PinguTab!, PinguTabContent!),
            (ImageGenTab!, ImageGenTabContent!),
        };

        foreach (var (button, panel) in tabMap)
        {
            // Skip if panel is not visible (not the active tab)
            if (!panel.IsVisible)
                continue;

            // Update styling for the first TextBlock of the visible tab section
            var firstTextBlock = panel.Children.OfType<TextBlock>().FirstOrDefault();
            if (firstTextBlock == null)
                continue;

            // Use the ToggleButton's IsChecked property to determine active tab
            // This is more robust than string comparison — it doesn't depend on the TextBlock content
            if (button.IsChecked == true)
            {
                firstTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(79, 195, 247)); // AccentBlue
                firstTextBlock.FontWeight = FontWeight.SemiBold;
            }
            else
            {
                firstTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)); // TextPrimary
                firstTextBlock.FontWeight = FontWeight.Normal;
            }
        }
    }

    private void SetPanelVisibility(StackPanel? panel, bool visible)
    {
        if (panel != null)
            panel.IsVisible = visible;
    }

    // ---- Tab Pointer Pressed Event Handlers ----

    private void OnChatTabPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e) => ShowTab("Chat");

    private void OnServerTabPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e) => ShowTab("Server");

    private void OnModelsTabPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e) => ShowTab("Models");

    private void OnDevicesTabPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e) => ShowTab("Devices");

    private void OnContextTabPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // Also switch right sidebar to Context tab
        UpdateRightSidebarTab("Context");
        ShowTab("Context");
    }

    private void OnImageGenTabPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e) => ShowTab("ImageGen");


    /// <summary>
    /// Attaches Click event handlers to the Tab TextBlocks so users can switch tabs by clicking.
    /// NOTE: This method is now deprecated — left sidebar tabs use ToggleButton.Click handlers via WireUpLeftTabClickHandlers().
    /// Right sidebar tabs use WireUpRightSidebarTabs().
    /// Kept as no-op for backward compatibility.
    /// </summary>
    private void AttachTabClickHandlers()
    {
        // No-op: Tab navigation is now handled by ToggleButton.Click events in WireUpLeftTabClickHandlers()
        // and WireUpRightSidebarTabs(). This method is kept for backward compatibility.
    }
}
