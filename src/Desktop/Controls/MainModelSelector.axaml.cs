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
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<MainModelSelector>? _logger;

    /// <summary>
    /// Default parameterless constructor for XAML instantiation.
    /// </summary>
    public MainModelSelector()
    {
        InitializeComponent();
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
                _logger = sp.GetService(typeof(ILogger<MainModelSelector>)) as ILogger<MainModelSelector>;
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

        // Update engine info and settings
        if (_mainAIManager != null)
        {
            var settings = _mainAIManager.CurrentSettings;
            var backend = _mainAIManager.CurrentBackend;
            EngineInfoText.Text = $"{backend} | llama-server";
            GpuLayersText.Text = $"GPU: {settings?.GpuLayers ?? 0}";
            CtxSizeText.Text = $"Ctx: {settings?.ContextSize ?? 4096}";
            BatchSizeText.Text = $"Batch: {settings?.BatchSize ?? 2048}";
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
        _logger?.LogInformation("Advanced settings clicked");
    }

    private void OnLogEntryReceived(object? sender, LogEntry e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // Update log display - could add to a log viewer panel
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
}