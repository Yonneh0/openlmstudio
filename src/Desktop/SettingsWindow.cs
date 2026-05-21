using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Desktop;

/// <summary>
/// Settings dialog with Server, Model, Agent, Plugin, and Data Privacy tabs.
/// Persists settings to the appdata directory as JSON.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly ILogger<SettingsWindow> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly AppDataDirectoryResolver _resolver;

    public SettingsWindow(ILogger<SettingsWindow> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _resolver = serviceProvider.GetService<AppDataDirectoryResolver>() ?? new AppDataDirectoryResolver();
        InitializeComponent();
        Loaded += OnWindowLoaded;
        ServerTabButton.IsCheckedChanged += OnSettingsTabChanged;
        ModelTabButton.IsCheckedChanged += OnSettingsTabChanged;
        AgentTabButton.IsCheckedChanged += OnSettingsTabChanged;
        PluginTabButton.IsCheckedChanged += OnSettingsTabChanged;
        SaveSettingsBtn.Click += OnSaveSettingsClicked;
        GetControl<Control>("Cancel")?.AddHandler(Control.ClickEvent, OnCancelClicked);
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close();

    private void OnSettingsTabChanged(object? sender, RoutedEventArgs e)
    {
        ServerSettingsPanel.IsVisible = ServerTabButton.IsChecked == true;
        ModelSettingsPanel.IsVisible = ModelTabButton.IsChecked == true;
        AgentSettingsPanel.IsVisible = AgentTabButton.IsChecked == true;
        PluginSettingsPanel.IsVisible = PluginTabButton.IsChecked == true;
        DataPrivacyPanel.IsVisible = PluginTabButton.IsChecked == false && ModelTabButton.IsChecked == false && AgentTabButton.IsChecked == false;
    }

    private async void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        await LoadSettings();
    }

    private async Task LoadSettings()
    {
        try
        {
            var settingsPath = GetSettingsFilePath();
            if (File.Exists(settingsPath))
            {
                var json = await File.ReadAllTextAsync(settingsPath);
                var settings = System.Text.Json.JsonSerializer.Deserialize<SettingsFile>(json);
                if (settings != null)
                {
                    // Server settings
                    if (settings.Server?.Port.HasValue == true) PortSettingInput.Text = settings.Server.Port.Value.ToString();
                    if (settings.Server?.HttpsEnabled.HasValue == true) HttpsEnabledCheckbox.IsChecked = settings.Server.HttpsEnabled.Value;
                    if (!string.IsNullOrEmpty(settings.Server?.ApiKey)) ApiKeySettingInput.Text = settings.Server.ApiKey;
                    if (settings.Server?.RateLimit.HasValue == true) RateLimitSettingInput.Text = settings.Server.RateLimit.Value.ToString();
                    if (settings.Server?.DefaultContextLength.HasValue == true) ContextLengthSettingInput.Text = settings.Server.DefaultContextLength.Value.ToString();

                    // Model settings
                    if (!string.IsNullOrEmpty(settings.Model?.DefaultModelId)) DefaultModelSelector.Text = settings.Model.DefaultModelId;
                    if (settings.Model?.GpuOffloadLayers.HasValue == true) GpuOffloadSlider.Value = settings.Model.GpuOffloadLayers.Value;
                    if (!string.IsNullOrEmpty(settings.Model?.ContextLengthOverride)) ContextLengthOverrideInput.Text = settings.Model.ContextLengthOverride;
                    if (settings.Model?.Temperature.HasValue == true) TemperatureSlider.Value = settings.Model.Temperature.Value;
                    if (settings.Model?.TopP.HasValue == true) TopPValueSlider.Value = settings.Model.TopP.Value;
                    if (settings.Model?.MaxTokens.HasValue == true) MaxTokensSettingInput.Text = settings.Model.MaxTokens.Value.ToString();

                    if (settings.ImageGen?.DefaultResolution != null)
                    {
                        var parts = settings.ImageGen.DefaultResolution.Split('x');
                        var resText = parts.Length == 2 ? $"{parts[0]}x{parts[1]}" : "512x512";
                        ImageResSettingInput.Text = resText;
                    }
                    if (settings.ImageGen?.DefaultSteps.HasValue == true) ImageStepsSettingSlider.Value = settings.ImageGen.DefaultSteps.Value;
                    if (settings.ImageGen?.DefaultCfgScale.HasValue == true) ImageCfgSettingSlider.Value = settings.ImageGen.DefaultCfgScale.Value;

                    if (!string.IsNullOrEmpty(settings.Compression?.Strategy))
                    {
                        var idx = settings.Compression.Strategy switch
                        {
                            "None" => 0,
                            "Light" => 1,
                            "Medium" => 2,
                            "Aggressive" => 3,
                            _ => 2
                        };
                        CompressionStrategyInput.SelectedIndex = idx;
                    }
                    if (settings.Compression?.TokenBudget.HasValue == true) TokenBudgetInput.Text = settings.Compression.TokenBudget.Value.ToString();

                    // Agent settings
                    if (settings.Agent?.MaxIterations.HasValue == true) AgentIterationLimitInput.Text = settings.Agent.MaxIterations.Value.ToString();
                    if (settings.Agent?.AutoCommitThresholdKb.HasValue == true) AgentAutoCommitSizeInput.Text = settings.Agent.AutoCommitThresholdKb.Value.ToString();
                    if (settings.Agent?.RequirePlanApproval.HasValue == true) AgentPlanApprovalCheckbox.IsChecked = settings.Agent.RequirePlanApproval.Value;

                    // Plugin settings
                    if (!string.IsNullOrEmpty(settings.Plugin?.RegistryUrl)) PluginRegistryUrlInput.Text = settings.Plugin.RegistryUrl;
                    if (settings.Plugin?.AutoUpdateCheckIntervalMin.HasValue == true) PluginUpdateIntervalInput.Text = settings.Plugin.AutoUpdateCheckIntervalMin.Value.ToString();
                    if (!string.IsNullOrEmpty(settings.Plugin?.SandboxPolicy))
                    {
                        var policyIdx = settings.Plugin.SandboxPolicy switch
                        {
                            "Strict" => 0,
                            "Restricted" => 1,
                            "Full" => 2,
                            _ => 0
                        };
                        PluginSandboxPolicyInput.SelectedIndex = policyIdx;
                    }
                    if (settings.Mcp?.TimeoutSec.HasValue == true) McptimeoutInput.Text = settings.Mcp.TimeoutSec.Value.ToString();
                    if (settings.Mcp?.MaxToolsPerServer.HasValue == true) MaxToolsPerServerInput.Text = settings.Mcp.MaxToolsPerServer.Value.ToString();

                    // Data privacy
                    if (settings.Privacy?.EncryptionEnabled.HasValue == true) EncryptionEnabledCheckbox.IsChecked = settings.Privacy.EncryptionEnabled.Value;
                    if (!string.IsNullOrEmpty(settings.Privacy?.ExportFormat))
                    {
                        var fmtIdx = settings.Privacy.ExportFormat switch
                        {
                            "JSON" => 0,
                            "Markdown" => 1,
                            "HTML" => 2,
                            _ => 0
                        };
                        ExportFormatInput.SelectedIndex = fmtIdx;
                    }
                    if (settings.Cache?.LruTimeoutDays.HasValue == true) CacheTimeoutInput.Text = settings.Cache.LruTimeoutDays.Value.ToString();
                    if (settings.Cache?.AutoCleanupOrphaned.HasValue == true) AutoCleanupCheckbox.IsChecked = settings.Cache.AutoCleanupOrphaned.Value;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings from disk");
        }
    }

    private async void OnSaveSettingsClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var settings = new SettingsFile
            {
                Server = new ServerSettings
                {
                    Port = int.TryParse(PortSettingInput.Text, out var p) ? p : 8080,
                    HttpsEnabled = HttpsEnabledCheckbox.IsChecked == true,
                    ApiKey = ApiKeySettingInput.Text,
                    RateLimit = int.TryParse(RateLimitSettingInput.Text, out var r) ? r : 60,
                    DefaultContextLength = int.TryParse(ContextLengthSettingInput.Text, out var c) ? c : 4096
                },
                Model = new ModelSettings
                {
                    DefaultModelId = DefaultModelSelector.Text,
                    GpuOffloadLayers = GpuOffloadSlider.Value >= 0 ? (int?)GpuOffloadSlider.Value : null,
                    ContextLengthOverride = ContextLengthOverrideInput.Text,
                    Temperature = (float?)TemperatureSlider.Value,
                    TopP = (float?)TopPValueSlider.Value,
                    MaxTokens = int.TryParse(MaxTokensSettingInput.Text, out var mt) ? mt : 4096
                },
                ImageGen = new ImageGenSettings
                {
                    DefaultResolution = ImageResSettingInput.Text,
                    DefaultSteps = (int)ImageStepsSettingSlider.Value,
                    DefaultCfgScale = (float)ImageCfgSettingSlider.Value
                },
                Compression = new CompressionSettings
                {
                    Strategy = GetSelectedText(CompressionStrategyInput),
                    TokenBudget = int.TryParse(TokenBudgetInput.Text, out var tb) ? tb : 4096
                },
                Agent = new AgentSettings
                {
                    MaxIterations = int.TryParse(AgentIterationLimitInput.Text, out var mi) ? mi : 50,
                    AutoCommitThresholdKb = int.TryParse(AgentAutoCommitSizeInput.Text, out var ac) ? ac : 1024,
                    RequirePlanApproval = AgentPlanApprovalCheckbox.IsChecked == true
                },
                Plugin = new PluginSettings
                {
                    RegistryUrl = PluginRegistryUrlInput.Text,
                    AutoUpdateCheckIntervalMin = int.TryParse(PluginUpdateIntervalInput.Text, out var pui) ? pui : 60,
                    SandboxPolicy = GetSelectedText(PluginSandboxPolicyInput)
                },
                Mcp = new McpSettings
                {
                    TimeoutSec = int.TryParse(McptimeoutInput.Text, out var mt2) ? mt2 : 30,
                    MaxToolsPerServer = int.TryParse(MaxToolsPerServerInput.Text, out var mts) ? mts : 50
                },
                Privacy = new PrivacySettings
                {
                    EncryptionEnabled = EncryptionEnabledCheckbox.IsChecked == true,
                    ExportFormat = GetSelectedText(ExportFormatInput),
                    LruTimeoutDays = int.TryParse(CacheTimeoutInput.Text, out var ct) ? ct : 30,
                    AutoCleanupOrphaned = AutoCleanupCheckbox.IsChecked == true
                }
            };

            var settingsPath = GetSettingsFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            await File.WriteAllTextAsync(settingsPath, System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            _logger.LogInformation("Settings saved to {Path}", settingsPath);

            Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            ShowError($"Failed to save settings: {ex.Message}");
        }
    }

    private static string GetSelectedText(ComboBox? comboBox)
    {
        if (comboBox?.SelectedItem is TextBlock tb)
            return tb.Text ?? string.Empty;
        return string.Empty;
    }

    private string GetSettingsFilePath()
    {
        var metaDir = Path.Combine(_resolver.GetAppDataDirectory(), "metadata");
        return Path.Combine(metaDir, "settings.json");
    }

    private void ShowError(string message)
    {
        try
        {
            var errorWin = new Window
            {
                Title = "OpenLMStudio - Error",
                Width = 400,
                Height = 200,
                Content = new Border
                {
                    Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(255, 37, 37, 41)),
                    Child = new TextBlock
                    {
                        Text = message,
                        Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(255, 255, 255, 255)),
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
            System.Diagnostics.Debug.WriteLine($"Settings error: {message}");
        }
    }
}

// ---- Settings serialization models ----

internal record SettingsFile(
    ServerSettings? Server = null,
    ModelSettings? Model = null,
    ImageGenSettings? ImageGen = null,
    CompressionSettings? Compression = null,
    AgentSettings? Agent = null,
    PluginSettings? Plugin = null,
    McpSettings? Mcp = null,
    PrivacySettings? Privacy = null,
    CacheSettings? Cache = null);

internal record ServerSettings(
    int? Port = null,
    bool? HttpsEnabled = null,
    string? ApiKey = null,
    int? RateLimit = null,
    int? DefaultContextLength = null);

internal record ModelSettings(
    string? DefaultModelId = null,
    int? GpuOffloadLayers = null,
    string? ContextLengthOverride = null,
    float? Temperature = null,
    float? TopP = null,
    int? MaxTokens = null);

internal record ImageGenSettings(
    string? DefaultResolution = "512x512",
    int? DefaultSteps = null,
    float? DefaultCfgScale = null);

internal record CompressionSettings(
    string? Strategy = "Medium",
    int? TokenBudget = null);

internal record AgentSettings(
    int? MaxIterations = null,
    int? AutoCommitThresholdKb = null,
    bool? RequirePlanApproval = null);

internal record PluginSettings(
    string? RegistryUrl = null,
    int? AutoUpdateCheckIntervalMin = null,
    string? SandboxPolicy = null);

internal record McpSettings(
    int? TimeoutSec = null,
    int? MaxToolsPerServer = null);

internal record PrivacySettings(
    bool? EncryptionEnabled = null,
    string? ExportFormat = null,
    int? LruTimeoutDays = null,
    bool? AutoCleanupOrphaned = null);

internal record CacheSettings(
    int? LruTimeoutDays = null,
    bool? AutoCleanupOrphaned = null);