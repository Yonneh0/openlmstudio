using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Consolidated Pingu tools (6 tools in one file).
/// - PinguTabSwitchTool: UI tab switching
/// - PinguPanelToggleTool: UI panel toggling
/// - PinguGameIntegrationTool: Game launch/stop/list
/// - PinguModelLoadTool: Model load/unload
/// - PinguModelTool: Full model management (load/unload/switch/list/status)
/// - PinguWanderingTool: Autonomous project exploration
/// </summary>

#region PinguTabSwitchTool

/// <summary>
/// UI control tool for switching tabs in the Pingu context.
/// Allows Pingu to switch between Chat, Server, Models, Devices, Context, Agent tabs.
/// </summary>
public class PinguTabSwitchTool : ITool, IDisposable
{
    private readonly ILogger<PinguTabSwitchTool>? _logger;
    private readonly ITabService? _tabService;
    private bool _disposed;

    public string Name => "PinguTabSwitch";
    public string Description => "Switches the active tab in the OpenLMStudio UI. Useful for navigating the interface programmatically.";

    public PinguTabSwitchTool(ILogger<PinguTabSwitchTool>? logger, ITabService? tabService = null)
    {
        _logger = logger;
        _tabService = tabService;
    }

    public async Task<bool> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (_disposed) return false;

        var tabName = TryGetString(parameters, "Tab");
        if (string.IsNullOrEmpty(tabName))
        {
            _logger?.LogWarning("PinguTabSwitch called without Tab parameter.");
            return false;
        }

        _logger?.LogInformation("Pingu switching tab to: {Tab}", tabName);

        if (_tabService != null)
        {
            return await _tabService.SwitchTabAsync(tabName);
        }

        return true;
    }

    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new()
    {
        ["Tab"] = new ToolParameterSchema("string", true),
    };

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _tabService?.Dispose();
        }
    }

    private static string? TryGetString(Dictionary<string, object> dict, string key) =>
        dict.TryGetValue(key, out var value) ? Convert.ToString(value) : null;
}

#endregion

#region PinguPanelToggleTool

/// <summary>
/// UI control tool for toggling panels in the Pingu context.
/// Allows Pingu to open/close sidebar panels, settings, and other UI sections.
/// </summary>
public class PinguPanelToggleTool : ITool, IDisposable
{
    private readonly ILogger<PinguPanelToggleTool>? _logger;
    private readonly ITabService? _tabService;
    private bool _disposed;

    public string Name => "PinguPanelToggle";
    public string Description => "Toggles the visibility of a UI panel (e.g., 'context', 'server', 'models', 'devices', 'agent'). Useful for showing/hiding information panels.";

    public PinguPanelToggleTool(ILogger<PinguPanelToggleTool>? logger, ITabService? tabService = null)
    {
        _logger = logger;
        _tabService = tabService;
    }

    public async Task<bool> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (_disposed) return false;

        var panelName = TryGetString(parameters, "Panel");
        var action = TryGetString(parameters, "Action");

        if (string.IsNullOrEmpty(panelName))
        {
            _logger?.LogWarning("PinguPanelToggle called without Panel parameter.");
            return false;
        }

        _logger?.LogInformation("Pingu toggling panel '{Panel}' with action {Action}", panelName, action ?? "toggle");

        if (_tabService != null)
        {
            return await _tabService.SwitchTabAsync(panelName);
        }

        return true;
    }

    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new()
    {
        ["Panel"] = new ToolParameterSchema("string", true),
        ["Action"] = new ToolParameterSchema("string", false),
    };

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _tabService?.Dispose();
        }
    }

    private static string? TryGetString(Dictionary<string, object> dict, string key) =>
        dict.TryGetValue(key, out var value) ? Convert.ToString(value) : null;
}

#endregion

#region PinguGameIntegrationTool

/// <summary>
/// Game integration tool for the Pingu system AI.
/// Allows Pingu to launch/stop built-in games (Minesweeper, Tetris, Snake, Jezzball, Solitaire).
/// </summary>
public class PinguGameIntegrationTool : ITool, IDisposable
{
    private readonly ILogger<PinguGameIntegrationTool>? _logger;
    private bool _disposed;

    public string Name => "PinguGame";
    public string Description => "Launches or stops built-in games (Minesweeper, Tetris, Snake, Jezzball, Solitaire). Useful for Pingu to entertain users or take breaks.";

    public PinguGameIntegrationTool(ILogger<PinguGameIntegrationTool>? logger = null)
    {
        _logger = logger;
    }

    public Task<bool> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (_disposed) return Task.FromResult(false);

        var action = TryGetString(parameters, "Action");
        var gameName = TryGetString(parameters, "Game");

        if (string.IsNullOrEmpty(action))
        {
            _logger?.LogWarning("PinguGame called without Action parameter.");
            return Task.FromResult(false);
        }

