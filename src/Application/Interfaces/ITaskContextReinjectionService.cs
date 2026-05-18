// Phase 5.9: Interface for fast task context re-injection — restore full context in <100ms via pre-compressed snapshot

using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Restores full context for a paused/abandoned agent task in <100ms via pre-compressed snapshot.</summary>
public interface ITaskContextReinjectionService : IDisposable
{
    /// <summary>Restore the compressed snapshot as fast as possible — bypasses recomputation of compression.</summary>
    Task<ContextWindow> FastReinjectAsync(Guid taskId);

    /// <summary>Resume tool call chain from interruption point using cached tool results.</summary>
    Task ResumeToolCallChainAsync(Guid taskId, Guid resumeFromCallId);

    /// <summary>Get a summary of what was reinjected (for UI display).</summary>
    Task<ReinjectionSummary> GetReinjectionSummaryAsync(Guid taskId);
}

/// <summary>Summary of what was reinjected from a task context snapshot.</summary>
public class ReinjectionSummary
{
    /// <summary>Tokens restored from compressed history.</summary>
    public long RestoredTokenCount { get; set; }

    /// <summary>Number of tool results restored from cache.</summary>
    public int ToolResultsRestored { get; set; }

    /// <summary>Whether the tool call chain was resumed from a specific point.</summary>
    public bool ToolChainResumed { get; set; }

    /// <summary>The last phase/state before interruption (e.g., Acting → Planning).</summary>
    public string? PreviousState { get; set; }

    /// <summary>Tokens remaining after reinjection for the task's budget.</summary>
    public long RemainingBudgetTokens { get; set; }
}