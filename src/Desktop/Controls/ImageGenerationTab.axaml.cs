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
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Types;
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
    private CancellationTokenSource? _generationCts;
    private string _currentPipeline = "sd15";
    private bool _isGenerating;
    private readonly List<LoraViewModel> _loraViewModels = new();
    private byte[]? _inputImageBytes;
    private ImageGalleryEntry? _lastResult;
    private readonly List<ImageGalleryEntry> _recentImages = new();

    public ImageGenerationTab() : this(null, null, null)
    {
    }

    public ImageGenerationTab(IImageGenerationCoordinator? coordinator, IImageSaver? saver, IImageFormatConverter? formatConverter)
    {
        InitializeComponent();
        _coordinator = coordinator;
        _saver = saver;
        _formatConverter = formatConverter;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        LoraList.ItemsSource = _loraViewModels;
        // Default to SD1.5
        OnPipelineSelected(this, new RoutedEventArgs());
    }

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
            foreach (var btn in new[] { Sd15Button, SdxlButton, Sd3Button, FluxButton })
            {
                var isActive = btn == button;
                btn.Background = isActive ?
                    (SolidColorBrush)this.FindResource("AccentBlue")! :
                    (SolidColorBrush)this.FindResource("BgTertiary")!;
            }
        }
    }

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
            Seed: ParseLong(SeedTextBox.Text, -1),
            SamplerType: SamplerComboBox.SelectedIndex switch
            {
                1 => "EulerA",
                2 => "DPMS",
                3 => "LMS",
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
                // Stream progress for real-time updates
                await foreach (var progress in _coordinator.ExecuteStreamingAsync(request, _generationCts.Token))
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Progress.Value = progress.Percentage;
                        ProgressText.Text = $"{progress.Percentage:F0}%";
                    });
                }

                // Get final result
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
            // TODO: Load mask image and set up inpainting mode
        });
    }

    private void OnVariation(object? sender, RoutedEventArgs e)
    {
        // Use the current preview image as input
        if (_inputImageBytes != null)
        {
            ImageInputBorder.IsVisible = true;
            // Variation uses full denoise (1.0) by default
            DenoiseSlider.Value = 1.0;
            DenoiseValue.Text = "1.00";
        }
        else
        {
            OnImage2Image(sender, e);
        }
    }

    private async void OnLoadImage(object? sender, RoutedEventArgs e)
    {
        await OnLoadImageInternal();
    }

    private async Task OnLoadImageInternal()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageContext == null) return;

        var files = await topLevel.StorageContext.OpenFilePickerAsync(new FilePickerOpenOptions
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
            _inputImageBytes = await File.ReadAllBytesAsync(files[0].Path.FullPath);
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

    private void OnPresetSelected(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            var content = btn.Content?.ToString() ?? "";
            // Parse number from content like "512" or "1024"
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
        if (topLevel?.StorageContext == null) return;

        var folder = await topLevel.StorageContext.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Output Folder"
        });

        if (folder.Any())
        {
            OutputPathTextBox.Text = folder[0].Path.FullPath;
        }
    }

    private void OnStop(object? sender, RoutedEventArgs e)
    {
        _generationCts?.Cancel();
    }

    private void ShowResult(ImageGenerationResult result)
    {
        using var stream = new MemoryStream(result.ImageBytes);
        var bitmap = new Bitmap(stream);
        PreviewImage.Source = bitmap;

        // Update progress
        Progress.Value = 100;
        ProgressText.Text = "100%";

        // Save to gallery
        if (_saver != null)
        {
            var outputPath = Path.Combine(
                ExpandPath(OutputPathTextBox.Text),
                _saver.GenerateTimestampedFilename());

            var format = GetSelectedFormat();
            var converted = _formatConverter != null
                ? _formatConverter.ConvertAsync(result.ImageBytes, GetOutputFormat(format)).Result
                : result.ImageBytes;

            _saver.SaveToDiskAsync(converted, outputPath, GetOutputFormat(format)).Wait();

            _lastResult = new ImageGalleryEntry(
                Id: Guid.NewGuid().ToString(),
                Prompt: result.Prompt,
                ModelId: result.ModelId,
                Width: result.Width,
                Height: result.Height,
                Seed: result.Seed,
                CfgScale: result.GuidanceScale,
                Steps: result.Steps,
                SamplerType: result.SamplerType,
                FilePath: outputPath,
                ThumbnailPath: outputPath,
                Timestamp: DateTime.UtcNow,
                NegativePrompt: result.NegativePrompt,
                LoRAAdapters: result.LoraAdapters?.Select(l => l.ModelId).ToList());

            // Add to recent images
            _recentImages.Insert(0, _lastResult);
            if (_recentImages.Count > 20)
                _recentImages.RemoveAt(_recentImages.Count - 1);

            // Update recent images
            UpdateRecentImages();
        }
    }

    private void UpdateRecentImages()
    {
        RecentImages.ItemsSource = _recentImages;
    }

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
}