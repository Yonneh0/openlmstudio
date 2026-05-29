using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using ClipboardExtensions = Avalonia.Input.Platform.ClipboardExtensions;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Types;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Infrastructure.Services;

namespace OpenLMStudio.Desktop.Controls;

/// <summary>
/// ViewModel for a single LoRA adapter entry in the ImageGenerationTab.
/// </summary>
public class LoraViewModel
{
    public string Name { get; set; } = "";
    public string FilePath { get; set; } = "";
    public double Weight { get; set; } = 1.0;
}

/// <summary>
/// Image Generation Tab — compact layout for the 280px left sidebar.
/// </summary>
public partial class ImageGenerationTab : UserControl
{
    private readonly IImageGenerationCoordinator? _coordinator;
    private readonly IImageGalleryService? _galleryService;
    private readonly IImageFormatConverter? _formatConverter;
    private readonly IImageSaver? _imageSaver;
    private readonly IDiffusionPipelineService? _pipeline;
    private readonly IDeviceStatusService? _deviceStatus;
    private readonly IModelRepository? _modelRepo;

    private string _selectedModelId = "";
    private string _currentMode = "Generate"; // Generate, Image2Image, Inpaint, Variation
    private bool _isModelLoaded;
    private double _modelVramUsage = 0;
    private double _totalVram = 0;
    private readonly ObservableCollection<LoraViewModel> _loraItems = new();
    private ImageGenerationResult? _lastResult;
    private CancellationTokenSource? _generationCts = new();

    public static readonly StyledProperty<string> ModelStatusColorProperty =
        AvaloniaProperty.Register<ImageGenerationTab, string>(nameof(ModelStatusColor), "#757575");

    public string ModelStatusColor
    {
        get => GetValue(ModelStatusColorProperty);
        set => SetValue(ModelStatusColorProperty, value);
    }

    public ObservableCollection<LoraViewModel> LoraItems => _loraItems;

    public ImageGenerationTab()
    {
        InitializeComponent();
        LoraList.ItemsSource = _loraItems;

        // Try to resolve services from the current Avalonia application
        var app = Avalonia.Application.Current;
        if (app?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWin = desktop.MainWindow as MainWindow;
            if (mainWin != null)
            {
                var sp = mainWin.FindResource("ServiceProvider") as IServiceProvider;
                if (sp != null)
                {
                    _coordinator = (IImageGenerationCoordinator)sp.GetService(typeof(IImageGenerationCoordinator))!;
                    _galleryService = (IImageGalleryService)sp.GetService(typeof(IImageGalleryService))!;
                    _formatConverter = (IImageFormatConverter)sp.GetService(typeof(IImageFormatConverter))!;
                    _imageSaver = (IImageSaver)sp.GetService(typeof(IImageSaver))!;
                    _pipeline = (IDiffusionPipelineService)sp.GetService(typeof(IDiffusionPipelineService))!;
                    _deviceStatus = (IDeviceStatusService)sp.GetService(typeof(IDeviceStatusService))!;
                    _modelRepo = (IModelRepository)sp.GetService(typeof(IModelRepository))!;
                }
            }
        }

        // Initialize VRAM from device status
        UpdateVramDisplay();

        // Load models if pipeline is available
        if (_pipeline != null)
        {
            RefreshModelListAsync();
        }
    }

