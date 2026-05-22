using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

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