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

        // Set SelectedItem on the TabControl — the ContentPresenter handles showing/hiding content
        switch (tabName)
        {
            case "Chat":
                LeftTabControl.SelectedItem = ChatTabItem;
                UpdateServerStatus();
                break;
            case "Server":
                LeftTabControl.SelectedItem = ServerTabItem;
                UpdateServerStatus();
                break;
            case "Models":
                LeftTabControl.SelectedItem = ModelsTabItem;
                RefreshModelListAsync();
                break;
            case "Devices":
                LeftTabControl.SelectedItem = DevicesTabItem;
                _ = UpdateDeviceStatusAsync();
                break;
            case "Context":
                LeftTabControl.SelectedItem = ContextTabItem;
                _ = RefreshContextBudgetAsync();
                UpdateRightSidebarTab("Context");
                break;
            case "Agent":
                LeftTabControl.SelectedItem = AgentTabItem;
                break;
            case "ImageGen":
                LeftTabControl.SelectedItem = ImageGenTabItem;
                break;
            case "Tasks":
                LeftTabControl.SelectedItem = TasksTabItem;
                break;
        }

        // Update active tab styling
        UpdateActiveTab(tabName);
    }

    /// <summary>
    /// Switches to the tab at the given zero-based index.
    /// </summary>
    public void SwitchToTab(int index)
    {
        var tabs = new[] { "Chat", "Server", "Models", "Devices", "Context", "Agent", "Tasks", "ImageGen" };
        if (index >= 0 && index < tabs.Length)
            ShowTab(tabs[index]);
    }

    /// <summary>
    /// Handles TabControl selection changes — updates styling and right sidebar.
    /// </summary>
    private void OnLeftTabControlSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Guard: if TabItems are null, we're still in EndInit — skip handling
        if (LeftTabControl.SelectedItem is not TabItem selectedItem)
            return;

        if (ChatTabItem == null || ServerTabItem == null || ModelsTabItem == null ||
            DevicesTabItem == null || ContextTabItem == null || AgentTabItem == null ||
            ImageGenTabItem == null || TasksTabItem == null)
            return;

        // Map TabItem to tab name based on which item is selected
        if (selectedItem == ChatTabItem)
            _activeTab = "Chat";
        else if (selectedItem == ServerTabItem)
            _activeTab = "Server";
        else if (selectedItem == ModelsTabItem)
            _activeTab = "Models";
        else if (selectedItem == DevicesTabItem)
            _activeTab = "Devices";
        else if (selectedItem == ContextTabItem)
            _activeTab = "Context";
        else if (selectedItem == AgentTabItem)
            _activeTab = "Agent";
        else if (selectedItem == ImageGenTabItem)
            _activeTab = "ImageGen";
        else if (selectedItem == TasksTabItem)
            _activeTab = "Tasks";

        UpdateActiveTab(_activeTab);
        UpdateRightSidebarTab(_activeTab);
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

        if (ImageGenTabContent != null)
            tabs.Add(ImageGenTabContent.Children.OfType<TextBlock>().FirstOrDefault());

        if (TasksTabContent != null)
            tabs.Add(TasksTabContent.Children.OfType<TextBlock>().FirstOrDefault());

        foreach (var tb in tabs)
        {
            if (tb == null) continue;

            // Only update the first TextBlock of each tab section (the tab title)
            var parent = tb.Parent as Panel;
            if (parent?.Name != null &&
                new[] { "ChatTabContent", "ServerTabContent", "ModelsTabContent", "DevicesTabContent", "ContextTabContent", "AgentTabContent", "ImageGenTabContent", "TasksTabContent" }
                    .Contains(parent.Name))
            {
                if (activeTabName.Equals(tb.Text, StringComparison.OrdinalIgnoreCase) ||
                    (activeTabName == "ImageGen" && tb.Text?.Equals("Image Generation") == true))
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
        // Show/hide right sidebar tab content panels
        SetPanelVisibility(RightContextContent, false);
        SetPanelVisibility(RightServerContent, false);
        SetPanelVisibility(RightDevicesContent, false);
        SetPanelVisibility(RightAnalysisContent, false);
        SetPanelVisibility(RightGamesContent, false);
        SetPanelVisibility(RightPinguContent, false);

        SetPanelVisibility(RightContextContent, activeTab == "Context");
        SetPanelVisibility(RightServerContent, activeTab == "Server");
        SetPanelVisibility(RightDevicesContent, activeTab == "Devices");
        SetPanelVisibility(RightAnalysisContent, activeTab == "Analysis");
        SetPanelVisibility(RightGamesContent, activeTab == "Games");
        SetPanelVisibility(RightPinguContent, activeTab == "Pingu");

        // Update tab button states
        if (RightContextTabButton != null) RightContextTabButton.IsChecked = activeTab == "Context";
        if (RightServerTabButton != null) RightServerTabButton.IsChecked = activeTab == "Server";
        if (RightDevicesTabButton != null) RightDevicesTabButton.IsChecked = activeTab == "Devices";
        if (RightAnalysisTabButton != null) RightAnalysisTabButton.IsChecked = activeTab == "Analysis";
        if (RightGamesTabButton != null) RightGamesTabButton.IsChecked = activeTab == "Games";
        if (RightPinguTabButton != null) RightPinguTabButton.IsChecked = activeTab == "Pingu";

        // Update context budget when switching to context tab
        if (activeTab == "Context")
            _ = RefreshContextBudgetAsync();

        // Update analysis data when switching to analysis tab
        if (activeTab == "Analysis")
            _ = RefreshAnalysisContextAsync();
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
    /// </summary>
    private void AttachTabClickHandlers()
    {
        // Each tab's title TextBlock is inside a StackPanel — attach click to that panel instead for better hit target
        var tabPanels = new[] { ChatTabContent, ServerTabContent, ModelsTabContent, DevicesTabContent, ContextTabContent, AgentTabContent, ImageGenTabContent, TasksTabContent };
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
                        case "ImageGenTabContent":
                            child.PointerPressed += (_, _) => ShowTab("ImageGen"); break;
                        case "TasksTabContent":
                            child.PointerPressed += (_, _) => ShowTab("Tasks"); break;
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

        // Attach right sidebar tab button click handlers
        if (RightContextTabButton != null)
            RightContextTabButton.IsCheckedChanged += (_, _) => UpdateRightSidebarTab(RightContextTabButton.IsChecked == true ? "Context" : _activeTab);

        if (RightServerTabButton != null)
            RightServerTabButton.IsCheckedChanged += (_, _) => UpdateRightSidebarTab(RightServerTabButton.IsChecked == true ? "Server" : _activeTab);

        if (RightDevicesTabButton != null)
            RightDevicesTabButton.IsCheckedChanged += (_, _) => UpdateRightSidebarTab(RightDevicesTabButton.IsChecked == true ? "Devices" : _activeTab);

        if (RightAnalysisTabButton != null)
            RightAnalysisTabButton.IsCheckedChanged += (_, _) => UpdateRightSidebarTab(RightAnalysisTabButton.IsChecked == true ? "Analysis" : _activeTab);

        if (RightGamesTabButton != null)
            RightGamesTabButton.IsCheckedChanged += (_, _) => UpdateRightSidebarTab(RightGamesTabButton.IsChecked == true ? "Games" : _activeTab);

        if (RightPinguTabButton != null)
            RightPinguTabButton.IsCheckedChanged += (_, _) => UpdateRightSidebarTab(RightPinguTabButton.IsChecked == true ? "Pingu" : _activeTab);
    }
}