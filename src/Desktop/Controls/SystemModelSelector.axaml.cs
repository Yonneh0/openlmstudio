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

public partial class SystemModelSelector : UserControl
{
    private readonly SystemAIManager _systemAIManager;
    private readonly GgufModelDownloader _modelDownloader;
    private readonly LogViewerService _logViewer;

    public SystemModelSelector(
        SystemAIManager systemAIManager,
        GgufModelDownloader modelDownloader,
        LogViewerService logViewer)
    {
        InitializeComponent();
        _systemAIManager = systemAIManager;
        _modelDownloader = modelDownloader;
        _logViewer = logViewer;

        LoadModelButton.Click += OnLoadModelClicked;
        StopButton.Click += OnStopClicked;
        RestartButton.Click += OnRestartClicked;
        AdvancedSettingsButton.Click += OnAdvancedSettingsClicked;

        _systemAIManager.StateChanged += OnStateChanged;
        _systemAIManager.LogEntryReceived += OnLogEntryReceived;

        _ = DiscoverModelsAsync();
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

    private void UpdateStateDisplay(SystemAIStateChanged state)
    {
        var accentRed = (ISolidColorBrush)(this.FindResource("AccentRed") ?? Avalonia.Media.Brushes.Gray);
        var accentGreen = (ISolidColorBrush)(this.FindResource("AccentGreen") ?? Avalonia.Media.Brushes.Green);
        StatusBadge.Background = state.NewState == SystemAIState.Running ? accentGreen : accentRed;
        StatusText.Text = state.NewState.ToString();

        if (state.NewState == SystemAIState.Running)
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
            await _systemAIManager.StartAsync(modelPath);
        }
    }

    private void OnStopClicked(object? sender, RoutedEventArgs e) => _systemAIManager.Stop();
    private void OnRestartClicked(object? sender, RoutedEventArgs e)
    {
        if (_systemAIManager.CurrentModelPath != null)
            _ = _systemAIManager.StartAsync(_systemAIManager.CurrentModelPath);
    }

    private void OnAdvancedSettingsClicked(object? sender, RoutedEventArgs e) { /* TODO */ }
    private void OnLogEntryReceived(object? sender, LogEntry e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => { /* update log */ });
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