using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

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