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

    private void UpdateActiveTab(string activeTabName)
    {
        // Update styling for all tab TextBlocks to show which is active
        var tabs = new List<TextBlock?>();

        if (ChatTabContent != null)
            tabs.Add(ChatTabContent.Children.OfType<TextBlock>().FirstOrDefault());

        if (ServerTabContent != null)
            tabs.Add(ServerTabContent.Children.OfType<TextBlock>().FirstOrDefault());

        if (ModelsTabContent != null)
            tabs.Add(ModelsTabContent.Children.OfType<TextBlock>().FirstOrDefault());

        if (DevicesTabContent != null)
            tabs.Add(DevicesTabContent.Children.OfType<TextBlock>().FirstOrDefault());

        if (ContextTabContent != null)
            tabs.Add(ContextTabContent.Children.OfType<TextBlock>().FirstOrDefault());


        if (PinguTabContent != null)
            tabs.Add(PinguTabContent.Children.OfType<TextBlock>().FirstOrDefault());

        if (ImageGenTabContent != null)
            tabs.Add(ImageGenTabContent.Children.OfType<TextBlock>().FirstOrDefault());


        foreach (var tb in tabs)
        {
            if (tb == null) continue;

            // Only update the first TextBlock of each tab section (the tab title)
            var parent = tb.Parent as Panel;
            if (parent?.Name != null &&
                new[] { "ChatTabContent", "ServerTabContent", "ModelsTabContent", "DevicesTabContent", "ContextTabContent", "PinguTabContent", "ImageGenTabContent" }
                    .Contains(parent.Name))
            {
                if (activeTabName.Equals(tb.Text, StringComparison.OrdinalIgnoreCase) ||
                    (activeTabName == "ImageGen" && tb.Text?.Equals("Image Generation") == true) ||
                    (activeTabName == "Pingu" && tb.Text?.Equals("Pingu") == true))
                {
                    tb.Foreground = new SolidColorBrush(Color.FromRgb(79, 195, 247)); // AccentBlue
                    tb.FontWeight = FontWeight.SemiBold;
                }
                else
                {
                    tb.Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)); // TextPrimary
                    tb.FontWeight = FontWeight.Normal;
                }
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
