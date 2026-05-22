using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;
using TaskStatus = OpenLMStudio.Domain.Models.TaskStatus;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Detects when an agent task is complete based on tool results and task state.
/// Delegates to TaskValidationService for AI-powered completion checks.
/// </summary>
public class TaskCompletionDetector : ITaskCompletionDetector
{
    private readonly ILogger<TaskCompletionDetector> _logger;
    private readonly ITaskValidationService _validationService;

    public TaskCompletionDetector(
        ILogger<TaskCompletionDetector> logger,
        ITaskValidationService validationService)
    {
        _logger = logger;
        _validationService = validationService;
    }

    public async Task<TaskValidationResult> DetectCompletionAsync(
        AgenticTask task,
        IReadOnlyList<AgentToolCallRecord> toolCalls,
        CancellationToken ct = default)
    {
        if (toolCalls.Count == 0)
        {
            _logger.LogDebug("No tool calls to evaluate for task {TaskId}", task.Id);
            return new TaskValidationResult(false, "No tool calls made — task may not have executed.");
        }

        // Build a summary from the tool call history
        var lastFew = toolCalls.TakeLast(10).ToList();
        var summaryLines = new List<string>();
        foreach (var call in lastFew)
        {
            summaryLines.Add($"- {call.ToolName}: {(call.Success ? "OK" : $"FAILED ({call.Result})")} [{call.DurationMs:F0}ms]");
        }
        var summary = string.Join("\n", summaryLines);

        // Use AI-powered validation
        return await _validationService.ValidateTaskCompletionAsync(task, summary, ct);
    }

    /// <summary>
    /// Quick heuristic check for obvious completion signals.
    /// Used as a pre-filter before calling the AI-powered validator.
    /// </summary>
    public static TaskValidationResult QuickHeuristicCheck(AgenticTask task, IReadOnlyList<AgentToolCallRecord> toolCalls)
    {
        if (task.Status == TaskStatus.Completed)
            return new TaskValidationResult(true, "Task already marked as completed.");

        if (task.Status == TaskStatus.Failed)
            return new TaskValidationResult(false, $"Task failed: {task.ErrorMessage}");

        if (toolCalls.Count >= task.MaxIterations)
            return new TaskValidationResult(false, $"Max iterations ({task.MaxIterations}) reached without completion.");

        // Check for obvious error patterns in recent tool calls
        var recentErrors = toolCalls.TakeLast(5).Count(c => !c.Success);
        if (recentErrors >= 3)
            return new TaskValidationResult(false, $"Too many recent errors ({recentErrors} failures in last 5 calls).");

        return new TaskValidationResult(false, "No completion signal detected — continue executing.");
    }
}