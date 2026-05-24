using System;
using System.Collections.ObjectModel;
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
/// Interaction logic for MainModelSelector.axaml.
/// Manages the UI for loading and managing MainAI models.
/// </summary>
public partial class MainModelSelector : UserControl
{
    private MainAIManager? _mainAIManager;
    private GgufModelDownloader? _modelDownloader;
    private LogViewerService? _logViewer;
    private readonly ILogger<MainModelSelector>? _logger;
    private readonly ObservableCollection<MainModelItem> _modelItems = new();
    private bool _isInitialized;
    private bool _buttonsWired;

    /// <summary>
    /// Default parameterless constructor for XAML instantiation.
    /// </summary>
    public MainModelSelector()
    {
        InitializeComponent();
        // Wire up button events after InitializeComponent
        _wireButtonEvents();
        _buttonsWired = true;
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

        // Re-wire button events if not already done
        if (!_buttonsWired)
        {
            _wireButtonEvents();
            _buttonsWired = true;
        }

        // Update UI with current state
        UpdateUI();
    }

    /// <summary>
    /// Wires up event handlers for buttons and state/log events.
    /// </summary>
    private void WireUpEvents()
    {
        if (!_buttonsWired)
        {
            _wireButtonEvents();
            _buttonsWired = true;
        }

        if (_mainAIManager != null)
        {
            _mainAIManager.StateChanged += OnStateChanged;
            _mainAIManager.LogEntryReceived += OnLogEntryReceived;
        }
    }

    /// <summary>
    /// Wires up button Click events.
    /// </summary>
    private void _wireButtonEvents()
    {
        if (LoadModelButton != null)
            LoadModelButton.Click += OnLoadModelClicked;
        if (StopButton != null)
            StopButton.Click += OnStopClicked;
        if (RestartButton != null)
            RestartButton.Click += OnRestartClicked;
        if (AdvancedSettingsButton != null)
            AdvancedSettingsButton.Click += OnAdvancedSettingsClicked;
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
            ModelNameText.Text = Path.GetFileNameWithoutExtension(state.ModelPath);
            ModelPathText.Text = state.ModelPath;
        }

