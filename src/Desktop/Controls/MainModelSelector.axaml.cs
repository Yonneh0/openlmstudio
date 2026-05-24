using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models.LLamaCpp;
using OpenLMStudio.Infrastructure.Services;

namespace OpenLMStudio.Desktop.Controls;

/// <summary>
/// Interaction logic for MainModelSelector.axaml.
/// </summary>
public partial class MainModelSelector : UserControl
{
    private readonly MainAIManager _mainAIManager;
    private readonly GgufModelDownloader _modelDownloader;
    private readonly LogViewerService _logViewer;
    private bool _isInitialized;

    public MainModelSelector(
        MainAIManager mainAIManager,
        GgufModelDownloader modelDownloader,
        LogViewerService logViewer)
    {
        InitializeComponent();
        _mainAIManager = mainAIManager;
        _modelDownloader = modelDownloader;
        _logViewer = logViewer;

        // Wire up events
        LoadModelButton.Click += OnLoadModelClicked;
        StopButton.Click += OnStopClicked;
        RestartButton.Click += OnRestartClicked;
        AdvancedSettingsButton.Click += OnAdvancedSettingsClicked;

        // Subscribe to state changes
        _mainAIManager.StateChanged += OnStateChanged;
        _mainAIManager.LogEntryReceived += OnLogEntryReceived;

        // Discover models
        _ = DiscoverModelsAsync();
        _isInitialized = true;
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
                // Add to a dropdown or store for later use
            }
        }
        catch (Exception ex)
        {
            // Silently handle discovery errors
        }
    }

    private void OnStateChanged(object? sender, MainAIStateChanged e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            UpdateStateDisplay(e);
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

        StatusBadge.Background = (ISolidColorBrush)(FindResource("AccentRed") ?? Brushes.Gray);
        StatusText.Text = state.NewState.ToString();

        if (state.NewState == MainAIState.Running)
        {
            StatusBadge.Background = (ISolidColorBrush)(FindResource("AccentGreen") ?? Brushes.Green);
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
        // Show file picker
        var window = TopLevel.GetTopLevel(this) as Window;
        var files = await window?.StorageProvider.OpenFilePickerAsync(new OpenFileDialogOptions
        {
            Title = "Select GGUF Model",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FileTypeFilter("GGUF Files", new[] { "*.gguf" }) }
        }).ConfigureAwait(false);

        if (files?.Any() == true)
        {
            var modelPath = files.First().Path.LocalPath;
            await _mainAIManager.StartAsync(modelPath);
        }
    }

    private void OnStopClicked(object? sender, RoutedEventArgs e)
    {
        _mainAIManager.Stop();
    }

    private void OnRestartClicked(object? sender, RoutedEventArgs e)
    {
        if (_mainAIManager.CurrentModelPath != null)
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
        Dispatcher.UIThread.InvokeAsync(() =>
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