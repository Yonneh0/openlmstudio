using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models.LLamaCpp;
using OpenLMStudio.Infrastructure.Services;

namespace OpenLMStudio.Desktop.Controls;

/// <summary>
/// Interaction logic for SystemModelSelector.axaml.
/// Manages the UI for loading and managing SystemAI models.
/// </summary>
public partial class SystemModelSelector : UserControl
{
    private SystemAIManager? _systemAIManager;
    private GgufModelDownloader? _modelDownloader;
    private LogViewerService? _logViewer;
    private readonly ILogger<SystemModelSelector>? _logger;
    private bool _isInitialized;

    /// <summary>
    /// Default parameterless constructor for XAML instantiation.
    /// </summary>
    public SystemModelSelector()
    {
        InitializeComponent();
        // Wire up button events so clicks work regardless of whether SetManager is called
        WireUpEvents();
        // Resolve dependencies from the app service provider if not set via DI
        try
        {
            var sp = GetAppServiceProvider();
            if (sp != null)
            {
                _modelDownloader = sp.GetService(typeof(GgufModelDownloader)) as GgufModelDownloader
                    ?? throw new InvalidOperationException("GgufModelDownloader not registered in DI");
                _logViewer = sp.GetService(typeof(LogViewerService)) as LogViewerService
                    ?? throw new InvalidOperationException("LogViewerService not registered in DI");
                _logger = sp.GetService(typeof(ILogger<SystemModelSelector>)) as ILogger<SystemModelSelector>;
            }
        }
        catch
        {
            // If DI resolution fails, use null (controls will gracefully handle nulls)
        }
    }

    /// <summary>
    /// Gets the application service provider.
    /// </summary>
    private static IServiceProvider? GetAppServiceProvider()
    {
        try
        {
            return App.ApplicationServices;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Constructor with DI parameters (for direct instantiation).
    /// </summary>
    public SystemModelSelector(
        SystemAIManager systemAIManager,
        GgufModelDownloader modelDownloader,
        LogViewerService logViewer)
    {
        InitializeComponent();
        _systemAIManager = systemAIManager;
        _modelDownloader = modelDownloader;
        _logViewer = logViewer;
        WireUpEvents();
    }

    /// <summary>
    /// Sets the manager after construction (for XAML-instantiated controls).
    /// </summary>
    public void SetManager(SystemAIManager manager)
    {
        // Unsubscribe from old events
        if (_systemAIManager != null)
        {
            _systemAIManager.StateChanged -= OnStateChanged;
            _systemAIManager.LogEntryReceived -= OnLogEntryReceived;
        }

        _systemAIManager = manager;

        // Subscribe to state changes
        if (_systemAIManager != null)
        {
            _systemAIManager.StateChanged += OnStateChanged;
            _systemAIManager.LogEntryReceived += OnLogEntryReceived;
        }

        // Update UI with current state
        UpdateUI();
    }

    /// <summary>
    /// Wires up event handlers for buttons and state/log events.
    /// </summary>
    private void WireUpEvents()
    {
        LoadModelButton.Click += OnLoadModelClicked;
        StopButton.Click += OnStopClicked;
        RestartButton.Click += OnRestartClicked;
        AdvancedSettingsButton.Click += OnAdvancedSettingsClicked;

        if (_systemAIManager != null)
        {
            _systemAIManager.StateChanged += OnStateChanged;
            _systemAIManager.LogEntryReceived += OnLogEntryReceived;
        }
    }

    /// <summary>
    /// Discovers models and updates the UI.
    /// </summary>
    private async Task DiscoverModelsAsync()
    {
        if (_modelDownloader == null) return;

        try
        {
            var models = await _modelDownloader.DiscoverModelsAsync();
            // Models are auto-discovered; update the UI with the first model if available
            if (models.Any())
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var firstModel = models.First();
                    if (ModelNameText != null)
                    {
                        ModelNameText.Text = firstModel.Name;
                        ModelPathText.Text = firstModel.FilePath;
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to discover models");
        }
    }

    private void OnStateChanged(object? sender, SystemAIStateChanged e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            UpdateStateDisplay(e);
        });
    }

    private void UpdateStateDisplay(SystemAIStateChanged e)
    {
        var accentRed = (ISolidColorBrush)(this.FindResource("AccentRed") ?? Brushes.Gray);
        var accentGreen = (ISolidColorBrush)(this.FindResource("AccentGreen") ?? Brushes.Green);
        StatusBadge.Background = e.NewState == SystemAIState.Running ? accentGreen : accentRed;
        StatusText.Text = e.NewState.ToString();

        if (e.NewState == SystemAIState.Running)
        {
            StopButton.IsVisible = true;
            RestartButton.IsVisible = true;
            LoadModelButton.IsVisible = false;
        }
        else
        {
            StopButton.IsVisible = false;
            RestartButton.IsVisible = false;
            LoadModelButton.IsVisible = true;
        }

        if (!string.IsNullOrEmpty(e.ModelPath))
        {
            ModelNameText.Text = Path.GetFileNameWithoutExtension(e.ModelPath);
            ModelPathText.Text = e.ModelPath;
        }

        // Update engine info and settings
        UpdateUI();
    }

    /// <summary>
    /// Updates the UI with current manager state.
    /// </summary>
    private void UpdateUI()
    {
        if (_systemAIManager == null) return;

        var settings = _systemAIManager.CurrentSettings;
        var backend = _systemAIManager.CurrentBackend;

        // Update model type
        if (ModelTypeText != null)
            ModelTypeText.Text = "SystemAI";

        // Update backend and port
        if (BackendText != null)
            BackendText.Text = backend.ToString();

        if (PortText != null)
            PortText.Text = "Port: 8082";

        // Update settings
        if (settings != null)
        {
            if (GpuLayersText != null)
                GpuLayersText.Text = $"GPU: {settings.GpuLayers}";

            if (CtxSizeText != null)
                CtxSizeText.Text = $"Ctx: {settings.ContextSize}";

            if (BatchSizeText != null)
                BatchSizeText.Text = $"Batch: {settings.BatchSize}";
        }

        // Update status info
        if (StatusInfoText != null)
        {
            var modelInfo = _systemAIManager.CurrentModelPath != null
                ? $"{(settings?.GpuLayers ?? 0)} GPU | {settings?.Threads ?? 0} threads"
                : "Ready";
            StatusInfoText.Text = modelInfo;
        }
    }

    private async void OnLoadModelClicked(object? sender, RoutedEventArgs e)
    {
        var window = Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        if (window == null) return;

        // Show download progress
        if (DownloadProgressArea != null)
            DownloadProgressArea.IsVisible = true;
        if (DownloadProgressText != null)
            DownloadProgressText.Text = "Selecting model...";
        if (DownloadProgressBar != null)
            DownloadProgressBar.Value = 0;

        var files = await window.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Select GGUF Model",
            AllowMultiple = false,
            FileTypeFilter = new[] { new Avalonia.Platform.Storage.FilePickerFileType("GGUF Files") { Patterns = new[] { "*.gguf" } } }
        }).ConfigureAwait(false);