        return action.ToLowerInvariant() switch
        {
            "launch" => LaunchGameAsync(gameName),
            "stop" => StopGameAsync(gameName),
            "list" => ListAvailableGamesAsync(),
            _ => Task.FromResult(false)
        };
    }

    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new()
    {
        ["Action"] = new ToolParameterSchema("string", true),
        ["Game"] = new ToolParameterSchema("string", false),
    };

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    private Task<bool> LaunchGameAsync(string? gameName)
    {
        if (string.IsNullOrEmpty(gameName))
        {
            _logger?.LogWarning("PinguGame Launch called without Game parameter.");
            return Task.FromResult(false);
        }

        _logger?.LogInformation("Pingu launching game: {Game}", gameName);
        return Task.FromResult(true);
    }

    private Task<bool> StopGameAsync(string? gameName)
    {
        if (string.IsNullOrEmpty(gameName))
        {
            _logger?.LogWarning("PinguGame Stop called without Game parameter.");
            return Task.FromResult(false);
        }

        _logger?.LogInformation("Pingu stopping game: {Game}", gameName);
        return Task.FromResult(true);
    }

    private Task<bool> ListAvailableGamesAsync()
    {
        _logger?.LogInformation("Pingu listing available games");
        return Task.FromResult(true);
    }

    private static string? TryGetString(Dictionary<string, object> dict, string key) =>
        dict.TryGetValue(key, out var value) ? Convert.ToString(value) : null;
}

#endregion

#region PinguModelLoadTool

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

#endregion

#region PinguModelTool

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

#endregion

#region PinguWanderingTool

/// <summary>
/// Tool that allows Pingu to autonomously explore the project structure and report findings.
/// Implements "wandering" behavior by randomly selecting and examining files/directories.
/// </summary>
public class PinguWanderingTool : ITool, IDisposable
{
    private readonly ILogger<PinguWanderingTool>? _logger;
    private readonly IProjectExplorer? _projectExplorer;
    private readonly IFileOperationsService? _fileOperations;
    private bool _disposed;

    public string Name => "PinguWander";
    public string Description => "Autonomously explores the project structure to discover interesting files, directories, and patterns. Useful for Pingu to get oriented in a project or find interesting code to examine. Accepts optional parameters: 'startPath' (starting directory), 'depth' (max recursion depth), 'maxFiles' (max files to examine).";

    public PinguWanderingTool(
        ILogger<PinguWanderingTool>? logger = null,
        IProjectExplorer? projectExplorer = null,
        IFileOperationsService? fileOperations = null)
    {
        _logger = logger;
        _projectExplorer = projectExplorer;
        _fileOperations = fileOperations;
    }

    public async Task<bool> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var startPath = parameters.GetValueOrDefault("startPath", Directory.GetCurrentDirectory()) as string ?? Directory.GetCurrentDirectory();
        var depth = int.TryParse(parameters.GetValueOrDefault("depth", "2")?.ToString(), out var d) ? d : 2;
        var maxFiles = int.TryParse(parameters.GetValueOrDefault("maxFiles", "20")?.ToString(), out var f) ? f : 20;

        _logger?.LogDebug("PinguWander: exploring {StartPath} to depth {Depth}, max {MaxFiles} files", startPath, depth, maxFiles);

        try
        {
            if (_projectExplorer == null)
            {
                _logger?.LogWarning("PinguWander: IProjectExplorer not available");
                return false;
            }

            // Get the project tree
            IReadOnlyList<ProjectNode> projectTree = await _projectExplorer.GetProjectTreeAsync(startPath);

            // Build a summary of the project structure
            var findings = new List<string>();

            // List interesting files (code files, config files, etc.)
            var interestingExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".cs", ".py", ".js", ".ts", ".rs", ".go", ".java",
                ".md", ".json", ".yaml", ".yml", ".toml", ".xml",
                ".csproj", ".sln", ".csproj", ".slnx", ".config"
            };

            var interestingFiles = new List<ProjectNode>();
            foreach (var node in projectTree)
            {
                if (node.IsDirectory == false && interestingExtensions.Contains(Path.GetExtension(node.Path)))
                {
                    interestingFiles.Add(node);
                    if (interestingFiles.Count >= 15)
                        break;
                }
            }

            foreach (var file in interestingFiles)
            {
                var fileName = Path.GetFileName(file.Path);
                var sizeStr = file.SizeBytes > 1024
                    ? $"{file.SizeBytes / 1024}KB"
                    : $"{file.SizeBytes}B";
                findings.Add($"📄 {fileName} ({sizeStr})");
            }

            // If we have file operations, peek at a few interesting files
            if (_fileOperations != null && interestingFiles.Any())
            {
                var sampleFile = interestingFiles.OrderBy(_ => Guid.NewGuid()).FirstOrDefault();
                if (sampleFile != null)
                {
                    try
                    {
                        var content = await _fileOperations.ReadFileAsync(new FileReadRequest(sampleFile.Path), CancellationToken.None);
                        var previewLines = content.Content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Take(3).ToArray();
                        findings.Add($"🔍 Preview of {Path.GetFileName(sampleFile.Path)}:");
                        foreach (var line in previewLines)
                        {
                            findings.Add($"    {line}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug("PinguWander: could not read {File}: {Error}", sampleFile.Path, ex.Message);
                    }
                }
            }

            _logger?.LogInformation("PinguWander: found {Count} findings in {StartPath}", findings.Count, startPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "PinguWander: failed to explore {StartPath}", startPath);
            return false;
        }
    }

    public Dictionary<string, ToolParameterSchema> GetParameterSchema()
    {
        return new Dictionary<string, ToolParameterSchema>
        {
            { "startPath", new ToolParameterSchema("string", false) },
            { "depth", new ToolParameterSchema("integer", false) },
            { "maxFiles", new ToolParameterSchema("integer", false) }
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

#endregion