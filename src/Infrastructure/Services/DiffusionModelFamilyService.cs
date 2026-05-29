using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Manages diffusion model families and provides configuration per family.
/// Supports SD 1.x, SDXL, SD 3, Flux, and custom families.
/// </summary>
public class DiffusionModelFamilyService : IDiffusionModelFamilyService
{
    private readonly ILogger<DiffusionModelFamilyService>? _logger;
    private readonly List<DiffusionModelFamilyConfig> _families = new();

    private static readonly ImageSamplerType[] AllSamplers = {
        ImageSamplerType.Euler,
        ImageSamplerType.EulerA,
        ImageSamplerType.DPMS,
        ImageSamplerType.LMS,
    };

    public DiffusionModelFamilyService(ILogger<DiffusionModelFamilyService>? logger)
    {
        _logger = logger;
        RegisterDefaultFamilies();
    }

    public IReadOnlyList<DiffusionModelFamilyConfig> Families => _families.AsReadOnly();

    public DiffusionModelFamilyConfig? GetFamily(string pipelineType)
        => _families.FirstOrDefault(f => f.PipelineType.Equals(pipelineType, StringComparison.OrdinalIgnoreCase));

    public DiffusionModelFamilyConfig? GetFamilyByModelId(string modelId)
    {
        var lower = modelId.ToLowerInvariant();
        // First try matching against the pipeline type (most reliable)
        var byPipeline = _families.FirstOrDefault(f => f.PipelineType.Equals(lower, StringComparison.OrdinalIgnoreCase));
        if (byPipeline != null) return byPipeline;

        // Then try matching against the safetensors file pattern
        var byPattern = _families.FirstOrDefault(f => f.SupportedSafetensorsFilePattern != null
            && lower.Contains(f.SupportedSafetensorsFilePattern.ToLowerInvariant()));
        if (byPattern != null) return byPattern;

        // Finally try matching against the family name
        return _families.FirstOrDefault(f => lower.Contains(f.Name.ToLowerInvariant()));
    }

    public void RegisterFamily(DiffusionModelFamilyConfig config)
    {
        if (_families.Any(f => f.Name.Equals(config.Name, StringComparison.OrdinalIgnoreCase)))
            return; // Already registered
        _families.Add(config);
        _logger?.LogInformation("Registered diffusion model family '{Name}'", config.Name);
    }

    public void RegisterDefaultFamilies()
    {
        RegisterFamily(new DiffusionModelFamilyConfig(
            "SD 1.5",
            "sd15",
            LatentChannels: 4,
            DefaultWidth: 512,
            DefaultHeight: 512,
            RecommendedStepsMin: 20,
            RecommendedStepsMax: 50,
            CfgScaleMin: 5.0,
            CfgScaleMax: 12.0,
            SupportedSafetensorsFilePattern: "stable-diffusion-v1-5",
            SupportedSamplers: AllSamplers));

        RegisterFamily(new DiffusionModelFamilyConfig(
            "SDXL",
            "sdxl",
            LatentChannels: 4,
            DefaultWidth: 1024,
            DefaultHeight: 1024,
            RecommendedStepsMin: 30,
            RecommendedStepsMax: 50,
            CfgScaleMin: 7.0,
            CfgScaleMax: 12.0,
            SupportedSafetensorsFilePattern: "sdxl",
            SupportedSamplers: AllSamplers));

        RegisterFamily(new DiffusionModelFamilyConfig(
            "SD 3",
            "sd3",
            LatentChannels: 4,
            DefaultWidth: 1024,
            DefaultHeight: 1024,
            RecommendedStepsMin: 25,
            RecommendedStepsMax: 50,
            CfgScaleMin: 4.0,
            CfgScaleMax: 10.0,
            SupportedSafetensorsFilePattern: "stable-diffusion-3",
            SupportedSamplers: AllSamplers));

        // Flux.1-dev — original Flux with DiT architecture (T5 + CLIP).
        RegisterFamily(new DiffusionModelFamilyConfig(
            "Flux.1-dev",
            "flux1",
            LatentChannels: 16,
            DefaultWidth: 1024,
            DefaultHeight: 1024,
            RecommendedStepsMin: 20,
            RecommendedStepsMax: 35,
            CfgScaleMin: 1.0,
            CfgScaleMax: 3.5,
            SupportedSafetensorsFilePattern: "flux1",
            SupportedSamplers: AllSamplers));

        // Flux.2 — Flux.2 with improved DiT, requires T5-XL encoder.
        RegisterFamily(new DiffusionModelFamilyConfig(
            "Flux.2",
            "flux2",
            LatentChannels: 16,
            DefaultWidth: 1024,
            DefaultHeight: 1024,
            RecommendedStepsMin: 25,
            RecommendedStepsMax: 50,
            CfgScaleMin: 1.0,
            CfgScaleMax: 3.5,
            SupportedSafetensorsFilePattern: "flux2",
            SupportedSamplers: AllSamplers));

        // Flux.1-schnell — quantized, faster Flux variant.
        RegisterFamily(new DiffusionModelFamilyConfig(
            "Flux.1-schnell",
            "flux_schnell",
            LatentChannels: 16,
            DefaultWidth: 1024,
            DefaultHeight: 1024,
            RecommendedStepsMin: 4,
            RecommendedStepsMax: 8,
            CfgScaleMin: 0.0,
            CfgScaleMax: 1.0,
            SupportedSafetensorsFilePattern: "schnell",
            SupportedSamplers: new[] { ImageSamplerType.Euler, ImageSamplerType.DPMS }));
    }

    public void Dispose() { }
}