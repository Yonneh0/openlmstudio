using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.Input.Platform;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Types;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Infrastructure.Services;

namespace OpenLMStudio.Desktop.Controls;

/// <summary>
/// ViewModel for a single LoRA adapter entry in the UI.
/// </summary>
public class LoraViewModel
{
    public string Name { get; set; } = string.Empty;
    public double Weight { get; set; }
    public string ModelId { get; set; } = string.Empty;
}

/// <summary>
/// Image generation tab — UI for text-to-image, image-to-image, inpainting, and variation.
/// Designed for the narrow left sidebar (max ~280px).
/// </summary>
public partial class ImageGenerationTab : UserControl
{
    private readonly IImageGenerationCoordinator? _coordinator;
    private readonly IImageSaver? _saver;
    private readonly IImageFormatConverter? _formatConverter;
    private readonly IImageGalleryService? _galleryService;
    private readonly IDiffusionPipelineService? _pipeline;
    private readonly IModelRepository? _modelRepo;
    private CancellationTokenSource? _generationCts;
    private string _currentPipeline = "sd15";
    private bool _isGenerating;
    private readonly List<LoraViewModel> _loraViewModels = new();
    private byte[]? _inputImageBytes;
    private ImageGalleryEntry? _lastResult;
    private readonly List<ImageGalleryEntry> _recentImages = new();
    private string? _selectedModelId;
    private bool _isModelLoaded;
    public string ModelStatusColor { get; private set; } = "#757575"; // Loaded=green, Loading=orange, Unloaded=gray

    public ImageGenerationTab() : this(null, null, null)
    {
    }

    public ImageGenerationTab(
        IImageGenerationCoordinator? coordinator,
        IImageSaver? saver,
        IImageFormatConverter? formatConverter,
        IImageGalleryService? galleryService = null,
        IDiffusionPipelineService? pipeline = null,
        IModelRepository? modelRepo = null)
    {
        InitializeComponent();
        _coordinator = coordinator;
        _saver = saver;
        _formatConverter = formatConverter;
        _galleryService = galleryService;
        _pipeline = pipeline;
        _modelRepo = modelRepo;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        LoraList.ItemsSource = _loraViewModels;
        // Default to SD1.5
        OnPipelineSelected(this, new RoutedEventArgs());
        // Load models
        await RefreshModelListAsync();
    }

    #region Model Loading

    private async void OnRefreshModels(object? sender, RoutedEventArgs e)
    {
        await RefreshModelListAsync();
    }

