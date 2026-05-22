using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// UI control tool for toggling panels (sidebar, context panel, etc.) in the Pingu context.
/// </summary>
public class PinguPanelToggleTool : ITool, IDisposable
{
    private readonly ILogger<PinguPanelToggleTool>? _logger;
    private readonly IPanelService? _panelService;
    private bool _disposed;

    public string Name => "PinguPanelToggle";
    public string Description => "Toggles the visibility of a UI panel (e.g., 'LeftSidebar', 'RightSidebar', 'Context', 'Status', 'BottomPane').";

    public PinguPanelToggleTool(ILogger<PinguPanelToggleTool>? logger, IPanelService? panelService = null)
    {
        _logger = logger;
        _panelService = panelService;
    }

    public async Task<bool> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (_disposed) return false;

        var panelName = TryGetString(parameters, "Panel");
        if (string.IsNullOrEmpty(panelName))
        {
            _logger?.LogWarning("PinguPanelToggle called without Panel parameter.");
            return false;
        }

        _logger?.LogInformation("Pingu toggling panel: {Panel}", panelName);

        if (_panelService != null)
        {
            return await _panelService.TogglePanelAsync(panelName);
        }

        return true;
    }

    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new()
    {
        ["Panel"] = new ToolParameterSchema("string", true),
    };

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _panelService?.Dispose();
        }
    }

    private static string? TryGetString(Dictionary<string, object> dict, string key) =>
        dict.TryGetValue(key, out var value) ? Convert.ToString(value) : null;
}