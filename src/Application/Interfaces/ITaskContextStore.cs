using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Defines the repository pattern for managing agentic task context snapshots.
/// </summary>
public interface ITaskContextStore : IDisposable
{
    /// <summary>
    /// Creates a new task context snapshot and persists it.
    /// Auto-generates when agent enters a new phase (Planning → Acting, etc.).
    /// </summary>
    Task CreateAsync(TaskContextSnapshot snapshot);

    /// <summary>
    /// Retrieves the full context for resuming an agent task from any point.
    /// </summary>
    Task<TaskContextSnapshot?> GetByTaskIdAsync(Guid taskId);

    /// <summary>
    /// Updates a delta tracking — only captures what changed since last snapshot (efficient incremental updates).
    /// </summary>
    Task UpdateAsync(TaskContextSnapshot snapshot);

    /// <summary>
    /// Archives or discards context based on the PruneStrategy when task completes.
    /// </summary>
    Task DeleteAsync(Guid taskId, ContextPruneStrategy strategy = ContextPruneStrategy.Archive);

    /// <summary>
    /// Lists all active (non-completed) agent tasks for a given chat.
    /// </summary>
    Task<List<TaskContextSnapshot>> ListActiveByChatIdAsync(Guid chatId);

    /// <summary>
    /// Lists all archived task context snapshots across all chats.
    /// </summary>
    Task<List<TaskContextSnapshot>> ListArchivedAsync();
}