using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Model management tool for the Pingu system AI.
/// Allows Pingu to load/unload/switch models and query model status.
/// </summary>
public class PinguModelTool : ITool, IDisposable
{
    private readonly ILogger<PinguModelTool>? _logger;
    private readonly IModelRepository? _modelRepository;
    private readonly ModelManager? _modelManager;
    private bool _disposed;

    public string Name => "PinguModel";
    public string Description => "Manages models: load, unload, switch, list loaded, get model info. Useful for Pingu to control which models are active.";

    public PinguModelTool(ILogger<PinguModelTool>? logger, IModelRepository? modelRepository = null, ModelManager? modelManager = null)
    {
        _logger = logger;
        _modelRepository = modelRepository;
        _modelManager = modelManager;
    }

    public async Task<bool> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (_disposed) return false;

        var action = TryGetString(parameters, "Action");
        var modelId = TryGetString(parameters, "ModelId");
        var modelType = TryGetString(parameters, "ModelType");

        if (string.IsNullOrEmpty(action))
        {
            _logger?.LogWarning("PinguModel called without Action parameter.");
            return false;
        }

        switch (action.ToLowerInvariant())
        {
            case "load":
                return await LoadModelAsync(modelId, modelType);
            case "unload":
                return await UnloadModelAsync(modelId);
            case "switch":
                return await SwitchModelAsync(modelId, modelType);
            case "list":
                return await ListModelsAsync(modelType);
            case "status":
                return await GetModelStatusAsync(modelId);
            default:
                _logger?.LogWarning("Unknown PinguModel action: {Action}", action);
                return false;
        }
    }

    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new()
    {
        ["Action"] = new ToolParameterSchema("string", true),
        ["ModelId"] = new ToolParameterSchema("string", false),
        ["ModelType"] = new ToolParameterSchema("string", false),
    };

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    private async Task<bool> LoadModelAsync(string? modelId, string? modelType)
    {
        if (string.IsNullOrEmpty(modelId))
        {
            _logger?.LogWarning("PinguModel Load called without ModelId.");
            return false;
        }

        _logger?.LogInformation("Pingu loading model: {ModelId} (type: {ModelType})", modelId, modelType ?? "auto");

        var parsedType = ParseModelType(modelType);
        if (_modelManager != null)
        {
            await _modelManager.LoadModelAsync(modelId, parsedType);
            return true;
        }

        return true;
    }

    private async Task<bool> UnloadModelAsync(string? modelId)
    {
        if (string.IsNullOrEmpty(modelId))
        {
            _logger?.LogWarning("PinguModel Unload called without ModelId.");
            return false;
        }

        _logger?.LogInformation("Pingu unloading model: {ModelId}", modelId);

        if (_modelManager != null)
        {
            await _modelManager.UnloadModelByIdAsync(modelId);
            return true;
        }

        return true;
    }

    private async Task<bool> SwitchModelAsync(string? modelId, string? modelType)
    {
        if (string.IsNullOrEmpty(modelId))
        {
            _logger?.LogWarning("PinguModel Switch called without ModelId.");
            return false;
        }

        _logger?.LogInformation("Pingu switching to model: {ModelId}", modelId);

        // Unload current active model, then load new one
        if (_modelManager != null)
        {
            var currentType = ParseModelType(modelType);
            await _modelManager.UnloadAllModelsAsync();
            await _modelManager.LoadModelAsync(modelId, currentType);
            return true;
        }

        return true;
    }

    private async Task<bool> ListModelsAsync(string? modelType)
    {
        _logger?.LogInformation("Pingu listing models (type: {ModelType})", modelType ?? "all");

        if (_modelRepository != null)
        {
            var parsedType = ParseModelType(modelType);
            var models = await _modelRepository.SearchMultiModalModelsAsync(modelTypeFilter: parsedType);
            _logger?.LogInformation("Pingu found {Count} models", models.Count());
        }

        return true;
    }

    private async Task<bool> GetModelStatusAsync(string? modelId)
    {
        if (string.IsNullOrEmpty(modelId))
        {
            _logger?.LogWarning("PinguModel Status called without ModelId.");
            return false;
        }

        _logger?.LogInformation("Pingu checking status of model: {ModelId}", modelId);

        if (_modelManager != null)
        {
            var status = _modelManager.GetMemoryReports()
                .FirstOrDefault(r => r.ModelId.Contains(modelId, StringComparison.OrdinalIgnoreCase));
            if (status != null)
            {
                _logger?.LogInformation("Model {ModelId} status: VRAM={GpuVramBytes} bytes, device={Device}",
                    modelId, status.GpuVramBytes, status.Device);
            }
            else
            {
                _logger?.LogInformation("Model {ModelId} status: not found in loaded models", modelId);
            }
        }

        return true;
    }

    private ModelType ParseModelType(string? modelType)
    {
        if (string.IsNullOrEmpty(modelType))
            return ModelType.TextGeneration; // Default

        return modelType.ToLowerInvariant() switch
        {
            "image" or "imagegeneration" => ModelType.ImageGeneration,
            "diffusion" => ModelType.Diffusion,
            "vae" => ModelType.Vae,
            "lora" => ModelType.Lora,
            "embedding" => ModelType.Embedding,
            _ => ModelType.TextGeneration
        };
    }

    private static string? TryGetString(Dictionary<string, object> dict, string key) =>
        dict.TryGetValue(key, out var value) ? Convert.ToString(value) : null;
}