using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Interfaces;

namespace OpenLMStudio.Desktop;

/// <summary>
/// Plugin management dialog with search, install, enable/disable, update, and policy controls.
/// </summary>
public partial class PluginManagementWindow : Window
{
    private readonly ILogger<PluginManagementWindow>? _logger;
    private readonly IPluginRegistry? _pluginRegistry;
    private readonly IServiceProvider? _serviceProvider;
    private readonly List<PluginCardInfo> _allPlugins = new();
    private readonly List<PluginCardInfo> _filteredPlugins = new();

    private record PluginCardInfo(
        PluginDefinition Definition,
        bool Enabled);

    public PluginManagementWindow(ILogger<PluginManagementWindow>? logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;

        // Resolve IPluginRegistry via the infrastructure layer
        try
        {
            _pluginRegistry = serviceProvider.GetService<IPluginRegistry>() ??
                              serviceProvider.GetService<Infrastructure.Services.PluginRegistry>();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to resolve IPluginRegistry");
        }

        InitializeComponent();
        Loaded += OnLoaded;
        PluginSearchBox.TextChanged += OnSearchTextChanged;
        RefreshPluginsBtn.Click += OnRefreshPlugins;
        InstallPluginBtn.Click += OnInstallPlugin;
        this.GetControl<Button>("Close")?.Click += (_, _) => Close();
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Show registry URL if configured
        if (_pluginRegistry != null)
        {
            var url = _pluginRegistry.GetRegistryUrl();
            if (url != null && RegistryUrlDisplay != null)
                RegistryUrlDisplay.Text = $"Registry: {url}";
        }
        await RefreshPlugins();
    }

    private async void OnRefreshPlugins(object? sender, RoutedEventArgs e)
    {
        await RefreshPlugins();
    }

