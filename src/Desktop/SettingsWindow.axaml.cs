// Avalonia Window code-behind — converts WPF-specific types to Avalonia equivalents

using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Logging;

namespace OpenLMStudio.Desktop;

/// <summary>
/// Interaction logic for SettingsWindow.axaml.
/// Provides UI controls for server, model, agent, and data privacy settings.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly ILogger<SettingsWindow>? _logger;

    /// <summary>Path to the settings.json file used to persist settings.</summary>
    private static string SettingsPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

    public SettingsWindow(ILogger<SettingsWindow>? logger = null)
    {
        InitializeComponent();

        // Set title programmatically to avoid XAML entity reference issues with "&" character
        this.Title = "Settings - OpenLMStudio";

        _logger = logger;

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

        // Load persisted settings
        LoadSettings();
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

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                _logger?.LogDebug("No settings file found at {Path}", SettingsPath);
                return;
            }

            var settings = System.Text.Json.JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(SettingsPath));
            if (settings == null) return;

            // Apply server settings
            if (PortSettingInput != null)
                PortSettingInput.Text = settings.ServerPort.ToString();
            if (HttpsEnabledCheckbox != null)
                HttpsEnabledCheckbox.IsChecked = settings.EnableHttps;
            if (ApiKeySettingInput != null)
                ApiKeySettingInput.Text = settings.ApiKey ?? string.Empty;
            if (RateLimitSettingInput != null)
                RateLimitSettingInput.Text = settings.RateLimit.ToString();
            if (ContextLengthSettingInput != null)
                ContextLengthSettingInput.Text = settings.ContextLength.ToString();

            // Apply model settings
            if (GpuOffloadSlider != null)
                GpuOffloadSlider.Value = settings.GpuOffloadLayers;
            if (TemperatureSlider != null)
                TemperatureSlider.Value = settings.Temperature;
            if (TopPValueSlider != null)
                TopPValueSlider.Value = settings.TopP;
            if (MaxTokensSettingInput != null)
                MaxTokensSettingInput.Text = settings.MaxTokens.ToString();
            if (ContextLengthOverrideInput != null)
                ContextLengthOverrideInput.Text = settings.ContextLengthOverride ?? string.Empty;

            // Apply agent settings
            if (AgentIterationLimitInput != null)
                AgentIterationLimitInput.Text = settings.MaxAgentIterations.ToString();
            if (AgentAutoCommitSizeInput != null)
                AgentAutoCommitSizeInput.Text = settings.AgentAutoCommitSizeKB.ToString();
            if (AgentPlanApprovalCheckbox != null)
                AgentPlanApprovalCheckbox.IsChecked = settings.RequirePlanApproval;

            // Apply data privacy settings
            if (EncryptionEnabledCheckbox != null)
                EncryptionEnabledCheckbox.IsChecked = settings.EnableEncryption;
            if (CacheTimeoutInput != null)
                CacheTimeoutInput.Text = settings.ModelCacheTimeoutDays.ToString();
            if (AutoCleanupCheckbox != null)
                AutoCleanupCheckbox.IsChecked = settings.AutoCleanupOrphanedModels;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load settings from {Path}", SettingsPath);
        }
    }

    private async void OnSaveSettingsClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var settings = new SettingsData();

            // Save server settings
            if (PortSettingInput?.Text != null && int.TryParse(PortSettingInput.Text, out var port))
                settings.ServerPort = port;
            settings.EnableHttps = HttpsEnabledCheckbox?.IsChecked == true;

            if (ApiKeySettingInput?.Text != null)
                settings.ApiKey = ApiKeySettingInput.Text.Trim();

            if (RateLimitSettingInput?.Text != null && int.TryParse(RateLimitSettingInput.Text, out var rateLimit))
                settings.RateLimit = rateLimit;

            if (ContextLengthSettingInput?.Text != null && int.TryParse(ContextLengthSettingInput.Text, out var ctxLen))
                settings.ContextLength = ctxLen;

            // Save model settings
            settings.GpuOffloadLayers = (int)(GpuOffloadSlider?.Value ?? -1);
            settings.Temperature = TemperatureSlider?.Value ?? 0.7;
            settings.TopP = TopPValueSlider?.Value ?? 1.0;

            if (MaxTokensSettingInput?.Text != null && int.TryParse(MaxTokensSettingInput.Text, out var maxTokens))
                settings.MaxTokens = maxTokens;

            if (ContextLengthOverrideInput?.Text != null)
                settings.ContextLengthOverride = ContextLengthOverrideInput.Text.Trim();

            // Save agent settings
            if (AgentIterationLimitInput?.Text != null && int.TryParse(AgentIterationLimitInput.Text, out var agentIter))
                settings.MaxAgentIterations = agentIter;

            if (AgentAutoCommitSizeInput?.Text != null && int.TryParse(AgentAutoCommitSizeInput.Text, out var autoCommitSize))
                settings.AgentAutoCommitSizeKB = autoCommitSize;

            settings.RequirePlanApproval = AgentPlanApprovalCheckbox?.IsChecked == true;

            // Save data privacy settings
            settings.EnableEncryption = EncryptionEnabledCheckbox?.IsChecked == true;

            if (CacheTimeoutInput?.Text != null && int.TryParse(CacheTimeoutInput.Text, out var cacheTimeout))
                settings.ModelCacheTimeoutDays = cacheTimeout;

            settings.AutoCleanupOrphanedModels = AutoCleanupCheckbox?.IsChecked == true;

            // Write settings file
            var json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(SettingsPath, json);

            _logger?.LogInformation("Settings saved to {Path}", SettingsPath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save settings");
            ShowError($"Failed to save settings: {ex.Message}");
        }

        this.Close(null);
    }

    /// <summary>
    /// Shows an error dialog.
    /// </summary>
    private void ShowError(string message)
    {
        try
        {
            var errorWin = new Window
            {
                Title = "OpenLMStudio - Error",
                Width = 400,
                Height = 250,
                Content = new Border
                {
                    Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(37, 37, 41)),
                    Child = new TextBlock
                    {
                        Text = message,
                        Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 255, 255)),
                        Padding = new Thickness(20),
                        FontSize = 14,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    }
                }
            };
            errorWin.ShowDialog(this);
        }
        catch
        {
            System.Diagnostics.Debug.WriteLine($"Error: {message}");
        }
    }

    /// <summary>
    /// Data class for settings serialization.
    /// </summary>
    private class SettingsData
    {
        public int ServerPort { get; set; } = 8080;
        public bool EnableHttps { get; set; }
        public string? ApiKey { get; set; }
        public int RateLimit { get; set; } = 60;
        public int ContextLength { get; set; } = 4096;
        public int GpuOffloadLayers { get; set; } = -1;
        public double Temperature { get; set; } = 0.7;
        public double TopP { get; set; } = 1.0;
        public int MaxTokens { get; set; } = 4096;
        public string? ContextLengthOverride { get; set; }
        public int MaxAgentIterations { get; set; } = 50;
        public int AgentAutoCommitSizeKB { get; set; } = 1024;
        public bool RequirePlanApproval { get; set; }
        public bool EnableEncryption { get; set; }
        public int ModelCacheTimeoutDays { get; set; } = 30;
        public bool AutoCleanupOrphanedModels { get; set; }
    }
}