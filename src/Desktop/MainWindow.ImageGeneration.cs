// Avalonia Window code-behind — Image generation partial class
// Brought to you by Carls' Jr.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Types;

namespace OpenLMStudio.Desktop;

public partial class MainWindow
{
    /// <summary>
    /// Handler for the Generate Image button — calls the DiffusionPipelineService to generate an image.
    /// </summary>
    private async void OnImageGenGenerateClicked(object? sender, RoutedEventArgs e)
    {
        if (_diffusionPipeline == null)
        {
            // Resolve from DI
            try
            {
                _diffusionPipeline = GetAppServiceProvider()?.GetService(typeof(OpenLMStudio.Application.Interfaces.IDiffusionPipelineService)) as OpenLMStudio.Application.Interfaces.IDiffusionPipelineService;
            }
            catch { /* Ignore resolution errors */ }
        }

        if (_diffusionPipeline == null)
        {
            ShowError("Diffusion pipeline not available. Ensure ONNX Runtime and diffusion models are configured.");
            return;
        }

        var prompt = ImageGenPromptInput?.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(prompt))
        {
            ShowError("Prompt is required for image generation.");
            return;
        }

        var negativePrompt = ImageGenNegPromptInput?.Text ?? string.Empty;

        // Read parameters from UI
        var steps = (int)(ImageGenStepsSlider?.Value ?? 20);
        var cfgScale = (float)(ImageGenCfgSlider?.Value ?? 7.5);
        var seedText = ImageGenSeedInput?.Text;
        var seed = long.TryParse(seedText ?? string.Empty, out var parsedSeed) ? parsedSeed : -1;
        var batchSize = (int)(ImageGenBatchSizeSlider?.Value ?? 1);

        // Get resolution from selector — handle both TextBlock content and string content
        var resolutionText = GetResolutionTextFromSelector();
        var resolution = ParseResolution(resolutionText);

        // Show progress
        ImageGenProgressText.Text = $"Generating image...";
        ImageGenGenerateBtn.IsEnabled = false;

        try
        {
            // Get the selected model
            var selectedModelItem = ImageGenModelSelector?.SelectedItem as ContentControl;
            var selectedModel = selectedModelItem?.Content as TextBlock;
            var modelMetadata = selectedModel?.Tag as OpenLMStudio.Domain.Models.MultiModalModelMetadata;
            var modelId = modelMetadata?.Id ?? "default";

            var result = await _diffusionPipeline.GenerateImageAsync(
                new Application.Interfaces.ImageGenerationRequest(
                    modelId,
                    prompt,
                    string.IsNullOrWhiteSpace(negativePrompt) ? null : negativePrompt,
                    resolution.Width,
                    resolution.Height,
                    cfgScale,
                    steps,
                    seed),
                CancellationToken.None);

            // Display the generated image
            if (ImageGenOutputArea != null)
            {
                // ImageGenOutputArea is a Border — wrap content in a StackPanel
                var existingPanel = ImageGenOutputArea.Child as StackPanel;
                if (existingPanel != null)
                {
                    existingPanel.Children.Clear();
                }
                else
                {
                    existingPanel = new StackPanel();
                    ImageGenOutputArea.Child = existingPanel;
                }

                // Convert to bitmap and display
                using var ms = new MemoryStream(result.ImageBytes);
                var image = new Avalonia.Media.Imaging.Bitmap(ms);

                var imageControl = new Avalonia.Controls.Image
                {
                    Source = image,
                    Stretch = Avalonia.Media.Stretch.Uniform,
                    MaxHeight = 512
                };
                existingPanel.Children.Add(imageControl);

                // Add metadata text
                var metadataText = new TextBlock
                {
                    Text = $"Seed: {result.Seed} | CFG: {result.GuidanceScale} | Steps: {result.Steps} | Model: {result.ModelId}",
                    Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(170, 170, 170)),
                    FontSize = 10,
                    Margin = new Thickness(0, 8, 0, 0),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                };
                existingPanel.Children.Add(metadataText);
            }

