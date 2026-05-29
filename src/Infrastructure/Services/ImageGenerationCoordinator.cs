using System.Runtime.CompilerServices;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Types;
using ImageOutputFormat = OpenLMStudio.Application.Types.ImageOutputFormat;
using OpenLMStudio.Infrastructure.Services;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// SystemAI coordinator for orchestrating image generation commands.
/// </summary>
public class ImageGenerationCoordinator : IImageGenerationCoordinator
{
    private readonly IDiffusionPipelineService _pipeline;
    private readonly IImageToImageService _imageToImage;
    private readonly IImageFormatConverter _formatConverter;
    private readonly IImageSaver _saver;
    private readonly ILogger<ImageGenerationCoordinator>? _logger;
    private bool _isGenerating;
    private int _currentStep;
    private int _totalSteps;
    private float _percentage;
    private string? _currentModelId;
    private string? _currentPrompt;

    public ImageGenerationCoordinator(
        IDiffusionPipelineService pipeline,
        IImageToImageService imageToImage,
        IImageFormatConverter formatConverter,
        IImageSaver saver,
        ILogger<ImageGenerationCoordinator>? logger = null)
    {
        _pipeline = pipeline;
        _imageToImage = imageToImage;
        _formatConverter = formatConverter;
        _saver = saver;
        _logger = logger;
    }

    public ImageGenerationStatus GetStatus()
        => new(_isGenerating, _currentStep, _totalSteps, _percentage, _currentModelId, _currentPrompt);

    public async Task<ImageGenerationResult> ExecuteAsync(ImageGenerationCommand command, CancellationToken ct = default)
    {
        _isGenerating = true;
        _currentModelId = command.PipelineType;
        _currentPrompt = command.Prompt;
        _percentage = 0;

        ImageGenerationResult result;

        if (command.ImageToImage != null)
        {
            var i2iResult = await _imageToImage.EncodeAndDenoiseAsync(
                new ImageToImageRequest(
                    command.PipelineType,
                    command.Prompt,
                    command.NegativePrompt,
                    command.ImageToImage.InputImage,
                    command.ImageToImage.DenoiseStrength,
                    command.Width,
                    command.Height,
                    command.CfgScale,
                    command.Steps,
                    command.Seed,
                    command.LoRAAdapters?.Select(l => new LoraAdapterReference(l.ModelId, l.Weight)).ToList(),
                    (ImageSamplerType)Enum.Parse(typeof(ImageSamplerType), command.SamplerType, true)
                ),
                ct
            );

            // Convert to requested format
            var format = ParseOutputFormat(command.OutputFormat);
            var converted = await _formatConverter.ConvertAsync(i2iResult.ImageBytes, format);

            // Save
            var filepath = await _saver.SaveToDiskAsync(converted, command.OutputPath ?? _saver.GenerateTimestampedFilename(), format);

            result = new ImageGenerationResult(
                converted,
                i2iResult.Width,
                i2iResult.Height,
                i2iResult.Seed,
                i2iResult.GuidanceScale,
                i2iResult.Steps,
                i2iResult.ModelId)
            {
                MimeType = format switch
                {
                    ImageOutputFormat.Png => "image/png",
                    ImageOutputFormat.Jpeg => "image/jpeg",
                    ImageOutputFormat.WebP => "image/webp",
                    ImageOutputFormat.Bmp => "image/bmp",
                    ImageOutputFormat.Gif => "image/gif",
                    ImageOutputFormat.Ico => "image/x-icon",
                    _ => "image/png",
                }
            };
        }
        else
        {
            var genRequest = new OpenLMStudio.Application.Types.ImageGenerationRequest(
                ModelId: command.PipelineType,
                Prompt: command.Prompt,
                NegativePrompt: command.NegativePrompt,
                Width: command.Width,
                Height: command.Height,
                GuidanceScale: command.CfgScale,
                Steps: command.Steps,
                Seed: command.Seed,
                LoraAdapters: command.LoRAAdapters?.Select(l => new LoraAdapterReference(l.ModelId, l.Weight)).ToList(),
                SamplerType: (ImageSamplerType)Enum.Parse(typeof(ImageSamplerType), command.SamplerType, true)
            );
            result = await _pipeline.GenerateImageAsync(genRequest, ct);
        }

        _isGenerating = false;
        return result;
    }

    public async IAsyncEnumerable<ImageGenerationProgress> ExecuteStreamingAsync(ImageGenerationCommand command, [EnumeratorCancellation] CancellationToken ct = default)
    {
        _isGenerating = true;
        _currentModelId = command.PipelineType;
        _currentPrompt = command.Prompt;

        var genRequest = new OpenLMStudio.Application.Types.ImageGenerationRequest(
            ModelId: command.PipelineType,
            Prompt: command.Prompt,
            NegativePrompt: command.NegativePrompt,
            Width: command.Width,
            Height: command.Height,
            GuidanceScale: command.CfgScale,
            Steps: command.Steps,
            Seed: command.Seed,
            LoraAdapters: command.LoRAAdapters?.Select(l => new LoraAdapterReference(l.ModelId, l.Weight)).ToList(),
            SamplerType: (ImageSamplerType)Enum.Parse(typeof(ImageSamplerType), command.SamplerType, true)
        );

        await foreach (var progress in _pipeline.StreamProgressAsync(genRequest, ct))
        {
            _currentStep = progress.Step;
            _totalSteps = progress.TotalSteps;
            _percentage = progress.Percentage;
            yield return progress;
        }

        _isGenerating = false;
    }

    public void Dispose()
    {
        (_pipeline as IDisposable)?.Dispose();
        (_imageToImage as IDisposable)?.Dispose();
        (_formatConverter as IDisposable)?.Dispose();
        (_saver as IDisposable)?.Dispose();
    }

    private static ImageOutputFormat ParseOutputFormat(string format)
        => format.ToLowerInvariant() switch
        {
            "png" => ImageOutputFormat.Png,
            "jpeg" or "jpg" => ImageOutputFormat.Jpeg,
            "webp" => ImageOutputFormat.WebP,
            "ico" => ImageOutputFormat.Ico,
            "bmp" => ImageOutputFormat.Bmp,
            "gif" => ImageOutputFormat.Gif,
            _ => ImageOutputFormat.Png,
        };
}