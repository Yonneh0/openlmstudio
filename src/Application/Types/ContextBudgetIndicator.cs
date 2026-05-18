// Phase 5.5: DTOs and types for context window budget management

using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Types;

/// <summary>
/// DTO returned by GetOrCreateBudgetAsync — contains only public state needed by callers.</summary>
public class ChatBudgetStateDto
{
    /// <summary>Total maximum tokens allowed for this chat's context window.</summary>
    public long MaximumTokens { get; set; }

    /// <summary>Tokens remaining before budget is exceeded.</summary>
    public long RemainingTokens { get; set; }
}

/// <summary>
/// Visual budget indicator sent to the UI showing how much of the context token budget is used.</summary>
public class ContextBudgetIndicator
{
    /// <summary>Total maximum tokens allowed for this chat's context window.</summary>
    public long MaximumTokens { get; set; }

    /// <summary>Tokens currently consumed across all context components.</summary>
    public long UsedTokens { get; set; }

    /// <summary>Tokens remaining before budget is exceeded.</summary>
    public long RemainingTokens { get; set; }

    /// <summary>Percentage of budget used (0-100).</summary>
    public float PercentageUsed { get; set; }

    /// <summary>Color zone for UI display: Green ≥80% free, Yellow 5-20% free, Red <5% free.</summary>
    public ContextBudgetColorZone ColorZone { get; set; }

    /// <summary>The current compression strategy applied to this chat's context.</summary>
    public CompressionLevel CompressionStrategy { get; set; }

    /// <summary>Tokens allocated per injection type (system prompt, compressed history, etc.).</summary>
    public Dictionary<string, long> BudgetAllocationSummary { get; set; } = new();

    /// <summary>Returns an empty indicator for when no budget exists for a chat.</summary>
    public static ContextBudgetIndicator CreateEmpty() => new() 
        { MaximumTokens = 0, UsedTokens = 0, RemainingTokens = 0, PercentageUsed = 0f };
}

/// <summary>Color zones for UI display of context budget indicator.</summary>
public enum ContextBudgetColorZone
{
    /// <summary>>20% remaining — green zone</summary>
    Green,

    /// <summary>5-20% remaining — yellow/warning zone</summary>
    Yellow,

    /// <summary><5% remaining — red/critical zone</summary>
    Red
}