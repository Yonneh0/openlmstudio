using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Wandering tool for the Pingu system AI.
/// Allows Pingu to perform autonomous exploratory behavior when idle.
/// </summary>
public class PinguWanderingTool : ITool, IDisposable
{
    private readonly ILogger<PinguWanderingTool>? _logger;
    private bool _disposed;

    public string Name => "PinguWander";
    public string Description => "Performs autonomous wandering/exploratory behavior. Pingu can inspect files, check git status, or explore the project structure.";

    public PinguWanderingTool(ILogger<PinguWanderingTool>? logger = null)
    {
        _logger = logger;
    }

    public async Task<bool> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (_disposed) return false;

        var action = TryGetString(parameters, "Action");
        var target = TryGetString(parameters, "Target");

        if (string.IsNullOrEmpty(action))
        {
            _logger?.LogWarning("PinguWander called without Action parameter.");
            return false;
        }

        _logger?.LogInformation("Pingu wandering: {Action} {Target}", action, target ?? "none");

        switch (action.ToLowerInvariant())
        {
            case "explore":
                // Trigger project structure exploration
                _logger?.LogDebug("Wandering: exploring project structure at {Target}", target ?? "current");
                break;
            case "inspect":
                // Trigger file inspection
                _logger?.LogDebug("Wandering: inspecting {Target}", target ?? "current file");
                break;
            case "check_git":
                // Trigger git status check
                _logger?.LogDebug("Wandering: checking git status");
                break;
            case "rest":
                _logger?.LogDebug("Wandering: resting");
                break;
            default:
                _logger?.LogWarning("Unknown wander action: {Action}", action);
                return false;
        }

        return true;
    }

    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new()
    {
        ["Action"] = new ToolParameterSchema("string", true),
        ["Target"] = new ToolParameterSchema("string", false),
    };

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    private static string? TryGetString(Dictionary<string, object> dict, string key) =>
        dict.TryGetValue(key, out var value) ? Convert.ToString(value) : null;
}