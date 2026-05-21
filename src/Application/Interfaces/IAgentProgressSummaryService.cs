using System;
using System.Collections.Generic;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Generates human-readable progress summaries for agent tasks.
/// </summary>
public record TaskProgressSummary(
    Guid TaskId,
    string TaskDescription,
    string Summary,
    int TotalToolCalls,
    int SuccessfulCalls,
    int FailedCalls,
    IReadOnlyList<string> CompletedPhases,
    DateTime StartedAt,
    DateTime? CompletedAt);

public interface IAgentProgressSummaryService
{
    /// <summary>
    /// Generates a progress summary for an agent task.
    /// </summary>
    TaskProgressSummary GenerateSummary(
        Guid taskId,
        string taskDescription,
        IReadOnlyList<AgentToolCallRecord> toolCalls,
        IReadOnlyList<string> completedPhases,
        DateTime startedAt,
        DateTime? completedAt = null);

    /// <summary>
    /// Generates a human-readable completion report string.
    /// </summary>
    string GenerateReport(TaskProgressSummary summary);
}