using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Model management tool for loading/unloading models in the Pingu context.
/// Allows Pingu to manage the model lifecycle programmatically.
/// </summary>
public class PinguModelLoadTool : ITool, IDisposable
{
    private readonly ILogger<PinguModelLoadTool>? _logger;
    private readonly IModelManager? _modelManager;
    private readonly IModelRepository? _modelRepository;
    private bool _disposed;

    public string Name => "PinguModelLoad";
    public string Description => "Loads or unloads a model into/from memory for inference.";

    public PinguModelLoadTool(ILogger<PinguModelLoadTool>? logger, IModelManager? modelManager = null, IModelRepository? modelRepository = null)
    {
        _logger = logger;
        _modelManager = modelManager;
        _modelRepository = modelRepository;
    }

    public async Task<bool> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (_disposed) return false;

        var action = TryGetString(parameters, "Action");
        var modelId = TryGetString(parameters, "ModelId");
        var modelType = TryGetString(parameters, "ModelType");

        if (string.IsNullOrEmpty(action) || string.IsNullOrEmpty(modelId))
        {
            _logger?.LogWarning("PinguModelLoad called without required parameters.");
            return false;
        }

        _logger?.LogInformation("Pingu {Action}ing model: {ModelId}", action, modelId);

        if (_modelManager != null)
        {
            switch (action.ToLowerInvariant())
            {
                case "load":
                    {
                        var type = string.IsNullOrEmpty(modelType) ? ModelType.TextGeneration :
                            modelType.ToLowerInvariant() switch
                            {
                                "image" => ModelType.ImageGeneration,
                                "diffusion" => ModelType.Diffusion,
                                "vae" => ModelType.Vae,
                                "lora" => ModelType.Lora,
                                "embedding" => ModelType.Embedding,
                                _ => ModelType.TextGeneration
                            };
                        await _modelManager.LoadModelAsync(modelId, type);
                        return true;
                    }
                case "unload":
                    await _modelManager.UnloadModelByIdAsync(modelId);
                    return true;
                case "unloadall":
                    await _modelManager.UnloadAllModelsAsync();
                    return true;
                case "list":
                    _logger?.LogInformation("Pingu listing loaded models: {Count}", _modelManager.LoadedCount);
                    return true;
                default:
                    _logger?.LogWarning("Unknown model action: {Action}", action);
                    return false;
            }
        }

        return true;
    }

    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new()
    {
        ["Action"] = new ToolParameterSchema("string", true),
        ["ModelId"] = new ToolParameterSchema("string", true),
        ["ModelType"] = new ToolParameterSchema("string", false),
    };

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _modelManager?.Dispose();
        }
    }

    private static string? TryGetString(Dictionary<string, object> dict, string key) =>
        dict.TryGetValue(key, out var value) ? Convert.ToString(value) : null;
}
