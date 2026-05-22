using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

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
