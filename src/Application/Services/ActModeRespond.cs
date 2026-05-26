namespace OpenLMStudio.Application.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenLMStudio.Domain.Interfaces;
using OpenLMStudio.Domain.Models;

/// <summary>
/// Stub implementation for act_mode_respond tool.
/// Only available in ACT MODE.
/// 
/// REMAINING WORK:
/// - Implement mode detection (check if agent is in act mode)
/// - Implement consecutive call detection
/// - Return progress update to user
/// </summary>
public class ActModeRespond
{
    private readonly ILogger<ActModeRespond> _logger;
    private bool _lastCallWasActModeRespond;

    public ActModeRespond(ILogger<ActModeRespond>? logger = null)
    {
        _logger = logger ?? NullLogger<ActModeRespond>.Instance;
    }

    /// <summary>
    /// Provides a progress update or preamble in ACT MODE.
    /// 
    /// REMAINING WORK:
    /// - Validate agent is in act mode
    /// - Check consecutive call state
    /// </summary>
    public async Task<ToolResult> RespondAsync(string response)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(response))
                return ToolResult.Fail("Missing required parameter: response");

            if (_lastCallWasActModeRespond)
                return ToolResult.Fail("act_mode_respond must not be called consecutively.");

            _lastCallWasActModeRespond = true;

            var output = $"Progress update: {response.Trim()}";

            stopwatch.Stop();
            _logger?.LogInformation("act_mode_respond: {Response}", response);

            return ToolResult.Ok(output) with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ToolResult.Fail($"Error in act_mode_respond: {ex.Message}")
                with
            { DurationMs = stopwatch.ElapsedMilliseconds };
        }
    }
}