        // Update engine info and settings (must run on UI thread)
        Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateUI());
    }

    /// <summary>
    /// Updates the UI with current manager state.
    /// </summary>
    private void UpdateUI()
    {
        if (_mainAIManager == null) return;

        var settings = _mainAIManager.CurrentSettings;
        var backend = _mainAIManager.CurrentBackend;

        // Update model type
        if (ModelTypeText != null)
            ModelTypeText.Text = "MainAI";

        // Update backend and port
        if (BackendText != null)
            BackendText.Text = backend.ToString();

        if (PortText != null)
        {
            var activeSlot = _mainAIManager.LoadedModels.FirstOrDefault(m => m.Id == _mainAIManager.ActiveModelId);
            PortText.Text = activeSlot != null && activeSlot.Port > 0 ? $"Port: {activeSlot.Port}" : "Port: --";
        }

        // Update model count
        if (ModelCountText != null)
        {
            var count = _mainAIManager.LoadedModels.Count;
            ModelCountText.Text = count == 1 ? "1 model" : $"{count} models";
        }

        // Update settings (safe even if settings is null)
        if (settings != null)
        {
            if (GpuLayersText != null)
                GpuLayersText.Text = $"GPU: {settings.GpuLayers}";

            if (CtxSizeText != null)
                CtxSizeText.Text = $"Ctx: {settings.ContextSize}";

            if (BatchSizeText != null)
                BatchSizeText.Text = $"Batch: {settings.BatchSize}";
        }

        // Update model list
        UpdateModelList();
    }

    /// <summary>
    /// Updates the model list display.
    /// </summary>
    private void UpdateModelList()
    {
        if (_mainAIManager == null || ModelsItemsControl == null) return;

        _modelItems.Clear();
        foreach (var slot in _mainAIManager.LoadedModels)
        {
            var isActive = slot.Id == _mainAIManager.ActiveModelId;
            _modelItems.Add(new MainModelItem(
                Name: Path.GetFileNameWithoutExtension(slot.ModelPath),
                Port: slot.Port,
                IsActive: isActive));
        }

        ModelsItemsControl.ItemsSource = _modelItems;
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

            if (_mainAIManager != null)
            {
                var success = await _mainAIManager.LoadModelAsync(modelPath);
                if (success)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        UpdateUI();
                        _logger?.LogInformation("Model loaded: {Model}", modelPath);
                    });
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
        _mainAIManager?.StopActiveModel();
    }

    private void OnRestartClicked(object? sender, RoutedEventArgs e)
    {
        if (_mainAIManager?.ActiveModelPath != null)
        {
            _ = _mainAIManager.LoadModelAsync(_mainAIManager.ActiveModelPath);
        }
    }

    private void OnAdvancedSettingsClicked(object? sender, RoutedEventArgs e)
    {
        if (_mainAIManager == null) return;

        var settings = _mainAIManager.CurrentSettings;
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

        var gpuSlider = new Slider
        {
            Value = settings.GpuLayers,
            Minimum = 0,
            Maximum = 100,
            Margin = new Thickness(0, 0, 0, 16)
        };

        panel.Children.Add(new TextBlock
        {
            Text = "GPU Layers",
            Margin = new Thickness(0, 0, 0, 4),
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        });
        panel.Children.Add(gpuSlider);

        var ctxTextBox = new TextBox
        {
            Text = settings.ContextSize.ToString(),
            Margin = new Thickness(0, 0, 0, 16)
        };

        panel.Children.Add(new TextBlock
        {
            Text = "Context Size",
            Margin = new Thickness(0, 0, 0, 4),
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        });
        panel.Children.Add(ctxTextBox);

        var batchTextBox = new TextBox
        {
            Text = settings.BatchSize.ToString(),
            Margin = new Thickness(0, 0, 0, 16)
        };

        panel.Children.Add(new TextBlock
        {
            Text = "Batch Size",
            Margin = new Thickness(0, 0, 0, 4),
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        });
        panel.Children.Add(batchTextBox);

        var threadsTextBox = new TextBox
        {
            Text = settings.Threads.ToString(),
            Margin = new Thickness(0, 0, 0, 16)
        };

        panel.Children.Add(new TextBlock
        {
            Text = "Threads",
            Margin = new Thickness(0, 0, 0, 4),
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        });
        panel.Children.Add(threadsTextBox);

        var saveButton = new Button
        {
            Content = "Save",
            Margin = new Thickness(0, 16, 0, 0),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
        saveButton.Click += (s, e) =>
        {
            // Apply settings from the actual controls (not display text)
            try
            {
                settings.GpuLayers = (int)gpuSlider.Value;
                settings.ContextSize = int.Parse(ctxTextBox.Text ?? settings.ContextSize.ToString());
                settings.BatchSize = int.Parse(batchTextBox.Text ?? settings.BatchSize.ToString());
                settings.Threads = int.Parse(threadsTextBox.Text ?? settings.Threads.ToString());

                // Persist settings to disk
                _mainAIManager?.SaveSettings(settings);

                _logger?.LogInformation("Settings saved: GPU={GpuLayers}, Ctx={Ctx}, Batch={Batch}, Threads={Threads}",
                    settings.GpuLayers, settings.ContextSize, settings.BatchSize, settings.Threads);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to save settings");
            }
            finally
            {
                dialog?.Close();
            }
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
                // Show important messages in the status area
                if (StatusText != null)
                {
                    var msg = e.Message.Length > 40 ? e.Message[..40] + "..." : e.Message;
                    StatusText.Text = msg;
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

/// <summary>
/// Represents a loaded model item for the UI.
/// </summary>
public record MainModelItem(string Name, int Port, bool IsActive);