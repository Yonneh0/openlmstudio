using System.Collections.Generic;

namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Represents a context segment within a chat conversation.
/// </summary>
public class ContextSegment : IDisposable
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public MessageRole Role { get; set; }
    public bool IsCompressed { get; set; }
    public bool IsPinned { get; set; }

    /// <summary>Token count for this segment.</summary>
    public int TokenCount { get; set; }

    /// <summary>Type of context injection that created this segment.</summary>
    public ContextInjectionType InjectionType { get; set; } = ContextInjectionType.CompressedHistory;

    /// <summary>Whether the segment is suppressed (not sent to AI).</summary>
    public bool IsSuppressed { get; set; }

    /// <summary>Relevance score assigned by ContextRelevanceEngine. Higher = more relevant.</summary>
    public float RelevanceScore { get; set; }

    public static ContextSegment CreateUncompressed(Message message) =>
        new()
        {
            Id = message.Id,
            Content = message.Content,
            Role = message.Role,
            IsCompressed = false,
            IsPinned = false,
            TokenCount = message.TokenCount > 0 ? message.TokenCount : EstimateTokenCount(message.Content)
        };

    /// <summary>Creates an empty segment with the given ID and relevance score (used by budget system).</summary>
    internal static ContextSegment CreateEmptyWithRelevance(Guid id) =>
        new() { Id = id, RelevanceScore = 0f };

    /// <summary>Standardized token counting: ~1 token per 4 characters for English.</summary>
    private static int EstimateTokenCount(string text) => 
        string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;

    public void Dispose() { /* No unmanaged resources */ }
}

/// <summary>Represents an agentic task state (Planning, Acting, etc.).</summary>
public enum AgentState
{
    /// <summary>Agent is planning the approach for this task.</summary>
    Planning,
    /// <summary>Agent is actively executing actions.</summary>
    Acting,
    /// <summary>Agent is paused awaiting user input/approval.</summary>
    Paused,
    /// <summary>Task completed successfully.</summary>
    Completed,
    /// <summary>Task failed with an error.</summary>
    Failed
}

/// <summary>The level of context compression applied to segments.</summary>
public enum CompressionLevel
{
    /// <summary>No compression — full detail preserved.</summary>
    None,
    /// <summary>Light compression — key phrases extracted.</summary>
    Light,
    /// <summary>Medium compression — summaries of older messages.</summary>
    Medium,
    /// <summary>Aggressive compression — only outlines/headers kept.</summary>
    Aggressive
}

/// <summary>Defines what types of context can be injected into a conversation.</summary>
public enum ContextInjectionType
{
    /// <summary>System prompt always present for the AI.</summary>
    SystemPrompt,
    /// <summary>Agentic task context snapshot (for agent harness).</summary>
    TaskContextSnapshot,
    /// <summary>Custom user-injected context (manual injection).</summary>
    CustomInjection,
    /// <summary>Compressed conversation history.</summary>
    CompressedHistory,
    /// <summary>Active project state (file tree, git status).</summary>
    ProjectState
}

/// <summary>A fully assembled context window for sending to the AI.</summary>
public class ContextWindow : IDisposable
{
    public List<ContextSegment> Segments { get; set; } = new();
    public long TotalTokenCount { get; set; }
    public CompressionLevel OverallCompression { get; set; }

    public static ContextWindow CreateEmpty() => new() { OverallCompression = CompressionLevel.None };

    public void Dispose() { /* No unmanaged resources */ }
}

/// <summary>Defines how context is pruned when a task completes.</summary>
public enum ContextPruneStrategy
{
    /// <summary>Keep full uncompressed context for reference later, mark as read-only.</summary>
    Archive,
    /// <summary>Store compressed snapshot only (minimal disk usage).</summary>
    CompressAndArchive,
    /// <summary>Remove all context — user confirms via dialog before deletion.</summary>
    Discard
}

/// <summary>The token budget for a conversation's context window.</summary>
public class ContextBudget : IDisposable
{
    public long MaximumTokens { get; set; }
    public long RemainingTokens { get; private set; }
    public Dictionary<ContextInjectionType, long> BudgetAllocation { get; set; } = new();

    public static ContextBudget CreateDefault(int maxTokens = 8192) => 
        new() 
        { 
            MaximumTokens = maxTokens, 
            RemainingTokens = maxTokens,
            BudgetAllocation = new Dictionary<ContextInjectionType, long> { [ContextInjectionType.SystemPrompt] = (long)(maxTokens / 4.0) } 
        };

    public void Deduct(long tokens)
    {
        if (tokens > RemainingTokens)
            throw new InvalidOperationException($"Cannot deduct {tokens} tokens: only {RemainingTokens} remaining.");
        RemainingTokens -= tokens;
    }

    public void Dispose() { /* No unmanaged resources */ }
}