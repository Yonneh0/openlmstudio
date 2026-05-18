// Phase 5.10: Interface for context pruning on task completion

using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>Manages context pruning strategies when a task completes.</summary>
public interface ITaskContextPruner : IDisposable
{
    /// <summary>Archive full uncompressed context for reference later, mark as read-only.</summary>
    Task ArchiveAsync(Guid taskId);

    /// <summary>Compress and archive — store compressed snapshot only (minimal disk usage).</summary>
    Task CompressAndArchiveAsync(Guid taskId);

    /// <summary>Discard all context for this task. Caller must confirm via dialog before calling.</summary>
    Task DiscardAsync(Guid taskId);
}