            ImageGenProgressText.Text = "Image generated successfully.";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error generating image");
            ImageGenProgressText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            ImageGenGenerateBtn.IsEnabled = true;
        }
    }

    /// <summary>
    /// Extracts the resolution text from the ImageGenResolutionSelector ComboBox.
    /// Handles both TextBlock content (directly added to ComboBox) and string content.
    /// </summary>
    private string GetResolutionTextFromSelector()
    {
        if (ImageGenResolutionSelector?.SelectedItem == null)
            return "512x512";

        var item = ImageGenResolutionSelector.SelectedItem;

        // If the item is a TextBlock, use its Text property
        if (item is TextBlock tb && tb.Text != null)
            return tb.Text;

        // If the item is a ContentControl with TextBlock content, extract the text
        if (item is ContentControl cc && cc.Content is TextBlock contentTb && contentTb.Text != null)
            return contentTb.Text;

        // Fall back to ToString() for other content types
        var text = item.ToString();
        return string.IsNullOrEmpty(text) ? "512x512" : text;
    }

    private static (int Width, int Height) ParseResolution(string resolutionText)
    {
        try
        {
            // Split by 'x' to get width and height — handle both square (512x512) and non-square (1280x720) formats
            var parts = resolutionText.Split('x');
            return (int.Parse(parts[0]), int.Parse(parts[1]));
        }
        catch
        {
            return (512, 512);
        }
    }

    /// <summary>
    /// Handler for the Image Generation model selector — discovers available diffusion/VAE models from the repository.
    /// </summary>
    private async void OnImageGenModelSelectorSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_modelRepository == null || _selectedChatId == null) return;

        try
        {
            // Use SearchMultiModalModelsAsync to get image generation / diffusion / VAE models
            var relevantModels = await _modelRepository.SearchMultiModalModelsAsync(
                modelTypeFilter: Domain.Models.ModelType.ImageGeneration);

            // Also include Diffusion and VAE types — we need both in the dropdown
            var allMultiModalModels = (await _modelRepository.ListMultiModalModelsAsync()).ToList();
            var imageGenModels = relevantModels.ToList().Concat(
                allMultiModalModels.Where(m =>
                    new[] { Domain.Models.ModelType.Diffusion, Domain.Models.ModelType.Vae }.Contains(m.ModelType))
            ).DistinctBy(m => m.Id).ToList();

            // Populate the dropdown with available models
            if (ImageGenModelSelector != null)
            {
                ImageGenModelSelector.Items.Clear();

                foreach (var model in imageGenModels)
                {
                    var sizeStr = model.FileSizeBytes > 0
                        ? $"{model.FileSizeBytes / 1_048_576:F0} MB"
                        : "N/A";

                    var item = new TextBlock
                    {
                        Text = $"{model.Name} ({sizeStr})",
                        Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                        Padding = new Thickness(8)
                    };

                    // Store the model metadata for later lookup on selection
                    item.Tag = model;
                    ImageGenModelSelector.Items.Add(item);
                }

                if (imageGenModels.Any())
                {
                    // Auto-select the first model and update resolution based on its default resolution
                    var firstItem = ImageGenModelSelector.Items[0] as ContentControl;
                    MultiModalModelMetadata? selectedModel = null;

                    if (firstItem != null && firstItem.Content is TextBlock txt)
                        selectedModel = txt.Tag as MultiModalModelMetadata;

                    ImageGenModelSelector.SelectedIndex = 0;

                    // Update resolution selector based on selected model's default resolution
                    if (selectedModel?.DefaultResolution != null && selectedModel.DefaultResolution > 0)
                    {
                        UpdateDefaultResolution(selectedModel.DefaultResolution.Value);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to discover image generation models");
        }
    }

    /// <summary>
    /// Updates the resolution dropdown based on a model's default resolution.
    /// </summary>
    private void UpdateDefaultResolution(int defaultRes)
    {
        if (ImageGenResolutionSelector == null || defaultRes <= 0) return;

        // Find existing item that matches the default resolution and select it.
        // Match both square (512x512) and non-square (1280x720) formats.
        var expectedSquare = $"{defaultRes}x{defaultRes}";
        var expectedNonSquare1 = $"{defaultRes}x{defaultRes / 2}"; // e.g., 1024x512
        var expectedNonSquare2 = $"{defaultRes / 2}x{defaultRes}"; // e.g., 512x1024

        foreach (var item in ImageGenResolutionSelector.Items.OfType<ContentControl>())
        {
            if (item.Content is TextBlock tb && tb.Text != null)
            {
                if (tb.Text == expectedSquare || tb.Text == expectedNonSquare1 || tb.Text == expectedNonSquare2)
                {
                    ImageGenResolutionSelector.SelectedItem = item;
                    return;
                }
            }
        }

        // If no matching item found, add the default resolution to the list
        var newItem = new TextBlock
        {
            Text = expectedSquare,
            Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
            Padding = new Thickness(8)
        };

        ImageGenResolutionSelector.Items.Add(newItem);
        ImageGenResolutionSelector.SelectedIndex = ImageGenResolutionSelector.Items.Count - 1;
    }

    /// <summary>
    /// Handler for the random seed button — generates a random seed value.
    /// </summary>
    private void OnRandomSeedClicked(object? sender, RoutedEventArgs e)
    {
        var rng = new Random();
        if (ImageGenSeedInput != null)
            ImageGenSeedInput.Text = rng.Next(int.MinValue, int.MaxValue).ToString();
    }
}