    private async Task RefreshPlugins()
    {
        PluginListPanel?.Children.Clear();
        _allPlugins.Clear();

        if (_pluginRegistry == null)
        {
            var msg = new TextBlock { Text = "Plugin registry not available.", Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 107)), Margin = new Thickness(16) };
            PluginListPanel?.Children.Add(msg);
            PluginCountLabel?.SetText("No registry configured");
            return;
        }

        try
        {
            var plugins = (await _pluginRegistry.ListInstalledPluginsAsync()).ToList();
            var registryPlugins = (await _pluginRegistry.ListRegistryPluginsAsync()).ToList();

            foreach (var p in plugins)
            {
                _allPlugins.Add(new PluginCardInfo(p, p.IsEnabled));
            }

            _filteredPlugins.Clear();
            _filteredPlugins.AddRange(_allPlugins);
            RenderPluginCards();
            PluginCountLabel?.SetText($"Plugins: {plugins.Count}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to discover plugins");
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        var query = PluginSearchBox?.Text?.Trim().ToLowerInvariant() ?? string.Empty;
        _filteredPlugins.Clear();
        if (string.IsNullOrEmpty(query))
            _filteredPlugins.AddRange(_allPlugins);
        else
            _filteredPlugins.AddRange(_allPlugins.Where(p =>
                p.Name.ToLowerInvariant().Contains(query) ||
                p.Description.ToLowerInvariant().Contains(query) ||
                p.Id.ToLowerInvariant().Contains(query)));
        RenderPluginCards();
    }

    private void RenderPluginCards()
    {
        PluginListPanel?.Children.Clear();

        if (_filteredPlugins.Count == 0)
        {
            var msg = new TextBlock
            {
                Text = "No plugins found.",
                Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
                Margin = new Thickness(16)
            };
            PluginListPanel?.Children.Add(msg);
            return;
        }

        foreach (var cardInfo in _filteredPlugins)
        {
            var plugin = cardInfo.Definition;
            var card = new StackPanel { Classes = { "pluginCard" } };

            // Name + Version
            var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
            var nameText = new TextBlock
            {
                Text = plugin.Name,
                Classes = { "pluginName" },
                Margin = new Thickness(0, 0, 8, 0)
            };
            var versionText = new TextBlock
            {
                Text = $"v{plugin.Version}",
                Classes = { "pluginVersion" }
            };
            nameRow.Children.Add(nameText);
            nameRow.Children.Add(versionText);
            card.Children.Add(nameRow);

            // Description
            var descText = new TextBlock
            {
                Text = plugin.Description,
                Classes = { "pluginDescription" },
                Margin = new Thickness(0, 2, 0, 8)
            };
            card.Children.Add(descText);

            // Controls row
            var controlsRow = new StackPanel { Orientation = Orientation.Horizontal };

            // Install/Update button
            var installBtn = new Button
            {
                Content = plugin.RegistryVersion != null ? $"Update to {plugin.RegistryVersion}" : "Install",
                Classes = { "pluginInstallBtn" },
                Margin = new Thickness(0, 0, 8, 0)
            };
            if (plugin.IsInstalled)
                installBtn.Content = "Reinstall";
            installBtn.Tag = plugin;
            installBtn.Click += (_, _) => OnInstallPluginClicked(plugin, installBtn);
            controlsRow.Children.Add(installBtn);

            // Enable/Disable toggle
            var toggleBtn = new Button
            {
                Content = cardInfo.Enabled ? "Disable" : "Enable",
                Classes = { "pluginBtn" },
                Margin = new Thickness(0, 0, 8, 0)
            };
            toggleBtn.Tag = plugin;
            toggleBtn.Click += (_, _) => OnTogglePluginClicked(plugin, toggleBtn);
            controlsRow.Children.Add(toggleBtn);

            // Policy dropdown
            var policyCombo = new ComboBox
            {
                SelectedIndex = plugin.SandboxPolicy switch
                {
                    { AllowFileWrites: false, AllowNetworkAccess: false, AllowCommandExecution: false } => 0,
                    { AllowFileWrites: true, AllowNetworkAccess: false } => 1,
                    { AllowFileWrites: true, AllowNetworkAccess: true } => 2,
                    _ => 0
                },
                Width = 160,
                Margin = new Thickness(0, 0, 8, 0)
            };
            policyCombo.Items.Add("Strict (No filesystem access)");
            policyCombo.Items.Add("Restricted (Read-only)");
            policyCombo.Items.Add("Full (Unrestricted)");
            policyCombo.Tag = plugin;
            policyCombo.SelectionChanged += (_, _) => OnPolicyChanged(plugin, policyCombo);
            controlsRow.Children.Add(policyCombo);

            // Uninstall button
            var uninstallBtn = new Button
            {
                Content = "Uninstall",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 107)),
                Background = new SolidColorBrush(Color.FromArgb(255, 45, 45, 48)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 107, 107)),
                Padding = new Thickness(10, 5)
            };
            uninstallBtn.Tag = plugin;
            uninstallBtn.Click += (_, _) => OnUninstallPluginClicked(plugin);
            controlsRow.Children.Add(uninstallBtn);

            card.Children.Add(controlsRow);
            PluginListPanel?.Children.Add(card);
        }
    }

    private async void OnInstallPluginClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not PluginDefinition plugin) return;
        btn.IsEnabled = false;
        btn.Content = "Installing...";
        try
        {
            if (_pluginRegistry != null)
            {
                await _pluginRegistry.InstallPluginAsync(plugin);
                await RefreshPlugins();
            }
        }
        catch (Exception ex)
        {
            ShowError($"Install failed: {ex.Message}");
            btn.IsEnabled = true;
            btn.Content = "Retry";
        }
    }

    private async void OnTogglePluginClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not PluginDefinition plugin) return;
        btn.Content = plugin.IsEnabled ? "Disabling..." : "Enabling...";
        btn.IsEnabled = false;
        try
        {
            if (_pluginRegistry != null)
            {
                await _pluginRegistry.SetEnabledStateAsync(plugin.Id, !plugin.IsEnabled);
                await RefreshPlugins();
            }
        }
        catch (Exception ex)
        {
            ShowError($"Toggle failed: {ex.Message}");
            btn.IsEnabled = true;
            btn.Content = plugin.IsEnabled ? "Disable" : "Enable";
        }
    }

    private void OnPolicyChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not ComboBox combo || combo.Tag is not PluginDefinition plugin) return;
        var idx = combo.SelectedIndex;
        _ = Task.Run(async () =>
        {
            if (_pluginRegistry == null) return;
            var policy = idx switch
            {
                0 => PluginSandboxPolicyDefaults.Default,
                1 => new PluginSandboxPolicy(
                    AllowFileWrites: true, AllowNetworkAccess: false, AllowCommandExecution: false,
                    AllowedPaths: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "/", "/home", "/tmp" },
                    BlockedCommands: PluginSandboxPolicyDefaults.Default.BlockedCommands,
                    MaxExecutionTime: TimeSpan.FromMinutes(10), MaxMemoryMb: 512),
                2 => new PluginSandboxPolicy(
                    AllowFileWrites: true, AllowNetworkAccess: true, AllowCommandExecution: true,
                    AllowedPaths: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "/" },
                    BlockedCommands: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sudo", "su", "fdisk", "dd", "mkfs" },
                    MaxExecutionTime: TimeSpan.FromMinutes(15), MaxMemoryMb: 1024),
                _ => PluginSandboxPolicyDefaults.Default
            };
            await _pluginRegistry.SetSandboxPolicyAsync(plugin.Id, policy);
        });
    }

    private async void OnUninstallPluginClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not PluginDefinition plugin) return;
        if (ShowConfirm($"Uninstall plugin '{plugin.Name}'?") == true)
        {
            try
            {
                if (_pluginRegistry != null)
                {
                    await _pluginRegistry.UninstallPluginAsync(plugin.Id);
                    await RefreshPlugins();
                }
            }
            catch (Exception ex)
            {
                ShowError($"Uninstall failed: {ex.Message}");
            }
        }
    }

    private async void OnInstallPlugin(object? sender, RoutedEventArgs e)
    {
        var inputBox = new Window
        {
            Title = "Install Plugin",
            Width = 400,
            Height = 200,
            Content = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 34)),
                Child = new StackPanel
                {
                    Padding = new Thickness(20),
                    Children =
                    {
                        new TextBlock { Text = "Enter plugin URL or local path:", Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)) },
                        new TextBox { x:Name = "PluginUrlInput", Margin = new Thickness(0, 8, 0, 16), Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)), Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)) },
                        new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Children =
                        {
                            new Button { Content = "Cancel", Margin = new Thickness(0, 0, 8, 0), Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)), Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)), BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromRgb(58, 58, 62)) }
                                { Click = (_, _) => inputBox.Close(false) },
                            new Button { Content = "Install", Background = new SolidColorBrush(Color.FromRgb(79, 195, 247)), Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255)) }
                                { Click = async (_, _) =>
                                {
                                    var input = inputBox.ContentPanel!.Children.OfType<TextBox>().FirstOrDefault();
                                    var url = input?.Text;
                                    inputBox.Close(true);
                                    if (!string.IsNullOrWhiteSpace(url) && _pluginRegistry != null)
                                    {
                                        try
                                        {
                                            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                                            {
                                                var def = new PluginDefinition(
                                                    Id = $"remote-{Guid.NewGuid():N}",
                                                    Name = "Remote Plugin",
                                                    Description = "Plugin installed from remote URL",
                                                    Version = new Version(1, 0, 0),
                                                    RegistryVersion = null,
                                                    IsInstalled = false,
                                                    IsEnabled = true,
                                                    Tags = new List<string>(),
                                                    Author = "remote",
                                                    DownloadUrl = uri,
                                                    SandboxPolicy = null);
                                                await _pluginRegistry.InstallPluginAsync(def);
                                            }
                                            else
                                            {
                                                var def = new PluginDefinition(
                                                    Id = $"local-{Guid.NewGuid():N}",
                                                    Name = "Local Plugin",
                                                    Description = "Plugin installed from local path",
                                                    Version = new Version(1, 0, 0),
                                                    RegistryVersion = null,
                                                    IsInstalled = false,
                                                    IsEnabled = true,
                                                    Tags = new List<string>(),
                                                    Author = "local",
                                                    DownloadUrl = new Uri(url),
                                                    SandboxPolicy = null);
                                                await _pluginRegistry.InstallPluginAsync(def);
                                            }
                                            await RefreshPlugins();
                                        }
                                        catch (Exception ex)
                                        {
                                            ShowError($"Install failed: {ex.Message}");
                                        }
                                    }
                                } }
                        }}
                    }
                }
            }
        };
        inputBox.ShowDialog(this);
    }

    private static bool? ShowConfirm(string message)
    {
        var win = new Window
        {
            Title = "Confirm",
            Width = 350,
            Height = 150,
            Content = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 34)),
                Child = new StackPanel
                {
                    Padding = new Thickness(20),
                    Children =
                    {
                        new TextBlock { Text = message, Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)) },
                        new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0), Children =
                        {
                            new Button { Content = "Cancel", Margin = new Thickness(0, 0, 8, 0), Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)), Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)), BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromRgb(58, 58, 62)) }
                                { Click = (_, _) => win.Close(false) },
                            new Button { Content = "OK", Background = new SolidColorBrush(Color.FromRgb(255, 107, 107)), Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255)) }
                                { Click = (_, _) => win.Close(true) }
                        }}
                    }
                }
            }
        };
        return win.ShowDialog<bool>();
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
                    Background = new SolidColorBrush(Color.FromRgb(37, 37, 41)),
                    Child = new TextBlock
                    {
                        Text = message,
                        Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
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
            System.Diagnostics.Debug.WriteLine($"Plugin error: {message}");
        }
    }
}