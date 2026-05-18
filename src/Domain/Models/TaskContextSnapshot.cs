// Note: ContextSegment is defined in Domain.Models (ChatContext.cs).
// AgentState and ContextPruneStrategy are also defined in ChatContext.cs — do NOT duplicate here.

using System.Collections.Generic;

namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Represents a snapshot of context for an agentic task.
/// Contains compressed history, tool results cache, and project state needed to resume from interruption.
/// </summary>
public class TaskContextSnapshot : IDisposable
{
    /// <summary>Unique identifier linking this snapshot to its parent task.</summary>
    public Guid TaskId { get; set; }

    /// <summary>Current task description/goal (for context matching).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Current agent state for the task. (Defined in ChatContext.cs)</summary>
    public AgentState CurrentState { get; set; } = AgentState.Planning;

    /// <summary>Compressed message sequence for quick re-injection.</summary>
    public List<ContextSegment> CompressedContext { get; set; } = new();

    /// <summary>Dictionary of tool call ID → compressed result for quick lookup during resume.</summary>
    public Dictionary<string, ContextSegment> ToolResultsCache { get; set; } = new();

    /// <summary>Active file tree at time of capture (compact representation).</summary>
    public string? ActiveFileTree { get; set; }

    /// <summary>Git status snapshot at time of capture.</summary>
    public string? GitStatusSnapshot { get; set; }

    /// <summary>List of relevant entities: file paths, code definitions, concepts mentioned in current context.</summary>
    public List<string> RelevantEntities { get; set; } = new();

    /// <summary>Tokens consumed by compressed context (for budget tracking).</summary>
    public long CompressedContextTokenCount { get; set; }

    /// <summary>Timestamp when the snapshot was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Last time this snapshot was updated.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Whether this snapshot should be archived (not discarded) on task completion.</summary>
    public bool ArchiveOnCompletion { get; set; } = true;

    public void Dispose()
    {
        // No unmanaged resources to clean up
    }
}