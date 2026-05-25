using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Interface for managing agent task progress tracking.
/// </summary>
public interface IAgentTaskProgressService : IDisposable
{
    /// <summary>
    /// Gets the current progress for a task.
    /// </summary>
    Task<AgentTaskProgress?> GetProgressAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Updates the progress percentage for a task.
    /// </summary>
    Task UpdateProgressAsync(Guid taskId, int percentage, CancellationToken ct = default);

    /// <summary>
    /// Adds a checklist item.
    /// </summary>
    Task AddChecklistItemAsync(Guid taskId, string description, bool isCompleted = false, CancellationToken ct = default);

    /// <summary>
    /// Marks a checklist item as completed.
    /// </summary>
    Task CompleteChecklistItemAsync(Guid taskId, string description, CancellationToken ct = default);

    /// <summary>
    /// Updates the current step.
    /// </summary>
    Task UpdateCurrentStepAsync(Guid taskId, string step, CancellationToken ct = default);

    /// <summary>
    /// Adds a reminder.
    /// </summary>
    Task AddReminderAsync(Guid taskId, string reminder, CancellationToken ct = default);

    /// <summary>
    /// Gets the calculated checklist percentage.
    /// </summary>
    Task<int> GetChecklistPercentageAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Checks if a reminder should be sent based on the reminder interval.
    /// </summary>
    Task<bool> ShouldSendReminderAsync(Guid taskId, CancellationToken ct = default);
}