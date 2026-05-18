// Phase 5.8: Interface for parent→child task context inheritance

using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Propagates relevant context from a parent task to child tasks with budget-aware filtering.</summary>
public interface ITaskContextInheritor : IDisposable
{
    /// <summary>Create an inherited snapshot for a new child task, propagating only the most relevant parent info.</summary>
    Task<TaskContextSnapshot> CreateChildInheritanceAsync(Guid parentTaskId, Guid childTaskId);

    /// <summary>Request additional context from a parent task on demand. Only returns if parent has budget to spare.</summary>
    Task<AdditionalParentContext?> RequestAdditionalContextFromParentAsync(Guid parentId, Guid childId);
}

/// <summary>Additional context retrieved from a parent task for the child's use.</summary>
public class AdditionalParentContext
{
    /// <summary>Relevant segments from the parent's compressed history that are useful to this child.</summary>
    public List<ContextSegment>? RelevantHistorySegments { get; set; }

    /// <summary>The project state snapshot at time of this context request.</summary>
    public string? ProjectStateSnapshot { get; set; }

    /// <summary>Tokens consumed by this additional parent context.</summary>
    public long TokenCount { get; set; }
}