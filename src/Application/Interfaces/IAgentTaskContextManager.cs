using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Interface for managing agent task context (truncation, summarization, file read cache).
/// </summary>
public interface IAgentTaskContextManager : IDisposable
{
    /// <summary>
    /// Truncates the conversation history based on settings.
    /// </summary>
    Task TruncateHistoryAsync(Guid taskId, int maxMessages, CancellationToken ct = default);

    /// <summary>
    /// Summarizes the conversation history based on settings.
    /// </summary>
    Task SummarizeHistoryAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Gets a file from the read cache.
    /// </summary>
    Task<string?> GetCachedFileAsync(Guid taskId, string filePath, CancellationToken ct = default);

    /// <summary>
    /// Sets a file in the read cache.
    /// </summary>
    Task SetCachedFileAsync(Guid taskId, string filePath, string content, CancellationToken ct = default);

    /// <summary>
    /// Clears the file read cache.
    /// </summary>
    Task ClearFileCacheAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Updates the file read cache with new data.
    /// </summary>
    Task UpdateFileCacheAsync(Guid taskId, Dictionary<string, string> cache, CancellationToken ct = default);

    /// <summary>
    /// Gets the current context budget.
    /// </summary>
    Task<ContextBudget> GetContextBudgetAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Sets the context budget.
    /// </summary>
    Task SetContextBudgetAsync(Guid taskId, ContextBudget budget, CancellationToken ct = default);
}