using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Result of an AI-powered task validation check.
/// </summary>
public record TaskValidationResult(
    bool Passed,
    string Message,
    string? RetrySuggestion = null);

/// <summary>
/// Uses Pingu (System AI) to validate whether a task has been completed successfully.
/// Sends task description, result summary, and validation criteria to the System AI.
/// </summary>
public interface ITaskValidationService
{
    /// <summary>
    /// Validates whether a task's result meets its validation criteria using the System AI.
    /// </summary>
    Task<TaskValidationResult> ValidateTaskCompletionAsync(
        AgenticTask task,
        string resultSummary,
        CancellationToken ct = default);

    /// <summary>
    /// Validates structured output against expected schema using the System AI.
    /// </summary>
    Task<TaskValidationResult> ValidateStructuredOutputAsync(
        AgenticTask task,
        Dictionary<string, object> outputFields,
        CancellationToken ct = default);
}
