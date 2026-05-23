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
        SetTabVisibility(AgentTabContent, tabName == "Agent");
        SetTabVisibility(GamesTabContent, tabName == "Games");
        SetTabVisibility(PinguTabContent, tabName == "Pingu");
        SetTabVisibility(ImageGenTabContent, tabName == "ImageGen");
        SetTabVisibility(TasksTabContent, tabName == "Tasks");
        SetTabVisibility(AnalysisTabContent, tabName == "Analysis");

        // Update ToggleButton checked state
        SetToggleButtonChecked(ChatTab, tabName == "Chat");
        SetToggleButtonChecked(ServerTab, tabName == "Server");
        SetToggleButtonChecked(ModelsTab, tabName == "Models");
        SetToggleButtonChecked(DevicesTab, tabName == "Devices");
        SetToggleButtonChecked(ContextTab, tabName == "Context");
        SetToggleButtonChecked(TasksTab, tabName == "Tasks");
        SetToggleButtonChecked(AgentTab, tabName == "Agent");
        SetToggleButtonChecked(GamesTab, tabName == "Games");
        SetToggleButtonChecked(PinguTab, tabName == "Pingu");
        SetToggleButtonChecked(ImageGenTab, tabName == "ImageGen");
        SetToggleButtonChecked(AnalysisTab, tabName == "Analysis");

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
            case "Analysis":
                _ = RefreshAnalysisContextAsync();
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
        var tabs = new[] { "Chat", "Server", "Models", "Devices", "Context", "Agent", "Games", "Pingu", "Tasks", "ImageGen", "Analysis" };
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

        if (AgentTabContent != null)
            tabs.Add(AgentTabContent.Children.OfType<TextBlock>().FirstOrDefault());

        if (GamesTabContent != null)
            tabs.Add(GamesTabContent.Children.OfType<TextBlock>().FirstOrDefault());

        if (PinguTabContent != null)
            tabs.Add(PinguTabContent.Children.OfType<TextBlock>().FirstOrDefault());

        if (ImageGenTabContent != null)
            tabs.Add(ImageGenTabContent.Children.OfType<TextBlock>().FirstOrDefault());

        if (TasksTabContent != null)
            tabs.Add(TasksTabContent.Children.OfType<TextBlock>().FirstOrDefault());

        if (AnalysisTabContent != null)
            tabs.Add(AnalysisTabContent.Children.OfType<TextBlock>().FirstOrDefault());

        foreach (var tb in tabs)
        {
            if (tb == null) continue;

            // Only update the first TextBlock of each tab section (the tab title)
            var parent = tb.Parent as Panel;
            if (parent?.Name != null &&
                new[] { "ChatTabContent", "ServerTabContent", "ModelsTabContent", "DevicesTabContent", "ContextTabContent", "AgentTabContent", "GamesTabContent", "PinguTabContent", "ImageGenTabContent", "TasksTabContent", "AnalysisTabContent" }
                    .Contains(parent.Name))
            {
                if (activeTabName.Equals(tb.Text, StringComparison.OrdinalIgnoreCase) ||
                    (activeTabName == "ImageGen" && tb.Text?.Equals("Image Generation") == true) ||
                    (activeTabName == "Games" && tb.Text?.Equals("Games") == true) ||
                    (activeTabName == "Pingu" && tb.Text?.Equals("Pingu") == true) ||
                    (activeTabName == "Analysis" && tb.Text?.Equals("AI Analysis") == true))
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

    private void UpdateRightSidebarTab(string activeTab)
    {
        // Right sidebar is now a simple placeholder — no-op for now.
        // Left panel tabs (Chats, Server, Models, Devices, Context, Agent, Games, Pingu, ImageGen, Tasks, Analysis)
        // handle all content display independently.
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

    private void OnAnalysisTabPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e) => ShowTab("Analysis");

    /// <summary>
    /// Attaches Click event handlers to the Tab TextBlocks so users can switch tabs by clicking.
    /// </summary>
    private void AttachTabClickHandlers()
    {
        // Each tab's title TextBlock is inside a StackPanel — attach click to that panel instead for better hit target
        var tabPanels = new[] { ChatTabContent, ServerTabContent, ModelsTabContent, DevicesTabContent, ContextTabContent, AgentTabContent, GamesTabContent, PinguTabContent, ImageGenTabContent, TasksTabContent, AnalysisTabContent };
        foreach (var tab in tabPanels)
        {
            if (tab == null) continue;

            // Make the entire StackPanel clickable by attaching a Click handler to its first element
            var child = tab.Children.OfType<Control>().FirstOrDefault();
            if (child != null && !string.IsNullOrEmpty(tab.Name))
            {
                try
                {
                    switch (tab.Name)
                    {
                        case "ChatTabContent":
                            child.PointerPressed += (_, _) => ShowTab("Chat"); break;
                        case "ServerTabContent":
                            child.PointerPressed += (_, _) => ShowTab("Server"); break;
                        case "ModelsTabContent":
                            child.PointerPressed += (_, _) => ShowTab("Models"); break;
                        case "DevicesTabContent":
                            child.PointerPressed += (_, _) => ShowTab("Devices"); break;
                        case "ContextTabContent":
                            child.PointerPressed += (_, _) => { UpdateRightSidebarTab("Context"); ShowTab("Context"); }; break;
                        case "AgentTabContent":
                            child.PointerPressed += (_, _) => ShowTab("Agent"); break;
                        case "GamesTabContent":
                            child.PointerPressed += (_, _) => ShowTab("Games"); break;
                        case "PinguTabContent":
                            child.PointerPressed += (_, _) => ShowTab("Pingu"); break;
                        case "ImageGenTabContent":
                            child.PointerPressed += (_, _) => ShowTab("ImageGen"); break;
                        case "TasksTabContent":
                            child.PointerPressed += (_, _) => ShowTab("Tasks"); break;
                        case "AnalysisTabContent":
                            child.PointerPressed += (_, _) => ShowTab("Analysis"); break;
                    }
                }
                catch { /* Ignore errors on individual tab attaches */ }
            }
        }

        // Also attach click handlers directly to the TabControl buttons in XAML for reliability — use lambda instead of RoutedEventHandler
        if (ChatTabContent?.Children.OfType<Control>().FirstOrDefault() is Control chatClickTarget)
            chatClickTarget.PointerPressed += (_, _) => ShowTab("Chat");

        var serverChild = ServerTabContent?.Children.OfType<Control>().FirstOrDefault();
        serverChild?.AddHandler(Control.PointerPressedEvent, (_, _) => ShowTab("Server"));

        var modelsChild = ModelsTabContent?.Children.OfType<Control>().FirstOrDefault();
        modelsChild?.AddHandler(Control.PointerPressedEvent, (_, _) => ShowTab("Models"));

        var devicesChild = DevicesTabContent?.Children.OfType<Control>().FirstOrDefault();
        devicesChild?.AddHandler(Control.PointerPressedEvent, (_, _) => ShowTab("Devices"));

        var tasksChild = TasksTabContent?.Children.OfType<Control>().FirstOrDefault();
        tasksChild?.AddHandler(Control.PointerPressedEvent, (_, _) => ShowTab("Tasks"));

        var gamesChild = GamesTabContent?.Children.OfType<Control>().FirstOrDefault();
        gamesChild?.AddHandler(Control.PointerPressedEvent, (_, _) => ShowTab("Games"));

        var pinguChild = PinguTabContent?.Children.OfType<Control>().FirstOrDefault();
        pinguChild?.AddHandler(Control.PointerPressedEvent, (_, _) => ShowTab("Pingu"));

        var analysisChild = AnalysisTabContent?.Children.OfType<Control>().FirstOrDefault();
        analysisChild?.AddHandler(Control.PointerPressedEvent, (_, _) => ShowTab("Analysis"));

        var imageGenChild = ImageGenTabContent?.Children.OfType<Control>().FirstOrDefault();
        imageGenChild?.AddHandler(Control.PointerPressedEvent, (_, _) => ShowTab("ImageGen"));

        var agentChild = AgentTabContent?.Children.OfType<Control>().FirstOrDefault();
        agentChild?.AddHandler(Control.PointerPressedEvent, (_, _) => ShowTab("Agent"));

        // Attach right sidebar tab button Click handlers (no-op — right panel is a placeholder)
    }
}
