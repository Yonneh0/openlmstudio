using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// AI-powered task completion verification using the System AI (Pingu).
/// </summary>
public record TaskValidationResult(
    bool Passed,
    string Message,
    string? FailedReason);

/// <summary>
/// Validates task completion criteria using the System AI.
/// </summary>
public interface ITaskValidationService
{
    /// <summary>
    /// Validates whether a task has been completed successfully based on its validation criteria.
    /// Uses System AI (Pingu) to evaluate the result against the criteria.
    /// </summary>
    /// <param name="task">The task to validate.</param>
    /// <param name="result">The result/output from the task.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result indicating pass/fail with reason.</returns>
    Task<TaskValidationResult> ValidateTaskCompletionAsync(
        Task task,
        string result,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates structured output against a JSON schema using the System AI.
    /// </summary>
    /// <param name="task">The task with OutputFields schema.</param>
    /// <param name="output">The structured output to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result indicating pass/fail with reason.</returns>
    Task<TaskValidationResult> ValidateStructuredOutputAsync(
        Task task,
        string output,
        CancellationToken cancellationToken = default);
}