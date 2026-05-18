using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// CommandExecute tool implementation for running shell commands within the agent sandbox.
/// </summary>
public class CommandExecuteTool : ITool, IDisposable
{
    private readonly ILogger<CommandExecuteTool>? _logger;
    private readonly ICommandExecutionService _commandExecutor;
    private bool _disposed;

    public string Name => "CommandExecute";
    public string Description => "Executes a shell command within the sandboxed environment.";

    /// <summary>
    /// Creates a new CommandExecute tool instance.
    /// </summary>
    public CommandExecuteTool(ILogger<CommandExecuteTool>? logger, ICommandExecutionService commandExecutor)
    {
        _logger = logger;
        _commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
    }

    /// <inheritdoc />
    public async Task<bool> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (parameters == null || _disposed) return false;

        try
        {
            var commandValue = TryGetString(parameters, "Command");
            if (string.IsNullOrEmpty(commandValue))
            {
                _logger?.LogWarning("CommandExecute tool called without Command parameter.");
                return false;
            }

            int timeoutSeconds = 120; // Default timeout
            if (parameters.TryGetValue("Timeout", out var timeoutValue) && timeoutValue is long t)
                timeoutSeconds = Convert.ToInt32(t);

            Dictionary<string, string>? envVars = null;
            if (parameters.TryGetValue("EnvironmentVariables", out var envVarValue))
            {
                // Parse environment variables from a dictionary parameter
                envVars = new();
                if (envVarValue is IEnumerable<KeyValuePair<string, object>> pairs)
                {
                    foreach (var pair in pairs)
                        envVars.Add(pair.Key, Convert.ToString(pair.Value) ?? string.Empty);
                }
            }

            // Execute through the command executor with sandbox isolation
            var request = new CommandExecuteRequest(commandValue, envVars, timeoutSeconds);
            await _commandExecutor.ExecuteAsync(request);

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "CommandExecute tool failed to execute command.");
            return false;
        }
    }

    /// <inheritdoc />
    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new Dictionary<string, ToolParameterSchema>
    {
        ["Command"] = new ToolParameterSchema("string", true),  // Required
        ["EnvironmentVariables"] = new ToolParameterSchema("object[]", false),  // Optional — dictionary of key-value pairs
        ["Timeout"] = new ToolParameterSchema("number", false)  // Optional — timeout in seconds (default: 120)
    };

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _commandExecutor.Dispose();
        }
    }

    private static string? TryGetString(Dictionary<string, object> dict, string key) =>
        dict.TryGetValue(key, out var value) ? Convert.ToString(value) : null;
}