// Phase 5.5: Interface for context window budget management

using OpenLMStudio.Application.Types;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Manages the token budget for a conversation's context window.
// Tracks all context component token usage and auto-evicts lowest-relevance segments when budget is exceeded.</summary>
public interface IContextWindowBudgeter : IDisposable
{
    /// <summary>Get or create the budget state for this chat, using default maxTokens if not set.</summary>
    Task<ChatBudgetStateDto> GetOrCreateBudgetAsync(Guid chatId, int maxTokens = 8192);

    /// <summary>Deduct tokens from the budget for a specific injection type. Returns remaining tokens after deduction.</summary>
    Task<long> DeductFromBudgetAsync(Guid chatId, ContextInjectionType injectionType, long tokens);

    /// <summary>Attempt to auto-evict lowest-relevance non-pinned segments until targetTokenReduction is reached.</summary>
    Task<bool> TryAutoEvictLowestRelevanceSegmentsAsync(Guid chatId, long targetTokenReduction);

    /// <summary>Returns a budget indicator for UI display (color zone, percentage used, etc.).</summary>
    Task<ContextBudgetIndicator> GetBudgetIndicatorAsync(Guid chatId);

    /// <summary>Sets the maximum token budget for this specific chat.</summary>
    Task SetBudgetForChatAsync(Guid chatId, int maxTokens);

    /// <summary>Sets the compression strategy for this chat. When set to Aggressive and over-budget, auto-evicts segments.</summary>
    Task SetCompressionStrategyForChatAsync(Guid chatId, CompressionLevel strategy);

    /// <summary>Updates relevance scores for all given segments (used by ContextRelevanceEngine).</summary>
    Task UpdateSegmentRelevanceScoresAsync(Guid chatId, IEnumerable<ContextSegment> segments);
}
