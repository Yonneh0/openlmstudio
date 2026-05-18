// Avalonia Window code-behind — converts WPF-specific types to Avalonia equivalents

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Desktop;

/// <summary>
/// Interaction logic for SettingsWindow.axaml.
/// Provides UI controls for server, model, agent, and data privacy settings.
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();

        // Set title programmatically to avoid XAML entity reference issues with "&" character
        this.Title = "Settings - OpenLMStudio";

        // Subscribe to tab selection events
        ServerTabButton.IsCheckedChanged += OnTabChanged;
        ModelTabButton.IsCheckedChanged += OnTabChanged;
        AgentTabButton.IsCheckedChanged += OnTabChanged;
        
        SaveSettingsBtn.Click += OnSaveSettingsClicked;

        // Set the data privacy panel title from code-behind since auto-formatting converts & back to &
        if (DataPrivacyPanel != null)
        {
            var firstBorder = DataPrivacyPanel.Children.OfType<Border>().FirstOrDefault();
            if (firstBorder?.Child is TextBlock tb)
                tb.Text = "Data Privacy & Storage";
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnTabChanged(object? sender, RoutedEventArgs e)
    {
        // Show/hide panels based on active tab
        if (ServerTabButton.IsChecked == true)
            SetPanelVisibility(ServerSettingsPanel, ModelSettingsPanel);
        else if (ModelTabButton.IsChecked == true)
            SetPanelVisibility(ModelSettingsPanel, ServerSettingsPanel);
        else if (AgentTabButton.IsChecked == true)
            SetPanelVisibility(AgentSettingsPanel, ServerSettingsPanel);

        // Data privacy panel is not exposed via tabs yet — could add a fourth tab later
    }

    private static void SetPanelVisibility(StackPanel activePanel, StackPanel inactivePanel)
    {
        activePanel.IsVisible = true;
        inactivePanel.IsVisible = false;
    }

    private async void OnSaveSettingsClicked(object? sender, RoutedEventArgs e)
    {
        // TODO: Persist settings to configuration file or SQLite database
        this.Close();
    }
}