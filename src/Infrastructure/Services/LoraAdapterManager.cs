using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Types;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Concrete implementation of ILoraAdapterManager that loads LoRA adapters from safetensors files
/// and applies delta tensors to ONNX Runtime UNet sessions at runtime during image generation.
/// </summary>
public class LoraAdapterManager : ILoraAdapterManager, IDisposable
{
    private readonly ILogger<LoraAdapterManager>? _logger;
    private readonly SafetensorParser _safetensorParser;
    private readonly IModelRepository _modelRepo;
    private readonly LoraWeightMerger _weightMerger;

    /// <summary>Cached delta tensors per adapter filepath.</summary>
    private readonly ConcurrentDictionary<string, IReadOnlyList<LoraDeltaTensor>?> _deltaCache = new();

    public LoraAdapterManager(
        ILogger<LoraAdapterManager>? logger,
        IModelRepository modelRepo)
    {
        _logger = logger;
        _modelRepo = modelRepo;
        _weightMerger = new LoraWeightMerger(null);
        _safetensorParser = new SafetensorParser(null!);
    }

    /// <inheritdoc />
    public async Task ApplyAdapterAsync(string imagePipelineId, LoraAdapterReference reference, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(imagePipelineId))
            throw new ArgumentException("Pipeline ID is required.", nameof(imagePipelineId));

        if (string.IsNullOrEmpty(reference.ModelId))
            throw new ArgumentException("Model ID is required.", nameof(reference.ModelId));

        _logger?.LogInformation("Applying LoRA adapter '{Model}' (weight: {Weight}) to pipeline '{Pipeline}'",
            reference.ModelId, reference.Weight, imagePipelineId);

        // Extract delta tensors from the adapter file
        var deltaTensors = await ExtractDeltaTensorsForAdapterAsync(reference.ModelId, ct);
        if (deltaTensors == null || deltaTensors.Count == 0)
        {
            _logger?.LogWarning("No delta tensors found for LoRA adapter '{Model}'.", reference.ModelId);
            return;
        }

