using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Detects when an agent task is complete based on tool results and task state.
/// Delegates to TaskValidationService for AI-powered completion checks.
/// </summary>
public interface ITaskCompletionDetector
{
    /// <summary>
    /// Evaluates whether a task should be marked complete based on current tool call history.
    /// </summary>
    Task<TaskValidationResult> DetectCompletionAsync(
        AgenticTask task,
        IReadOnlyList<AgentToolCallRecord> toolCalls,
        CancellationToken ct = default);
}