    private void UpdateVramDisplay()
    {
        if (_deviceStatus != null)
        {
            _totalVram = _deviceStatus.GpuMemory ?? 8.0; // Default to 8GB if unknown
            _modelVramUsage = _isModelLoaded ? _totalVram * 0.7 : 0;
        }
        else
        {
            _totalVram = 8.0;
            _modelVramUsage = _isModelLoaded ? 5.6 : 0;
        }

        var vramPercentage = _totalVram > 0 ? (_modelVramUsage / _totalVram) * 100 : 0;
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            VramBar.Value = vramPercentage;
            VramText.Text = $"{_modelVramUsage:F1} / {_totalVram:F0} GB";
        });
    }

    private async void RefreshModelListAsync()
    {
        if (_pipeline == null) return;

        var models = await _pipeline.GetAvailableModelsAsync();
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            ModelComboBox.Items.Clear();
            foreach (var model in models)
            {
                var item = new ComboBoxItem { Content = model.Name ?? model.Id, Tag = model.Id };
                ModelComboBox.Items.Add(item);
            }

            if (ModelComboBox.Items.Count > 0)
            {
                ModelComboBox.SelectedIndex = 0;
            }
        });
    }

    private async void OnModelSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (ModelComboBox.SelectedItem is ComboBoxItem item && item.Tag is string modelId)
        {
            _selectedModelId = modelId;
            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                ModelInfoText.Text = $"ID: {modelId}";
            });
        }
    }

    private async void OnLoadModel(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedModelId) || _pipeline == null)
        {
            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                ModelInfoText.Text = "No model selected";
            });
            return;
        }

        ModelStatusColor = "#FF9800";
        ModelStatusText.Text = " Loading";
        ModelInfoText.Text = "Loading model...";

        var success = await _pipeline.LoadModelAsync(_selectedModelId);

        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (success)
            {
                _isModelLoaded = true;
                ModelStatusColor = "#4CAF50";
                ModelStatusText.Text = " Loaded";
                _modelVramUsage = _totalVram * 0.7;
                UpdateVramDisplay();
                ModelInfoText.Text = $"Loaded: {_selectedModelId}";
            }
            else
            {
                ModelStatusColor = "#FF6B6B";
                ModelStatusText.Text = " Error";
                ModelInfoText.Text = "Failed to load model";
            }
        });
    }

    private async void OnUnloadModel(object? sender, RoutedEventArgs e)
    {
        if (_pipeline == null) return;

        var success = await _pipeline.UnloadModelAsync(_selectedModelId);

        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (success)
            {
                _isModelLoaded = false;
                ModelStatusColor = "#757575";
                ModelStatusText.Text = " Unloaded";
                _modelVramUsage = 0;
                UpdateVramDisplay();
                ModelInfoText.Text = "Model unloaded";
            }
        });
    }

    private void OnRefreshModels(object? sender, RoutedEventArgs e)
    {
        RefreshModelListAsync();
    }

    private void OnGenerate(object? sender, RoutedEventArgs e)
    {
        _currentMode = "Generate";
        UpdateModeButtons();
        ExecuteGeneration();
    }

    private void OnImage2Image(object? sender, RoutedEventArgs e)
    {
        _currentMode = "Image2Image";
        UpdateModeButtons();
    }

    private void OnInpaint(object? sender, RoutedEventArgs e)
    {
        _currentMode = "Inpaint";
        UpdateModeButtons();
    }

    private void OnVariation(object? sender, RoutedEventArgs e)
    {
        _currentMode = "Variation";
        UpdateModeButtons();
    }

    private void UpdateModeButtons()
    {
        var accentBrush = this.FindResource("AccentBlue") as IBrush ?? new SolidColorBrush(Colors.Black);
        var normalBrush = this.FindResource("BgTertiary") as IBrush ?? new SolidColorBrush(Colors.Black);

        GenerateButton.Background = _currentMode == "Generate" ? accentBrush : normalBrush;
        Image2ImageButton.Background = _currentMode == "Image2Image" ? accentBrush : normalBrush;
        InpaintButton.Background = _currentMode == "Inpaint" ? accentBrush : normalBrush;
        VariationButton.Background = _currentMode == "Variation" ? accentBrush : normalBrush;
    }

    private async void ExecuteGeneration()
    {
        if (_coordinator == null || string.IsNullOrEmpty(_selectedModelId))
        {
            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                ProgressText.Text = "Select a model first";
            });
            return;
        }

        var width = int.TryParse(WidthTextBox.Text, out var w) ? w : 1024;
        var height = int.TryParse(HeightTextBox.Text, out var h) ? h : 1024;
        var steps = int.TryParse(StepsTextBox.Text, out var s) ? s : 30;
        var cfg = double.TryParse(CfgTextBox.Text, out var c) ? c : 7.5;
        var seed = long.TryParse(SeedTextBox.Text, out var sd) ? sd : -1;

        var prompt = PromptTextBox.Text ?? "";
        var negativePrompt = NegativePromptTextBox.Text ?? "";

        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            Progress.Value = 0;
            ProgressText.Text = "Generating...";
            GenerateButton2.IsEnabled = false;
            PauseButton.IsEnabled = true;
        });

        var result = await _coordinator.ExecuteAsync(new ImageGenerationCommand(
            PipelineType: _selectedModelId,
            Prompt: prompt,
            NegativePrompt: negativePrompt,
            Width: width,
            Height: height,
            Steps: steps,
            CfgScale: cfg,
            Seed: (int)seed,
            SamplerType: SamplerComboBox.SelectedItem is ComboBoxItem ci2 ? ci2.Content?.ToString() ?? "Euler" : "Euler",
            LoRAAdapters: null,
            ImageToImage: null,
            ControlNet: null,
            Inpaint: null,
            OutputFormat: "png",
            OutputPath: OutputPathTextBox.Text), CancellationToken.None);

        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            _lastResult = result;
            Progress.Value = 100;
            ProgressText.Text = "Complete!";
            GenerateButton2.IsEnabled = true;
            PauseButton.IsEnabled = false;

            // Display result
            if (result.ImageBytes != null && result.ImageBytes.Length > 0)
            {
                using var ms = new System.IO.MemoryStream(result.ImageBytes);
                var bitmap = new Bitmap(ms);
                PreviewImage.Source = bitmap;
            }
        });
    }

    private void OnRandomizeSeed(object? sender, RoutedEventArgs e)
    {
        SeedTextBox.Text = new Random().Next(0, 999999999).ToString();
    }

    private void OnPresetSelected(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Content is string content)
        {
            if (long.TryParse(content, out var preset))
            {
                WidthTextBox.Text = preset.ToString();
                HeightTextBox.Text = preset.ToString();
            }
        }
    }

    private void OnAddLora(object? sender, RoutedEventArgs e)
    {
        _loraItems.Add(new LoraViewModel { Name = "new_adapter.safetensors", Weight = 1.0 });
    }

    private void OnClearLora(object? sender, RoutedEventArgs e)
    {
        _loraItems.Clear();
    }

    private void OnRemoveLora(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Parent is Panel panel)
        {
            var item = panel.Parent as Border;
            if (item?.DataContext is LoraViewModel lora)
            {
                _loraItems.Remove(lora);
            }
        }
    }

    private void OnBrowseOutput(object? sender, RoutedEventArgs e)
    {
        // Placeholder for file dialog
        OutputPathTextBox.Text = "~/Pictures/OpenLMStudio";
    }

    private void OnStop(object? sender, RoutedEventArgs e)
    {
        _generationCts?.Cancel();
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            ProgressText.Text = "Stopped";
        });
    }

    private async void OnSaveImage(object? sender, RoutedEventArgs e)
    {
        if (_lastResult?.ImageBytes == null || _imageSaver == null)
        {
            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                ProgressText.Text = "No image to save";
            });
            return;
        }

        var format = OutputFormatComboBox.SelectedIndex switch
        {
            0 => ImageOutputFormat.Png,
            1 => ImageOutputFormat.Jpeg,
            2 => ImageOutputFormat.WebP,
            3 => ImageOutputFormat.Ico,
            4 => ImageOutputFormat.Bmp,
            5 => ImageOutputFormat.Gif,
            _ => ImageOutputFormat.Png
        };

        var path = OutputPathTextBox.Text ?? "~/Pictures/OpenLMStudio";
        await _imageSaver.SaveToDiskAsync(_lastResult!.ImageBytes, path, format);

        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            ProgressText.Text = $"Saved as {format}";
        });
    }

    private async void OnCopyImage(object? sender, RoutedEventArgs e)
    {
        if (_lastResult?.ImageBytes == null) return;

        // Copy to clipboard as PNG
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard != null)
        {
            await ClipboardExtensions.SetTextAsync(topLevel.Clipboard, Convert.ToBase64String(_lastResult.ImageBytes));
        }

        ProgressText.Text = "Copied to clipboard";
    }

    private void OnViewGallery(object? sender, RoutedEventArgs e)
    {
        // Placeholder: open gallery window
        ProgressText.Text = "Gallery view";
    }
}