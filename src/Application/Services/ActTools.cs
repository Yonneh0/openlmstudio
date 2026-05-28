namespace OpenLMStudio.Application.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenLMStudio.Domain.Interfaces;
using OpenLMStudio.Domain.Models;

// ============================================================================
// ActModeRespond
// ============================================================================

/// <summary>
/// Stub implementation for act_mode_respond tool.
/// Only available in ACT MODE.
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
                with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
    }
}

// ============================================================================
// AttemptCompletion
// ============================================================================

/// <summary>
/// Stub implementation for attempt_completion tool.
/// Presents the final result to the user.
/// </summary>
public class AttemptCompletion
{
    private readonly ILogger<AttemptCompletion> _logger;

    public AttemptCompletion(ILogger<AttemptCompletion>? logger = null)
    {
        _logger = logger ?? NullLogger<AttemptCompletion>.Instance;
    }

    /// <summary>
    /// Presents the final result to the user.
    /// </summary>
    public async Task<ToolResult> CompleteAsync(string result, string? command = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(result))
                return ToolResult.Fail("Missing required parameter: result");

            var output = $"Task completed successfully:\n\n{result.Trim()}";

            if (!string.IsNullOrEmpty(command))
                output += $"\n\n[Command to run: {command}]";

            stopwatch.Stop();
            _logger?.LogInformation("attempt_completion: {Result}", result);

            return ToolResult.Ok(output) with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ToolResult.Fail($"Error in attempt_completion: {ex.Message}")
                with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
    }
}

// ============================================================================
// PlanModeRespond
// ============================================================================

/// <summary>
/// Stub implementation for plan_mode_respond tool.
/// Only available in PLAN MODE.
/// </summary>
public class PlanModeRespond
{
    private readonly ILogger<PlanModeRespond> _logger;

    public PlanModeRespond(ILogger<PlanModeRespond>? logger = null)
    {
        _logger = logger ?? NullLogger<PlanModeRespond>.Instance;
    }

    /// <summary>
    /// Responds to the user's inquiry in plan mode.
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

// ============================================================================
// QuestionService
// ============================================================================

/// <summary>
/// Implements ask_followup_question tool for interactive questions.
/// Fully implemented with option support.
/// </summary>
public class QuestionService
{
    private readonly ILogger<QuestionService> _logger;

    public QuestionService(ILogger<QuestionService>? logger = null)
    {
        _logger = logger ?? NullLogger<QuestionService>.Instance;
    }

    /// <summary>
    /// Asks the user a question with optional options.
    /// </summary>
    public async Task<ToolResult> AskAsync(string question, List<string>? options = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(question))
                return ToolResult.Fail("Missing required parameter: question");

            var output = new System.Text.StringBuilder();
            output.AppendLine(question);

            if (options != null && options.Count > 0)
            {
                output.AppendLine("\nOptions:");
                for (var i = 0; i < options.Count; i++)
                    output.AppendLine($"  {(i + 1)}. {options[i]}");
            }

            var result = output.ToString().Trim();
            result += "\n\n[Please respond with your selection.]";

            stopwatch.Stop();
            _logger?.LogInformation("ask_followup_question: {Question}", question);

            return ToolResult.Ok(result) with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ToolResult.Fail($"Error asking question: {ex.Message}")
                with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
    }
}