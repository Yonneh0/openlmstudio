using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.Logging;

namespace OpenLMStudio.Desktop;

/// <summary>
/// Interaction logic for SettingsWindow.xaml.
/// Provides settings management across Server, Model, and Agent tabs with save/cancel functionality.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly ILogger<SettingsWindow>? _logger;

    public SettingsWindow(ILogger<SettingsWindow>? logger = null)
    {
        InitializeComponent();
        _logger = logger;

        // Initialize tab selection (Server tab active by default)
        ServerTabButton.IsChecked = true;

        // Wire up button handlers
        SaveSettingsBtn.Click += OnSaveClicked;

        // Find and wire Cancel button
        if (FindName("Cancel") is Button cancelButton)
            cancelButton.Click += OnCancelClicked;
    }

    private void OnTabChanged(object sender, RoutedEventArgs e)
    {
        var tabButton = (ToggleButton)sender;
        var activeTab = tabButton.Tag as string ?? "Server";

        // Hide all panels first
        ServerSettingsPanel.Visibility = Visibility.Collapsed;
        ModelSettingsPanel.Visibility = Visibility.Collapsed;
        AgentSettingsPanel.Visibility = Visibility.Collapsed;
        DataPrivacyPanel.Visibility = Visibility.Collapsed;

        // Show the selected panel
        switch (activeTab)
        {
            case "Server":
                ServerSettingsPanel.Visibility = Visibility.Visible;
                break;
            case "Model":
                ModelSettingsPanel.Visibility = Visibility.Visible;
                break;
            case "Agent":
                AgentSettingsPanel.Visibility = Visibility.Visible;
                DataPrivacyPanel.Visibility = Visibility.Visible; // Also show data privacy under agent tab
                break;
        }

        // Update active tab styling across all tabs
        UpdateTabStyling(tabButton);
    }

    private void UpdateTabStyling(ToggleButton activeButton)
    {
        var activeStyle = (Style)FindResource("SettingsTabButtonActive");
        var inactiveStyle = (Style)FindResource("SettingsTabButton");

        // Apply to all tab buttons
        foreach (var btn in new[] { ServerTabButton, ModelTabButton, AgentTabButton })
        {
            if (btn == activeButton)
                btn.Style = activeStyle;
            else
                btn.Style = inactiveStyle;
        }
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            _logger?.LogInformation("Saving settings");

            // Save server settings
            var portText = PortSettingInput?.Text;
            if (!string.IsNullOrEmpty(portText))
            {
                var port = int.TryParse(portText, out var parsedPort) ? parsedPort : 8080;
                _logger?.LogInformation("Server port: {Port}", port);
            }

            // Save agent settings
            var iterationLimit = AgentIterationLimitInput?.Text;
            if (!string.IsNullOrEmpty(iterationLimit))
            {
                var limit = int.TryParse(iterationLimit, out var parsed) ? parsed : 50;
                _logger?.LogInformation("Agent max iterations: {Limit}", limit);
            }

            Close(); // Close on save success
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving settings");
            MessageBox.Show($"Failed to save settings: {ex.Message}", "OpenLMStudio", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }
}