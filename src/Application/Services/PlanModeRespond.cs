namespace OpenLMStudio.Application.Services.Agent;

using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Interfaces;
using OpenLMStudio.Domain.Models;

/// <summary>
/// Stub implementation for plan_mode_respond tool.
/// Only available in PLAN MODE.
/// 
/// REMAINING WORK:
/// - Implement mode detection (check if agent is in plan mode)
/// - Return user message to agent
/// - Support mode switching to ACT MODE
/// </summary>
public class PlanModeRespond
{
    private readonly ILogger<PlanModeRespond> _logger;

    public PlanModeRespond(ILogger<PlanModeRespond>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Responds to the user's inquiry in plan mode.
    /// 
    /// REMAINING WORK:
    /// - Validate agent is in plan mode
    /// - Return response with user message
    /// </summary>
    public async Task<ToolResult> RespondAsync(string response, bool needsMoreExploration = false)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(response))
                return ToolResult.Fail("Missing required parameter: response");

            var output = response.Trim();
            if (needsMoreExploration)
                output += "\n\n[More exploration needed.]";

            stopwatch.Stop();
            _logger?.LogInformation("plan_mode_respond: {Response}", response);

            return ToolResult.Ok(output) with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ToolResult.Fail($"Error in plan_mode_respond: {ex.Message}")
                with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
    }
}
