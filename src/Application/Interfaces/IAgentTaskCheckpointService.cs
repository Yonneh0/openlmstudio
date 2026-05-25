using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Interface for managing task checkpoints.
/// </summary>
public interface IAgentTaskCheckpointService : IDisposable
{
    /// <summary>
    /// Saves a checkpoint for a task.
    /// </summary>
    Task SaveCheckpointAsync(Guid taskId, TaskCheckpoint checkpoint, CancellationToken ct = default);

    /// <summary>
    /// Gets the latest checkpoint for a task.
    /// </summary>
    Task<TaskCheckpoint?> GetLatestCheckpointAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Gets all checkpoints for a task.
    /// </summary>
    Task<IReadOnlyList<TaskCheckpoint>> GetCheckpointsAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Gets the latest tool checkpoint for a task.
    /// </summary>
    Task<TaskCheckpoint?> GetLatestToolCheckpointAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Gets the latest completion checkpoint for a task.
    /// </summary>
    Task<TaskCheckpoint?> GetLatestCompletionCheckpointAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Deletes all checkpoints for a task.
    /// </summary>
    Task DeleteCheckpointsAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Saves a tool checkpoint after a tool call.
    /// </summary>
    Task SaveToolCheckpointAsync(Guid taskId, string toolName, Dictionary<string, object> parameters, string result, CancellationToken ct = default);

    /// <summary>
    /// Saves a task checkpoint after a task step.
    /// </summary>
    Task SaveTaskCheckpointAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Saves a completion checkpoint after task completion.
    /// </summary>
    Task SaveCompletionCheckpointAsync(Guid taskId, CancellationToken ct = default);
}