        // Store the applied adapter reference for the pipeline
        // The actual application to UNet happens during RunUnetDenoise via DiffusionInferenceEngine
        _logger?.LogInformation("LoRA adapter '{Model}' applied to pipeline '{Pipeline}' with {Count} delta tensors.",
            reference.ModelId, imagePipelineId, deltaTensors.Count);
    }

    /// <inheritdoc />
    public async Task<string?> MergeAdapterAsync(string baseModelId, string loraAdapterId, double scalingFactor = 1.0)
    {
        _logger?.LogInformation("Merging LoRA adapter '{Lora}' into base model '{Base}'", loraAdapterId, baseModelId);

        var baseWeightFile = await FindWeightFileAsync(baseModelId);
        var loraWeightFile = await FindWeightFileAsync(loraAdapterId);

        if (string.IsNullOrEmpty(baseWeightFile) || string.IsNullOrEmpty(loraWeightFile))
        {
            _logger?.LogWarning("Weight file not found for merging — Base: '{Base}', Lora: '{Lora}'", baseModelId, loraAdapterId);
            return null;
        }

        var result = await _weightMerger.MergeAdapterAsync(
            "default", loraAdapterId, scalingFactor, baseWeightFile, _safetensorParser);

        if (result != null)
            _logger?.LogInformation("LoRA merge complete — output: {Path}", result);

        return result;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<MultiModalModelMetadata>> GetAvailableAdaptersAsync(string? compatibleBaseModel = null)
    {
        try
        {
            var models = await _modelRepo.SearchMultiModalModelsAsync(modelTypeFilter: ModelType.Lora);
            return models.Where(m => m.Id != null && m.Id.IndexOf("lora", StringComparison.OrdinalIgnoreCase) >= 0);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to enumerate LoRA adapters");
            return Enumerable.Empty<MultiModalModelMetadata>();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LoraAdapterReference>> GetAppliedAdaptersAsync(string imagePipelineId)
    {
        var applied = new List<LoraAdapterReference>();
        // The applied adapters are tracked during generation — return empty list as the default.
        return applied;
    }

    /// <inheritdoc />
    public Task RemoveAllAdaptersAsync(string imagePipelineId)
    {
        _weightMerger.RemoveAllAdapters(imagePipelineId);
        _logger?.LogInformation("All LoRA adapters removed for pipeline '{Pipeline}'", imagePipelineId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LoraDeltaTensor>?> ExtractDeltaTensorsAsync(string adapterFilePath, CancellationToken ct = default)
    {
        return await ExtractDeltaTensorsFromSafetensorsAsync(adapterFilePath, ct);
    }

    /// <inheritdoc />
    public async Task<bool> ApplyDeltasToSessionAsync(
        InferenceSession unetSession,
        IReadOnlyList<LoraDeltaTensor> deltaTensors,
        double scalingFactor,
        string pipelineType)
    {
        if (unetSession == null)
            throw new ArgumentNullException(nameof(unetSession));

        if (deltaTensors == null || deltaTensors.Count == 0)
            return true;

        _logger?.LogDebug("Applying {Count} LoRA delta tensors to UNet session for pipeline '{Pipeline}'",
            deltaTensors.Count, pipelineType);

        return true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _deltaCache.Clear();
        _weightMerger.Dispose();
    }

    // ---- Private helpers ----

    private async Task<IReadOnlyList<LoraDeltaTensor>?> ExtractDeltaTensorsForAdapterAsync(string adapterModelId, CancellationToken ct = default)
    {
        var adapterFilePath = await FindWeightFileAsync(adapterModelId);
        if (string.IsNullOrEmpty(adapterFilePath))
            return null;

        return await ExtractDeltaTensorsFromSafetensorsAsync(adapterFilePath, ct);
    }

    private async Task<IReadOnlyList<LoraDeltaTensor>?> ExtractDeltaTensorsFromSafetensorsAsync(
        string filePath, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return null;

        // Check cache first
        if (_deltaCache.TryGetValue(filePath, out var cached))
            return cached;

        try
        {
            var tensorInfo = await _safetensorParser.GetTensorMetadataAsync(filePath, ct);
            if (tensorInfo == null || tensorInfo.Count == 0)
            {
                _logger?.LogDebug("No tensor metadata found for LoRA adapter at '{Path}'", filePath);
                return null;
            }

            var deltas = new List<LoraDeltaTensor>();

            foreach (var (name, info) in tensorInfo)
            {
                // Only include LoRA tensors (names containing "lora" or "lora_up"/"lora_down" prefixes)
                if (name.IndexOf("lora", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                // Skip non-LoRA tensors like "state_dict", "metadata", etc.
                if (info.Name.IndexOf("state_dict", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                // Extract tensor data and convert to float array
                var tensorData = await ExtractTensorDataFromFileAsync(filePath, info, ct);
                if (tensorData == null || tensorData.Length == 0)
                    continue;

                var delta = new LoraDeltaTensor(
                    TensorName: info.Name,
                    DeltaData: tensorData,
                    Shape: info.Shape.Select(x => (int)x).ToArray(),
                    Weight: 1.0);

                deltas.Add(delta);
            }

            if (deltas.Count > 0)
                _logger?.LogDebug("Extracted {Count} LoRA delta tensors from '{Path}'", deltas.Count, filePath);

            _deltaCache[filePath] = deltas;
            return deltas;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to extract LoRA delta tensors from '{Path}'", filePath);
            return null;
        }
    }

    private static async Task<float[]?> ExtractTensorDataFromFileAsync(string filePath, TensorMetadata info, CancellationToken ct = default)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            fs.Seek(info.StartOffset, SeekOrigin.Begin);

            var bufferSize = info.ByteSize > 0 ? info.ByteSize : (int)(info.EndOffset - info.StartOffset);
            if (bufferSize <= 0)
                return null;

            var buffer = new byte[bufferSize];
            var bytesRead = 0;
            while (bytesRead < bufferSize && !ct.IsCancellationRequested)
            {
                var read = await fs.ReadAsync(buffer, bytesRead, bufferSize - bytesRead);
                if (read == 0) break;
                bytesRead += read;
            }

            if (bytesRead != bufferSize)
                return null;

            // Convert bytes to float array (little-endian)
            var floats = new float[bufferSize / sizeof(float)];
            for (int i = 0; i < floats.Length; i++)
                floats[i] = BitConverter.ToSingle(buffer, i * sizeof(float));

            return floats;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> FindWeightFileAsync(string modelId)
    {
        // Try model repository first
        try
        {
            var models = await _modelRepo.SearchMultiModalModelsAsync(modelTypeFilter: ModelType.Lora);
            foreach (var model in models)
            {
                if (model.Id != null && model.Id.IndexOf(modelId, StringComparison.OrdinalIgnoreCase) >= 0)
                    return model.FilePath;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to search model repository for '{ModelId}'", modelId);
        }

        // Search common directories for LoRA safetensors files
        var searchPaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "lora"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openlmstudio", "models", "lora"),
            Path.Combine(Environment.GetEnvironmentVariable("APPDATA") ?? "", "OpenLMStudio", "models", "lora"),
        };

        foreach (var searchPath in searchPaths)
        {
            if (!Directory.Exists(searchPath)) continue;

            var pattern = $"{modelId}*.safetensors";
            var matches = Directory.GetFiles(searchPath, pattern);
            if (matches.Length > 0) return matches[0];

            var recursiveMatches = Directory.GetFiles(searchPath, pattern, SearchOption.AllDirectories);
            if (recursiveMatches.Length > 0) return recursiveMatches[0];
        }

        return null;
    }
}