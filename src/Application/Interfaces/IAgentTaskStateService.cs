using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Interface for managing agent task state persistence.
/// </summary>
public interface IAgentTaskStateService : IDisposable
{
    /// <summary>
    /// Gets the current state for a task.
    /// </summary>
    Task<AgentTaskState?> GetStateAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Saves the state for a task.
    /// </summary>
    Task SaveStateAsync(Guid taskId, AgentTaskState state, CancellationToken ct = default);

    /// <summary>
    /// Updates the agent state of a task.
    /// </summary>
    Task UpdateAgentStateAsync(Guid taskId, AgentStateExtended newState, CancellationToken ct = default);

    /// <summary>
    /// Resets the state for a task.
    /// </summary>
    Task ResetStateAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Gets all tracked task states.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, AgentTaskState>> GetAllStatesAsync(CancellationToken ct = default);
}