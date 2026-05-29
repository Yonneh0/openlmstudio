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
/// </summary>
public partial class ImageGenerationTab : UserControl
{
    private readonly IImageGenerationCoordinator? _coordinator;
    private readonly IImageSaver? _saver;
    private CancellationTokenSource? _generationCts;
    private string _currentPipeline = "sd15";
    private bool _isGenerating;
    private readonly List<LoraViewModel> _loraViewModels = new();

    public ImageGenerationTab(IImageGenerationCoordinator? coordinator = null, IImageSaver? saver = null)
    {
        InitializeComponent();
        _coordinator = coordinator;
        _saver = saver;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Initialize LoRA list
        LoraList.ItemsSource = _loraViewModels;
    }

    private void OnPipelineSelected(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            _currentPipeline = button.Content?.ToString()?.ToLowerInvariant() switch
            {
                "sdxl" => "sdxl",
                "sd 3" or "sd3" => "sd3",
                "flux" => "flux",
                _ => "sd15",
            };

            // Update button styles
            foreach (var btn in new[] { Sd15Button, SdxlButton, Sd3Button, FluxButton })
                btn.Background = btn == button ?
                    (SolidColorBrush)this.FindResource("AccentBlue")! :
                    (SolidColorBrush)this.FindResource("BgTertiary")!;
        }
    }

    private async void OnGenerate(object? sender, RoutedEventArgs e)
    {
        if (_isGenerating) return;
        _isGenerating = true;
        GenerateButton.IsEnabled = false;
        GenerateButton2.IsEnabled = false;

        var request = new ImageGenerationCommand(
            PipelineType: _currentPipeline,
            Prompt: PromptTextBox.Text ?? "",
            NegativePrompt: NegativePromptTextBox.Text,
            Width: int.TryParse(WidthTextBox.Text, out var w) ? w : 1024,
            Height: int.TryParse(HeightTextBox.Text, out var h) ? h : 1024,
            Steps: int.TryParse(StepsTextBox.Text, out var s) ? s : 30,
            CfgScale: double.TryParse(CfgTextBox.Text, out var c) ? c : 7.5,
            Seed: int.TryParse(SeedTextBox.Text, out var seed) ? seed : -1,
            SamplerType: SamplerComboBox.SelectedIndex switch
            {
                1 => "EulerA",
                2 => "DPMS",
                3 => "LMS",
                _ => "Euler",
            },
            LoRAAdapters: _loraViewModels.Select(l => new LoraAdapterCommand(l.ModelId, l.Weight)).ToList(),
            ImageToImage: null,
            ControlNet: null,
            Inpaint: null,
            OutputFormat: GetSelectedFormat(),
            OutputPath: OutputPathTextBox.Text);

        if (_coordinator != null)
        {
            _generationCts = new CancellationTokenSource();
            try
            {
                var result = await _coordinator.ExecuteAsync(request, _generationCts.Token);
                ShowResult(result);
            }
            catch (OperationCanceledException)
            {
                // Generation cancelled
            }
            finally
            {
                _isGenerating = false;
                GenerateButton.IsEnabled = true;
                GenerateButton2.IsEnabled = true;
            }
        }
    }

    private void OnImage2Image(object? sender, RoutedEventArgs e)
    {
        ImageInputBorder.IsVisible = true;
    }

    private void OnInpaint(object? sender, RoutedEventArgs e)
    {
        // TODO: Load mask image
    }

    private void OnVariation(object? sender, RoutedEventArgs e)
    {
        // TODO: Image variation
    }

    private void OnDenoiseChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        DenoiseValue.Text = e.NewValue.ToString("F2");
    }

    private void OnAddLora(object? sender, RoutedEventArgs e)
    {
        _loraViewModels.Add(new LoraViewModel { Name = "New LoRA", Weight = 1.0, ModelId = "lora_new" });
        LoraList.ItemsSource = null;
        LoraList.ItemsSource = _loraViewModels;
    }

    private void OnClearLora(object? sender, RoutedEventArgs e)
    {
        _loraViewModels.Clear();
        LoraList.ItemsSource = null;
        LoraList.ItemsSource = _loraViewModels;
    }

    private void OnRemoveLora(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is LoraViewModel lora)
        {
            _loraViewModels.Remove(lora);
            LoraList.ItemsSource = null;
            LoraList.ItemsSource = _loraViewModels;
        }
    }

    private void OnPresetSelected(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Content?.ToString()?.Replace("²", ""), out var preset))
        {
            WidthTextBox.Text = preset.ToString();
            HeightTextBox.Text = preset.ToString();
        }
    }

    private async void OnBrowseOutput(object? sender, RoutedEventArgs e)
    {
        // TODO: Show folder picker
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
        var entry = new ImageGalleryEntry(
            Id: Guid.NewGuid().ToString(),
            Prompt: result.Prompt,
            ModelId: result.ModelId,
            Width: result.Width,
            Height: result.Height,
            Seed: result.Seed,
            CfgScale: result.GuidanceScale,
            Steps: result.Steps,
            SamplerType: "Euler",
            FilePath: result.DataUri,
            ThumbnailPath: "",
            Timestamp: DateTime.UtcNow,
            NegativePrompt: result.NegativePrompt,
            LoRAAdapters: result.LoraAdapters?.Select(l => l.ModelId).ToList());
        _saver.SaveToGalleryAsync(imageBytes: result.ImageBytes, metadata: new ImageGenerationMetadata(
            Prompt: result.Prompt,
            NegativePrompt: result.NegativePrompt,
            ModelId: result.ModelId,
            Width: result.Width,
            Height: result.Height,
            Seed: result.Seed,
            CfgScale: result.GuidanceScale,
            Steps: result.Steps,
            SamplerType: "Euler"), ct: default);
        }
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
}