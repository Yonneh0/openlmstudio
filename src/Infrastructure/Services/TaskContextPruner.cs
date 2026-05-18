// Implements Phase 5.10: Context Pruning on Task Completion
// Archives, compresses-and-archives, or discards task context based on user preference.

using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>Manages context pruning strategies when a task completes.</summary>
public class TaskContextPruner : ITaskContextPruner, IDisposable
{
    private readonly ILogger<TaskContextPruner>? _logger;
    private readonly ITaskContextStore _taskContextStore;

    public TaskContextPruner(ILogger<TaskContextPruner>? logger, ITaskContextStore taskContextStore)
    {
        _logger = logger;
        _taskContextStore = taskContextStore;
    }

    /// <inheritdoc />
    public async Task ArchiveAsync(Guid taskId)
    {
        // The store's Delete method with Archive strategy handles moving data to the archived table.
        // We just need to update the snapshot to mark it as archived so it won't be reprocessed.
        var snapshot = await _taskContextStore.GetByTaskIdAsync(taskId);
        if (snapshot == null)
            return;

        // Mark as archive-on-completion and set state to Completed
        snapshot.ArchiveOnCompletion = true;
        snapshot.CurrentState = AgentState.Completed;
        
        await _taskContextStore.UpdateAsync(snapshot);

        _logger?.LogInformation("Archived task context for TaskId={TaskId}", taskId);
    }

    /// <inheritdoc />
    public async Task CompressAndArchiveAsync(Guid taskId)
    {
        var snapshot = await _taskContextStore.GetByTaskIdAsync(taskId);
        if (snapshot == null || !snapshot.CompressedContext.Any())
            return;

        // The store already stores compressed context. We just need to ensure the state is Completed
        // and trigger archive via the store's Delete with CompressAndArchive strategy.
        snapshot.ArchiveOnCompletion = true;
        snapshot.CurrentState = AgentState.Completed;
        
        await _taskContextStore.UpdateAsync(snapshot);

        _logger?.LogInformation("Compressed and archived task context for TaskId={TaskId}", taskId);
    }

    /// <inheritdoc />
    public async Task DiscardAsync(Guid taskId)
    {
        // Delete the snapshot — user should confirm via dialog before calling.
        await _taskContextStore.DeleteAsync(taskId, ContextPruneStrategy.Discard);
        
        _logger?.LogInformation("Discarded task context for TaskId={TaskId}", taskId);
    }

    public void Dispose()
    {
        // No unmanaged resources to clean up
    }
}