using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using OpenLMStudio.Domain.Models.LLamaCpp;
using OpenLMStudio.Infrastructure.Services;

namespace OpenLMStudio.Desktop.Controls;

/// <summary>
/// Interaction logic for SystemModelSelector.axaml.
/// </summary>
public partial class SystemModelSelector : UserControl
{
    private SystemAIManager? _systemAIManager;
    private readonly GgufModelDownloader _modelDownloader;
    private readonly LogViewerService _logViewer;

    /// <summary>
    /// Default parameterless constructor for XAML instantiation.
    /// </summary>
    public SystemModelSelector()
    {
        InitializeComponent();
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

    private async Task DiscoverModelsAsync()
    {
        try
        {
            var models = await _modelDownloader.DiscoverModelsAsync();
            foreach (var model in models)
            {
                var item = new ComboBoxItem
                {
                    Content = $"{model.Name} ({FormatSize(model.FileSizeBytes)})",
                    Tag = model.FilePath
                };
            }
        }
        catch { /* silently handle */ }
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
        var statusColor = e.NewState switch
        {
            SystemAIState.Running => "#4CAF50",
            SystemAIState.Stopped => "#FF6B6B",
            SystemAIState.Starting => "#FF9800",
            SystemAIState.Stopping => "#FF9800",
            SystemAIState.Error => "#FF6B6B",
            _ => "#888888"
        };

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
            ModelNameText.Text = System.IO.Path.GetFileNameWithoutExtension(e.ModelPath);
            ModelPathText.Text = e.ModelPath;
        }
    }

    private async void OnLoadModelClicked(object? sender, RoutedEventArgs e)
    {
        var window = Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        if (window == null) return;

        var files = await window.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Select GGUF Model",
            AllowMultiple = false,
            FileTypeFilter = new[] { new Avalonia.Platform.Storage.FilePickerFileType("GGUF Files") { Patterns = new[] { "*.gguf" } } }
        }).ConfigureAwait(false);

        if (files?.Any() == true)
        {
            var modelPath = files.First().Path.LocalPath;
            _systemAIManager?.StartAsync(modelPath);
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
        // TODO: Show advanced settings dialog
    }

    private void OnLogEntryReceived(object? sender, LogEntry e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // Update log display
        });
    }

    private static string FormatSize(long bytes)
    {
        return bytes switch
        {
            < 1024 * 1024 => $"{bytes / 1024} MB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} GB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
        };
    }
}