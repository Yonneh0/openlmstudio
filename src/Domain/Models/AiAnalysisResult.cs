// Represents the AI's analysis context when it analyzed file changes and suggested them.
// This is populated by TaskContextSnapshot.AiAnalysis field per Phase 5.6 spec.

namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Stores the full conversation context + project state snapshot from when an agent analyzed file changes and suggested them.
/// Used by the AI Analysis Context Panel for Git Diff Review (Phase 7.6.1).
/// </summary>
public class AiAnalysisResult : IDisposable
{
    /// <summary>Compressed message sequence active during analysis.</summary>
    public List<ContextSegment>? AnalyzedChatHistory { get; set; }

    /// <summary>File tree, git status, open documents at time of review.</summary>
    public string? ProjectStateAtTimeOfAnalysis { get; set; }

    /// <summary>List of context segment IDs that were relevant to the AI's reasoning.</summary>
    public List<string>? RelevantContextSegmentIds { get; set; } = new();

    /// <summary>Tokens consumed by the analysis context (for budget tracking).</summary>
    public long AnalysisTokenCount { get; set; }

    /// <summary>When this analysis was performed.</summary>
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;

    public void Dispose()
    {
        // No unmanaged resources to clean up
    }
}