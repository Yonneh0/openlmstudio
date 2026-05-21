using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Generates human-readable progress summaries and completion reports for agent tasks.
/// </summary>
public class AgentProgressSummaryService : IAgentProgressSummaryService
{
    private readonly ILogger<AgentProgressSummaryService>? _logger;

    public AgentProgressSummaryService(ILogger<AgentProgressSummaryService>? logger = null)
    {
        _logger = logger;
    }

    public TaskProgressSummary GenerateSummary(
        Guid taskId,
        string taskDescription,
        IReadOnlyList<AgentToolCallRecord> toolCalls,
        IReadOnlyList<string> completedPhases,
        DateTime startedAt,
        DateTime? completedAt = null)
    {
        var totalCalls = toolCalls.Count;
        var successfulCalls = toolCalls.Count(c => c.Success);
        var failedCalls = totalCalls - successfulCalls;

        var summary = BuildSummary(taskDescription, successfulCalls, failedCalls, completedAt);

        var result = new TaskProgressSummary(
            TaskId: taskId,
            TaskDescription: taskDescription,
            Summary: summary,
            TotalToolCalls: totalCalls,
            SuccessfulCalls: successfulCalls,
            FailedCalls: failedCalls,
            CompletedPhases: completedPhases ?? new List<string>(),
            StartedAt: startedAt,
            CompletedAt: completedAt);

        _logger?.LogInformation("Generated progress summary for task {TaskId}: {TotalCalls} calls, {SuccessfulCalls} successful",
            taskId, totalCalls, successfulCalls);

        return result;
    }

    public string GenerateReport(TaskProgressSummary summary)
    {
        var duration = summary.CompletedAt.HasValue
            ? summary.CompletedAt.Value - summary.StartedAt
            : TimeSpan.Zero;

        var report = new List<string>();
        report.Add($"Task: {summary.TaskDescription}");
        report.Add($"Duration: {FormatDuration(duration)}");
        report.Add($"Tool calls: {summary.TotalToolCalls} total, {summary.SuccessfulCalls} successful, {summary.FailedCalls} failed");

        if (summary.CompletedPhases.Any())
        {
            report.Add($"Phases completed: {string.Join(", ", summary.CompletedPhases)}");
        }

        report.Add($"\nSummary: {summary.Summary}");
        report.Add($"\nElapsed: {duration.TotalSeconds:F1}s");

        return string.Join("\n", report);
    }

    private static string BuildSummary(string taskDescription, int successfulCalls, int failedCalls, DateTime? completedAt)
    {
        if (completedAt.HasValue)
        {
            return $"Task completed with {successfulCalls}/{successfulCalls + failedCalls} tool calls successful.";
        }

        var status = failedCalls > successfulCalls ? "partially failed" : "in progress";
        return $"Task {status}: {successfulCalls} successful, {failedCalls} failed.";
    }

    private static string FormatDuration(TimeSpan span)
    {
        if (span.TotalHours >= 1)
            return $"{span.Hours}h {span.Minutes}m {span.Seconds}s";
        if (span.TotalMinutes >= 1)
            return $"{span.Minutes}m {span.Seconds}s";
        return $"{span.TotalSeconds:F1}s";
    }
}