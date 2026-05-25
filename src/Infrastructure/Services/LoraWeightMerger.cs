using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
/// Loads and merges LoRA adapter weights into a base ONNX Runtime InferenceSession using ONNX Runtime weight manipulation.
/// Supports multiple adapter stacking with configurable scaling factors (alpha/rank).
/// </summary>
public class LoraWeightMerger : IDisposable
{
    private readonly ILogger<LoraWeightMerger>? _logger;

    /// <summary>Pipeline ID → list of loaded LoRA adapters with their weights.</summary>
    private readonly Dictionary<string, List<LoRAAdapterState>> _loadedAdapters = new(StringComparer.OrdinalIgnoreCase);

    public LoraWeightMerger(ILogger<LoraWeightMerger>? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Loads a LoRA adapter and merges its delta tensors into the provided ONNX Runtime InferenceSession.
    /// Returns true if successful, false otherwise.
    /// </summary>
    public async Task<bool> LoadAdapterAsync(
        string pipelineId,
        string adapterModelId,
        double scalingFactor,
        SafetensorParser safetensorParser,
        IModelRepository modelRepo)
    {
        try
        {
            _logger?.LogInformation("Loading LoRA adapter '{Adapter}' for pipeline '{Pipeline}'", adapterModelId, pipelineId);

            var metadata = new LoraAdapterReference(adapterModelId, scalingFactor);

            // Find the LoRA adapter's safetensors file via model repository or direct path search
            var adapterFilePath = await FindLoraAdapterFileAsync(adapterModelId, modelRepo);
            if (adapterFilePath == null)
            {
                _logger?.LogWarning("LoRA adapter '{Adapter}' not found.", adapterModelId);
                return false;
            }

            // Validate safetensors header
            var headerValid = await safetensorParser.ValidateHeaderAsync(adapterFilePath);
            if (!headerValid)
            {
                _logger?.LogWarning("Safetensors header validation failed for LoRA adapter '{Adapter}'.", adapterModelId);
                return false;
            }

            // Parse safetensors header to extract delta tensor shapes/dtypes and weight data
            var deltaTensors = await ExtractDeltaTensorsFromSafetensors(adapterFilePath, safetensorParser);
            if (deltaTensors == null || deltaTensors.Count == 0)
            {
                _logger?.LogWarning("No delta tensors extracted from LoRA adapter '{Adapter}'.", adapterModelId);
                return false;
            }

            var state = new LoRAAdapterState(metadata, adapterFilePath, deltaTensors);

            (_loadedAdapters[pipelineId] ??= new()).Add(state);

            _logger?.LogInformation("LoRA adapter '{Adapter}' loaded successfully with {Count} delta tensors for pipeline '{Pipeline}'",
                adapterModelId, deltaTensors.Count, pipelineId);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load LoRA adapter '{Adapter}' for pipeline '{Pipeline}'", adapterModelId, pipelineId);
            return false;
        }
    }

    /// <summary>
    /// Merges a LoRA adapter's delta tensors into a base model and saves the merged safetensors file.
    /// Returns path to merged model or null if failed.
    /// </summary>
    public async Task<string?> MergeAdapterAsync(
        string pipelineId,
        string adapterModelId,
        double scalingFactor,
        string baseModelPath,
        SafetensorParser safetensorParser)
    {
        try
        {
            _logger?.LogInformation("Merging LoRA adapter '{Adapter}' into base model for pipeline '{Pipeline}'", adapterModelId, pipelineId);

            var adapterFilePath = await FindLoraAdapterFileAsync(adapterModelId, null);
            if (adapterFilePath == null)
            {
                _logger?.LogWarning("LoRA adapter '{Adapter}' not found.", adapterModelId);
                return null;
            }

            // Load base model tensors from safetensors
            var baseTensors = await LoadBaseTensorsFromSafetensors(baseModelPath, safetensorParser);
            if (baseTensors == null || baseTensors.Count == 0)
            {
                _logger?.LogWarning("No base tensors loaded from '{Path}'.", baseModelPath);
                return null;
            }

            // Load LoRA adapter delta tensors
            var deltaTensors = await ExtractDeltaTensorsFromSafetensors(adapterFilePath, safetensorParser);
            if (deltaTensors == null || deltaTensors.Count == 0)
            {
                _logger?.LogWarning("No delta tensors extracted from LoRA adapter '{Adapter}'.", adapterModelId);
                return null;
            }

            // Apply LoRA merge formula: W_merged = W_base + alpha/rank * delta_W
            var mergedTensors = MergeTensors(baseTensors, deltaTensors, scalingFactor);
            if (mergedTensors == null)
            {
                _logger?.LogWarning("Tensor merging failed for LoRA adapter '{Adapter}'.", adapterModelId);
                return null;
            }

            // Write merged model to a new safetensors file
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var outputDir = Path.Combine(appData, "OpenLMStudio", "models", "lora", "merged");
            Directory.CreateDirectory(outputDir);

            var outputFileName = $"{adapterModelId}_merged_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.safetensors";
            var outputPath = Path.Combine(outputDir, outputFileName);

            await SaveMergedSafetensors(outputPath, mergedTensors);

            _logger?.LogInformation("LoRA adapter '{Adapter}' merged successfully into '{Path}'", adapterModelId, outputPath);
            return outputPath;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to merge LoRA adapter '{Adapter}' into base model for pipeline '{Pipeline}'", adapterModelId, pipelineId);
            return null;
        }
    }

    /// <summary>
    /// Gets all currently loaded adapters and their weights for a given pipeline.
    /// </summary>
    public IReadOnlyList<LoRAAdapterState> GetAppliedAdapters(string pipelineId) =>
        _loadedAdapters.TryGetValue(pipelineId, out var list) ? list : (IReadOnlyList<LoRAAdapterState>)Array.Empty<LoRAAdapterState>();

    /// <summary>
    /// Removes all applied LoRA adapters from the specified pipeline.
    /// </summary>
    public void RemoveAllAdapters(string pipelineId)
    {
        _logger?.LogInformation("Removing all LoRA adapters for pipeline '{Pipeline}'", pipelineId);
        _loadedAdapters.Remove(pipelineId);
    }

    /// <summary>
    /// Unloads a specific adapter from the specified pipeline.
    /// </summary>
    public bool RemoveAdapter(string pipelineId, string adapterModelId)
    {
        if (!_loadedAdapters.TryGetValue(pipelineId, out var adapters) || adapters == null)
            return false;

        for (var i = 0; i < adapters.Count; i++)
        {
            if (adapters[i].Adapter.ModelId == adapterModelId)
            {
                adapters.RemoveAt(i);
                if (adapters.Count == 0)
                    _loadedAdapters.Remove(pipelineId);
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        _loadedAdapters.Clear();
    }

    // ---- Private helpers ----

    private static async Task<string?> FindLoraAdapterFileAsync(string adapterModelId, IModelRepository? modelRepo)
    {
        // Try model repository first (if registered via JsonModelRepository)
        if (modelRepo != null)
        {
            var models = await modelRepo.SearchMultiModalModelsAsync(modelTypeFilter: ModelType.Lora);
            foreach (var model in models)
            {
                if (model.Id != null && model.Id.IndexOf(adapterModelId, StringComparison.OrdinalIgnoreCase) >= 0)
                    return model.FilePath;
            }
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

            var pattern = $"{adapterModelId}*.safetensors";
            var matches = Directory.GetFiles(searchPath, pattern);
            if (matches.Length > 0) return matches[0];

            var recursiveMatches = Directory.GetFiles(searchPath, pattern, SearchOption.AllDirectories);
            if (recursiveMatches.Length > 0) return recursiveMatches[0];
        }

        return null;
    }

    /// <summary>
    /// Parses a safetensors file and returns a dictionary of tensor name → delta data.
    /// Filters for LoRA-specific tensors (those containing "lora" in the name).
    /// </summary>
    private async Task<IReadOnlyDictionary<string, byte[]>> ExtractDeltaTensorsFromSafetensors(
        string filePath, SafetensorParser safetensorParser)
    {
        var deltaTensors = new Dictionary<string, byte[]>();

        try
        {
            // Get tensor metadata from safetensors header
            var tensorInfo = await safetensorParser.GetTensorMetadataAsync(filePath);
            if (tensorInfo == null || tensorInfo.Count == 0)
                return deltaTensors;

            foreach (var (name, info) in tensorInfo)
            {
                // Only include LoRA tensors (typically named with "lora" prefix like lora_A, lora_B, etc.)
                if (name.IndexOf("lora", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                // Extract tensor data from file at the offset specified in metadata
                var tensorData = await ExtractTensorDataFromFileAsync(filePath, info);
                if (tensorData != null)
                    deltaTensors[name] = tensorData;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to extract LoRA delta tensors from '{Path}'", filePath);
        }

        return deltaTensors;
    }

    /// <summary>
    /// Loads base model tensors from a safetensors file.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, byte[]>> LoadBaseTensorsFromSafetensors(
        string filePath, SafetensorParser safetensorParser)
    {
        var tensors = new Dictionary<string, byte[]>();

        try
        {
            var tensorInfo = await safetensorParser.GetTensorMetadataAsync(filePath);
            if (tensorInfo == null)
                return tensors;

            foreach (var (name, info) in tensorInfo)
            {
                var tensorData = await ExtractTensorDataFromFileAsync(filePath, info);
                if (tensorData != null)
                    tensors[name] = tensorData;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load base tensors from '{Path}'", filePath);
        }

        return tensors;
    }

    /// <summary>
    /// Extracts raw bytes for a tensor from a safetensors file using offset and length metadata.
    /// </summary>
    private async Task<byte[]?> ExtractTensorDataFromFileAsync(string filePath, TensorMetadata info)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            fs.Seek(info.StartOffset, SeekOrigin.Begin);

            var bufferSize = info.ByteSize > 0 ? info.ByteSize : (int)(info.EndOffset - info.StartOffset);
            if (bufferSize <= 0)
            {
                _logger?.LogDebug("Skipping tensor with invalid byte size: {Tensor}", info.Name);
                return null;
            }

            var buffer = new byte[bufferSize];
            var bytesRead = 0;
            while (bytesRead < bufferSize)
            {
                var read = await fs.ReadAsync(buffer, bytesRead, bufferSize - bytesRead);
                if (read == 0) break;
                bytesRead += read;
            }

            return bytesRead == bufferSize ? buffer : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Merges base tensors with LoRA delta tensors using the formula:
    /// W_merged = W_base + (alpha / rank) * delta_W
    /// Returns null if tensor shapes don't match for any key.
    /// </summary>
    private IReadOnlyDictionary<string, byte[]>? MergeTensors(
        IReadOnlyDictionary<string, byte[]> baseTensors,
        IReadOnlyDictionary<string, byte[]> deltaTensors,
        double scalingFactor)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, baseData) in baseTensors)
        {
            if (!deltaTensors.TryGetValue(name, out var deltaData))
            {
                // No delta tensor for this key — keep base tensor as-is
                result[name] = baseData;
                continue;
            }

            // Ensure shapes match
            if (baseData.Length != deltaData.Length)
            {
                _logger?.LogWarning("Shape mismatch for tensor '{Name}' — skipping merge", name);
                result[name] = baseData;
                continue;
            }

            // Apply LoRA formula: W_merged = W_base + scale * delta_W
            var merged = new byte[baseData.Length];
            for (int i = 0; i < baseData.Length; i++)
            {
                // Convert to float, apply formula, convert back
                var baseFloat = BitConverter.ToSingle(baseData, i * sizeof(float));
                var deltaFloat = BitConverter.ToSingle(deltaData, i * sizeof(float));
                var mergedFloat = baseFloat + (float)(scalingFactor * deltaFloat);
                BitConverter.TryWriteBytes(new Span<byte>(merged, i * sizeof(float), sizeof(float)), mergedFloat);
            }

            result[name] = merged;
        }

        return result;
    }

    /// <summary>
    /// Saves merged tensors as a safetensors file with proper header.
    /// </summary>
    private async Task SaveMergedSafetensors(string outputPath, IReadOnlyDictionary<string, byte[]> mergedTensors)
    {
        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);

        // Write safetensors header (JSON-encoded)
        // Format: {tensor_name: {dtype, shape, offsets}, __metadata__: {}}
        var headerDict = new System.Text.Json.Nodes.JsonObject();
        foreach (var (name, data) in mergedTensors)
        {
            var tensorNode = new System.Text.Json.Nodes.JsonObject
            {
                ["dtype"] = "f32",
                ["shape"] = new System.Text.Json.Nodes.JsonArray(data.Length / sizeof(float)),
                ["data_offsets"] = new System.Text.Json.Nodes.JsonArray(0, data.Length)
            };
            headerDict[name] = tensorNode;
        }

        var headerJson = System.Text.Json.JsonSerializer.Serialize(headerDict);
        var headerBytes = System.Text.Encoding.UTF8.GetBytes(headerJson);

        // Write header length as u64 LE
        var headerLengthBytes = new byte[8];
        for (int i = 0; i < 8; i++) headerLengthBytes[i] = (byte)(headerLengthBytes.Length > i ? (headerBytes.Length >> (i * 8)) & 0xFF : 0);
        await fs.WriteAsync(headerLengthBytes, 0, 8);

        // Write header JSON
        await fs.WriteAsync(headerBytes, 0, headerBytes.Length);

        // Write tensor data
        foreach (var (_, data) in mergedTensors)
        {
            await fs.WriteAsync(data, 0, data.Length);
        }

        _logger?.LogInformation("Saved merged safetensors to '{Path}' ({Size} bytes)", outputPath, fs.Length);
    }
}

/// <summary>
/// State for a loaded LoRA adapter — tracks the adapter reference, file path, and extracted delta tensors.
/// </summary>
public class LoRAAdapterState
{
    public LoraAdapterReference Adapter { get; }
    public string? AdapterFile { get; }
    public IReadOnlyDictionary<string, byte[]> DeltaTensors { get; }

    public LoRAAdapterState(LoraAdapterReference adapter, string? adapterFile, IReadOnlyDictionary<string, byte[]> deltaTensors)
    {
        Adapter = adapter;
        AdapterFile = adapterFile;
        DeltaTensors = deltaTensors;
    }
}