        if (files?.Any() == true)
        {
            var modelPath = files.First().Path.LocalPath;

            if (DownloadProgressText != null)
                DownloadProgressText.Text = "Loading model...";
            if (DownloadProgressBar != null)
                DownloadProgressBar.Value = 50;

            if (_systemAIManager != null)
            {
                var success = await _systemAIManager.StartAsync(modelPath);
                if (success)
                {
                    UpdateUI();
                    _logger?.LogInformation("SystemAI model loaded: {Model}", modelPath);
                }
            }

            // Hide download progress after a short delay
            _ = Task.Delay(TimeSpan.FromMilliseconds(1000)).ContinueWith(_ =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (DownloadProgressArea != null)
                        DownloadProgressArea.IsVisible = false;
                });
            });
        }
        else
        {
            if (DownloadProgressArea != null)
                DownloadProgressArea.IsVisible = false;
        }
    }

    private void OnStopClicked(object? sender, RoutedEventArgs e)
    {
        _systemAIManager?.Stop();
    }

    private void OnRestartClicked(object? sender, RoutedEventArgs e)
    {
        if (_systemAIManager?.CurrentModelPath != null)
        {
            _ = _systemAIManager.StartAsync(_systemAIManager.CurrentModelPath);
        }
    }

    private void OnAdvancedSettingsClicked(object? sender, RoutedEventArgs e)
    {
        if (_systemAIManager == null) return;

        var settings = _systemAIManager.CurrentSettings;
        if (settings == null) return;

        var win = new Window
        {
            Title = "Advanced Settings",
            Width = 400,
            Height = 500,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        win.Content = CreateSettingsPanel(settings, win);

        var parentWindow = Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        win.ShowDialog(parentWindow ?? new Window());
    }

    private Control CreateSettingsPanel(RecommendedSettings settings, Window dialog)
    {
        var panel = new StackPanel
        {
            Margin = new Thickness(16)
        };

        panel.Children.Add(new TextBlock
        {
            Text = "GPU Layers",
            Margin = new Thickness(0, 0, 0, 4),
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        });
        panel.Children.Add(new Slider
        {
            Value = settings.GpuLayers,
            Minimum = 0,
            Maximum = 100,
            Margin = new Thickness(0, 0, 0, 16)
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Context Size",
            Margin = new Thickness(0, 0, 0, 4),
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        });
        panel.Children.Add(new TextBox
        {
            Text = settings.ContextSize.ToString(),
            Margin = new Thickness(0, 0, 0, 16)
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Batch Size",
            Margin = new Thickness(0, 0, 0, 4),
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        });
        panel.Children.Add(new TextBox
        {
            Text = settings.BatchSize.ToString(),
            Margin = new Thickness(0, 0, 0, 16)
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Threads",
            Margin = new Thickness(0, 0, 0, 4),
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        });
        panel.Children.Add(new TextBox
        {
            Text = settings.Threads.ToString(),
            Margin = new Thickness(0, 0, 0, 16)
        });

        var saveButton = new Button
        {
            Content = "Save",
            Margin = new Thickness(0, 16, 0, 0),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
        saveButton.Click += (s, e) =>
        {
            // TODO: Apply settings
            dialog?.Close();
        };
        panel.Children.Add(saveButton);

        return panel;
    }

    private void OnLogEntryReceived(object? sender, LogEntry e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (e.IsImportant)
            {
                // Show important messages in the status info
                if (StatusInfoText != null)
                {
                    var msg = e.Message.Length > 50 ? e.Message[..50] + "..." : e.Message;
                    StatusInfoText.Text = msg;
                }
            }
        });
    }

    private static string FormatSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / (1024.0):F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
        };
    }

    /// <summary>
    /// Called after the control is loaded into the visual tree.
    /// </summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _isInitialized = true;
        _ = DiscoverModelsAsync();
    }
}