    private async Task RefreshModelListAsync()
    {
        if (_modelRepo == null)
            return;

        var models = await _modelRepo.ListMultiModalModelsAsync();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ModelComboBox.Items.Clear();
            foreach (var model in models)
            {
                var name = model.Id ?? model.FilePath?.Split('/').LastOrDefault() ?? "Unknown";
                var item = new ComboBoxItem { Content = name };
                item.Tag = model;
                ModelComboBox.Items.Add(item);
            }
            if (ModelComboBox.Items.Count > 0)
                ModelComboBox.SelectedIndex = 0;
        });
    }

    private void OnModelSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (ModelComboBox.SelectedItem is ComboBoxItem item && item.Tag is MultiModalModelMetadata metadata)
        {
            _selectedModelId = metadata.Id;
            ModelInfoText.Text = $"Type: {GetPipelineType(metadata)} | Channels: {GetLatentChannels(metadata)} | File: {Path.GetFileName(metadata.FilePath ?? "")}";
            ModelStatusText.Text = _isModelLoaded ? " ● Loaded" : " ○ Unloaded";
            UpdateModelStatusColor();
        }
    }

    private async void OnLoadModel(object? sender, RoutedEventArgs e)
    {
        if (_selectedModelId == null)
        {
            ModelStatusText.Text = " — No model selected";
            return;
        }

        _isModelLoaded = true;
        ModelStatusText.Text = " ● Loading...";
        UpdateModelStatusColor();
        LoadModelButton.IsEnabled = false;

        if (_pipeline != null)
        {
            await _pipeline.LoadModelAsync(_selectedModelId);
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ModelStatusText.Text = " ● Loaded";
            LoadModelButton.IsEnabled = true;
            UpdateModelStatusColor();
        });
    }

    private async void OnUnloadModel(object? sender, RoutedEventArgs e)
    {
        if (_selectedModelId == null)
            return;

        _isModelLoaded = false;
        ModelStatusText.Text = " ○ Unloading...";
        UpdateModelStatusColor();
        UnloadModelButton.IsEnabled = false;

        if (_pipeline != null)
        {
            await _pipeline.UnloadModelAsync(_selectedModelId);
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ModelStatusText.Text = " ○ Unloaded";
            UnloadModelButton.IsEnabled = true;
            UpdateModelStatusColor();
        });
    }

    private void UpdateModelStatusColor()
    {
        ModelStatusColor = _isModelLoaded ? "#4CAF50" : "#757575";
        ModelStatusText.Foreground = (SolidColorBrush)this.FindResource("TextSecondary")!;
    }

    private static string GetPipelineType(MultiModalModelMetadata metadata)
    {
        if (metadata.Id != null && metadata.Id.IndexOf("sdxl", StringComparison.OrdinalIgnoreCase) >= 0)
            return "SDXL";
        if (metadata.Id != null && metadata.Id.IndexOf("flux", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Flux";
        if (metadata.Id != null && metadata.Id.IndexOf("sd3", StringComparison.OrdinalIgnoreCase) >= 0)
            return "SD3";
        return "SD1.5";
    }

    private static int GetLatentChannels(MultiModalModelMetadata metadata)
    {
        // Heuristic: SD1.5 = 4, SDXL = 4, SD3 = 16, Flux = 16
        var type = GetPipelineType(metadata);
        return type == "Flux" || type == "SD3" ? 16 : 4;
    }

    #endregion

    #region Mode Tabs

    private void OnPipelineSelected(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            _currentPipeline = button.Content?.ToString()?.ToLowerInvariant() switch
            {
                "sdxl" => "sdxl",
                "sd3" or "sd 3" => "sd3",
                "flux" => "flux",
                _ => "sd15",
            };

            // Update button styles
            foreach (var btn in new[] { GenerateButton, Image2ImageButton, InpaintButton, VariationButton })
            {
                var isActive = btn == button;
                btn.Background = isActive ?
                    (SolidColorBrush)this.FindResource("AccentBlue")! :
                    (SolidColorBrush)this.FindResource("BgTertiary")!;
            }
        }
    }

    private void OnImage2Image(object? sender, RoutedEventArgs e)
    {
        ImageInputBorder.IsVisible = true;
    }

    private async void OnInpaint(object? sender, RoutedEventArgs e)
    {
        await OnLoadImageInternal();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ImageInputBorder.IsVisible = true;
        });
    }

    private void OnVariation(object? sender, RoutedEventArgs e)
    {
        if (_inputImageBytes != null)
        {
            ImageInputBorder.IsVisible = true;
            DenoiseSlider.Value = 1.0;
            DenoiseValue.Text = "1.00";
        }
        else
        {
            OnImage2Image(sender, e);
        }
    }

    #endregion

    #region Image Input

    private async void OnLoadImage(object? sender, RoutedEventArgs e)
    {
        await OnLoadImageInternal();
    }

    private async Task OnLoadImageInternal()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var files = await ((IStorageProvider)topLevel).OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load Image",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp" } },
            }
        });

        if (files.Any())
        {
            _inputImageBytes = await File.ReadAllBytesAsync(files[0].Path.LocalPath);
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                using var stream = new MemoryStream(_inputImageBytes);
                var bitmap = new Bitmap(stream);
                ImagePreview.Source = bitmap;
            });
        }
    }

    private void OnDenoiseChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        DenoiseValue.Text = e.NewValue.ToString("F2");
    }

    #endregion

    #region LoRA Adapters

    private void OnAddLora(object? sender, RoutedEventArgs e)
    {
        _loraViewModels.Add(new LoraViewModel { Name = "New LoRA", Weight = 1.0, ModelId = "lora_new" });
    }

    private void OnClearLora(object? sender, RoutedEventArgs e)
    {
        _loraViewModels.Clear();
    }

    private void OnRemoveLora(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is LoraViewModel lora)
        {
            _loraViewModels.Remove(lora);
        }
    }

    #endregion

    #region Generation

    private async void OnGenerate(object? sender, RoutedEventArgs e)
    {
        if (_isGenerating) return;
        _isGenerating = true;
        GenerateButton.IsEnabled = false;
        GenerateButton2.IsEnabled = false;
        Progress.Value = 0;
        ProgressText.Text = "0%";

        var request = new ImageGenerationCommand(
            PipelineType: _currentPipeline,
            Prompt: PromptTextBox.Text ?? "",
            NegativePrompt: NegativePromptTextBox.Text,
            Width: ParseInt(WidthTextBox.Text, 1024),
            Height: ParseInt(HeightTextBox.Text, 1024),
            Steps: ParseInt(StepsTextBox.Text, 30),
            CfgScale: ParseDouble(CfgTextBox.Text, 7.5),
            Seed: (int)ParseLong(SeedTextBox.Text, -1),
            SamplerType: SamplerComboBox.SelectedIndex switch
            {
                1 => "EulerA",
                2 => "DPMS",
                3 => "DPMSSDE",
                4 => "Heun",
                5 => "LMS",
                _ => "Euler",
            },
            LoRAAdapters: _loraViewModels.Select(l => new LoraAdapterCommand(l.ModelId, l.Weight)).ToList(),
            ImageToImage: _inputImageBytes != null ? new ImageToImageCommand(_inputImageBytes, DenoiseSlider.Value) : null,
            ControlNet: null,
            Inpaint: null,
            OutputFormat: GetSelectedFormat(),
            OutputPath: ExpandPath(OutputPathTextBox.Text));

        if (_coordinator != null)
        {
            _generationCts = new CancellationTokenSource();
            try
            {
                await foreach (var progress in _coordinator.ExecuteStreamingAsync(request, _generationCts.Token))
                {
                    var topLevel = TopLevel.GetTopLevel(this);
                    if (topLevel != null)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            Progress.Value = progress.Percentage;
                            ProgressText.Text = $"{progress.Percentage:F0}%";
                        });
                    }
                }

                var result = await _coordinator.ExecuteAsync(request, _generationCts.Token);
                await Dispatcher.UIThread.InvokeAsync(() => ShowResult(result));
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.UIThread.InvokeAsync(() => ProgressText.Text = "Cancelled");
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() => ProgressText.Text = $"Error: {ex.Message}");
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _isGenerating = false;
                    GenerateButton.IsEnabled = true;
                    GenerateButton2.IsEnabled = true;
                });
            }
        }
    }

    private void OnStop(object? sender, RoutedEventArgs e)
    {
        _generationCts?.Cancel();
    }

    private void OnRandomizeSeed(object? sender, RoutedEventArgs e)
    {
        SeedTextBox.Text = new Random().Next(int.MaxValue).ToString();
    }

    private void OnPresetSelected(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            var content = btn.Content?.ToString() ?? "";
            var numStr = new string(content.Where(c => char.IsDigit(c)).ToArray());
            if (int.TryParse(numStr, out var preset) && preset > 0)
            {
                WidthTextBox.Text = preset.ToString();
                HeightTextBox.Text = preset.ToString();
            }
        }
    }

    private async void OnBrowseOutput(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var folder = await ((IStorageProvider)topLevel).OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Output Folder"
        });

        if (folder.Any())
        {
            OutputPathTextBox.Text = folder[0].Path.LocalPath;
        }
    }

    private void ShowResult(ImageGenerationResult result)
    {
        using var stream = new MemoryStream(result.ImageBytes);
        var bitmap = new Bitmap(stream);
        PreviewImage.Source = bitmap;

        Progress.Value = 100;
        ProgressText.Text = "100%";

        if (_saver != null)
        {
            var outputPath = Path.Combine(
                ExpandPath(OutputPathTextBox.Text),
                _saver.GenerateTimestampedFilename());

            var format = GetOutputFormat(GetSelectedFormat());
            var converted = _formatConverter != null
                ? _formatConverter.ConvertAsync(result.ImageBytes, format).Result
                : result.ImageBytes;

            _saver.SaveToDiskAsync(converted, outputPath, format).Wait();

            _lastResult = new ImageGalleryEntry(
                Id: Guid.NewGuid().ToString(),
                Prompt: result.Prompt,
                ModelId: result.ModelId,
                Width: result.Width,
                Height: result.Height,
                Seed: result.Seed,
                CfgScale: result.GuidanceScale,
                Steps: result.Steps,
                Sampler: result.SamplerType,
                FilePath: outputPath,
                ThumbnailPath: outputPath,
                Timestamp: DateTime.UtcNow);

            _recentImages.Insert(0, _lastResult);
            if (_recentImages.Count > 20)
                _recentImages.RemoveAt(_recentImages.Count - 1);

            UpdateRecentImages();
        }
    }

    private void UpdateRecentImages()
    {
        RecentImages.ItemsSource = _recentImages;
    }

    private async void OnSaveImage(object? sender, RoutedEventArgs e)
    {
        if (_lastResult == null || _saver == null) return;
        var expanded = _saver.ExpandPath(OutputPathTextBox.Text);
        var filename = _saver.GenerateTimestampedFilename();
        var path = Path.Combine(expanded, filename);
        if (_formatConverter != null)
        {
            var format = GetOutputFormat(GetSelectedFormat());
            var imageBytes = await File.ReadAllBytesAsync(_lastResult.FilePath);
            var converted = await _formatConverter.ConvertAsync(imageBytes, format);
            await _saver.SaveToDiskAsync(converted, path, format);
        }
    }

    private void OnCopyImage(object? sender, RoutedEventArgs e)
    {
        if (_lastResult == null) return;
        try
        {
            var imageBytes = File.ReadAllBytes(_lastResult.FilePath);
            var text = Convert.ToBase64String(imageBytes);
            var top = TopLevel.GetTopLevel(this);
            if (top?.Clipboard != null)
            {
                Dispatcher.UIThread.InvokeAsync(() => top.Clipboard!.SetTextAsync(text));
            }
        }
        catch { }
    }

    private void OnViewGallery(object? sender, RoutedEventArgs e)
    {
        // TODO: Open gallery window
    }

    #endregion

    #region Helpers

    private static int ParseInt(string? value, int defaultValue)
    {
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    private static long ParseLong(string? value, long defaultValue)
    {
        return long.TryParse(value, out var result) ? result : defaultValue;
    }

    private static double ParseDouble(string? value, double defaultValue)
    {
        return double.TryParse(value, out var result) ? result : defaultValue;
    }

    private string GetSelectedFormat()
    {
        return OutputFormatComboBox.SelectedIndex switch
        {
            1 => "jpeg",
            2 => "webp",
            3 => "ico",
            4 => "bmp",
            5 => "gif",
            _ => "png",
        };
    }

    private static ImageOutputFormat GetOutputFormat(string format)
        => format.ToLowerInvariant() switch
        {
            "jpeg" or "jpg" => ImageOutputFormat.Jpeg,
            "webp" => ImageOutputFormat.WebP,
            "ico" => ImageOutputFormat.Ico,
            "bmp" => ImageOutputFormat.Bmp,
            "gif" => ImageOutputFormat.Gif,
            _ => ImageOutputFormat.Png,
        };

    private static string ExpandPath(string path)
    {
        if (path.StartsWith("~/", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, path.Substring(2));
        }
        return path;
    }

    #endregion
}