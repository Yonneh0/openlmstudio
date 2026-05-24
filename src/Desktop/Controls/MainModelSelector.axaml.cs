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
/// Interaction logic for MainModelSelector.axaml.
/// </summary>
public partial class MainModelSelector : UserControl
{
    private MainAIManager? _mainAIManager;
    private readonly GgufModelDownloader _modelDownloader;
    private readonly LogViewerService _logViewer;

    /// <summary>
    /// Default parameterless constructor for XAML instantiation.
    /// </summary>
    public MainModelSelector()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Constructor with DI parameters (for direct instantiation).
    /// </summary>
    public MainModelSelector(
        MainAIManager mainAIManager,
        GgufModelDownloader modelDownloader,
        LogViewerService logViewer)
    {
        InitializeComponent();
        _mainAIManager = mainAIManager;
        _modelDownloader = modelDownloader;
        _logViewer = logViewer;
        WireUpEvents();
    }

    /// <summary>
    /// Sets the manager after construction (for XAML-instantiated controls).
    /// </summary>
    public void SetManager(MainAIManager manager)
    {
        // Unsubscribe from old events
        if (_mainAIManager != null)
        {
            _mainAIManager.StateChanged -= OnStateChanged;
            _mainAIManager.LogEntryReceived -= OnLogEntryReceived;
        }

        _mainAIManager = manager;

        // Subscribe to state changes
        if (_mainAIManager != null)
        {
            _mainAIManager.StateChanged += OnStateChanged;
            _mainAIManager.LogEntryReceived += OnLogEntryReceived;
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

        if (_mainAIManager != null)
        {
            _mainAIManager.StateChanged += OnStateChanged;
            _mainAIManager.LogEntryReceived += OnLogEntryReceived;
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
        catch
        {
            // Silently handle discovery errors
        }
    }

    private void OnStateChanged(object? sender, MainAIStateChanged state)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            UpdateStateDisplay(state);
        });
    }

    private void UpdateStateDisplay(MainAIStateChanged state)
    {
        var statusColor = state.NewState switch
        {
            MainAIState.Running => "#4CAF50",
            MainAIState.Stopped => "#FF6B6B",
            MainAIState.Starting => "#FF9800",
            MainAIState.Stopping => "#FF9800",
            MainAIState.Error => "#FF6B6B",
            _ => "#888888"
        };

        var accentRed = (ISolidColorBrush)(this.FindResource("AccentRed") ?? Brushes.Gray);
        var accentGreen = (ISolidColorBrush)(this.FindResource("AccentGreen") ?? Brushes.Green);
        StatusBadge.Background = state.NewState == MainAIState.Running ? accentGreen : accentRed;
        StatusText.Text = state.NewState.ToString();

        if (state.NewState == MainAIState.Running)
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

        if (!string.IsNullOrEmpty(state.ModelPath))
        {
            ModelNameText.Text = System.IO.Path.GetFileNameWithoutExtension(state.ModelPath);
            ModelPathText.Text = state.ModelPath;
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
            _mainAIManager?.StartAsync(modelPath);
        }
    }

    private void OnStopClicked(object? sender, RoutedEventArgs e)
    {
        _mainAIManager?.Stop();
    }

    private void OnRestartClicked(object? sender, RoutedEventArgs e)
    {
        if (_mainAIManager?.CurrentModelPath != null)
        {
            _ = _mainAIManager.StartAsync(_mainAIManager.CurrentModelPath);
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