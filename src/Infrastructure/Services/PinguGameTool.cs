using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Game control tool for the Pingu system AI.
/// Allows Pingu to launch and manage built-in games.
/// </summary>
public class PinguGameTool : ITool, IDisposable
{
    private readonly ILogger<PinguGameTool>? _logger;
    private readonly IGamesPanel? _gamesPanel;
    private bool _disposed;

    public string Name => "PinguGame";
    public string Description => "Launches or stops a built-in game (Minesweeper, Tetris, Snake, Jezzball, Solitaire).";

    public PinguGameTool(ILogger<PinguGameTool>? logger, IGamesPanel? gamesPanel = null)
    {
        _logger = logger;
        _gamesPanel = gamesPanel;
    }

    public async Task<bool> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (_disposed) return false;

        var action = TryGetString(parameters, "Action");
        var game = TryGetString(parameters, "Game");

        if (string.IsNullOrEmpty(action) || string.IsNullOrEmpty(game))
        {
            _logger?.LogWarning("PinguGame called without required parameters.");
            return false;
        }

        _logger?.LogInformation("Pingu {Action}ing game: {Game}", action, game);

        if (_gamesPanel != null)
        {
            if (action.Equals("launch", StringComparison.OrdinalIgnoreCase))
            {
                _gamesPanel.ActivateGame(game);
                return true;
            }

            if (action.Equals("stop", StringComparison.OrdinalIgnoreCase))
            {
                _gamesPanel.ActivateGame("none");
                return true;
            }
        }
        else
        {
            _logger?.LogDebug("PinguGame: IGamesPanel not available — logged as informational only.");
        }

        return false;
    }

    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new()
    {
        ["Action"] = new ToolParameterSchema("string", true),
        ["Game"] = new ToolParameterSchema("string", true),
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