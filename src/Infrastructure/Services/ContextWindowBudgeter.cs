// Implements Phase 5.5: Context Window Budgeting System
// Tracks token budget across all context components and auto-evicts lowest-relevance segments when exceeded.

using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Types;
using OpenLMStudio.Domain.Models;
using System.Collections.Concurrent;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Manages the token budget for a conversation's context window.
// Tracks all context component token usage and auto-evicts lowest-relevance segments when budget is exceeded.</summary>
public class ContextWindowBudgeter : IContextWindowBudgeter, IDisposable
{
    private readonly ILogger<ContextWindowBudgeter>? _logger;

    // Budget state per chat (keyed by ChatId)
    private readonly ConcurrentDictionary<Guid, ChatBudgetState> _budgetStates = new();

    public ContextWindowBudgeter(ILogger<ContextWindowBudgeter>? logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ChatBudgetStateDto> GetOrCreateBudgetAsync(Guid chatId, int maxTokens = 8192)
    {
        if (!_budgetStates.TryGetValue(chatId, out var budget))
        {
            lock (_budgetStates)
            {
                // Double-check after acquiring lock
                _budgetStates.TryGetValue(chatId, out budget);
            }

            if (budget == null)
            {
                budget = ChatBudgetState.CreateDefault(chatId, maxTokens);
                _budgetStates[chatId] = budget;
            }
        }

        return new ChatBudgetStateDto { MaximumTokens = budget.MaximumTokens, RemainingTokens = budget.RemainingTokens };
    }

    /// <inheritdoc />
    public async Task<long> DeductFromBudgetAsync(Guid chatId, ContextInjectionType injectionType, long tokens)
    {
        var budget = await GetOrCreateBudgetInternalAsync(chatId);
        budget.Deduct(injectionType, tokens);

        // Check if auto-eviction needed after deduction
        if (budget.RemainingTokens < 0 && budget.CompressionStrategy != CompressionLevel.Aggressive)
        {
            await TryAutoEvictAsync(budget);
        }

        return budget.RemainingTokens;
    }

    /// <inheritdoc />
    public async Task<bool> TryAutoEvictLowestRelevanceSegmentsAsync(Guid chatId, long targetTokenReduction)
    {
        if (!_budgetStates.TryGetValue(chatId, out var budget))
            return false;

        var evicted = 0L;

        // Sort segments by relevance score (ascending — lowest first)
        var segmentsToEvict = budget.Segments
            .Where(s => !s.IsPinned && s.InjectionType != ContextInjectionType.SystemPrompt && s.InjectionType != ContextInjectionType.TaskContextSnapshot)
            .OrderBy(s => s.RelevanceScore)
            .TakeWhile(s => evicted < targetTokenReduction);

        foreach (var segment in segmentsToEvict)
        {
            budget.MarkSegmentForEviction(segment.Id, segment.TokenCount);
            evicted += segment.TokenCount;
        }

        return evicted > 0;
    }

    /// <inheritdoc />
    public async Task<ContextBudgetIndicator> GetBudgetIndicatorAsync(Guid chatId)
    {
        if (!_budgetStates.TryGetValue(chatId, out var budget))
            return ContextBudgetIndicator.CreateEmpty();

        long usedTokens = budget.MaximumTokens - budget.RemainingTokens;
        double percentageUsed = budget.MaximumTokens > 0 ? (double)usedTokens / budget.MaximumTokens : 1.0;

        // Determine color zone based on usage percentage
        var colorZone = ContextBudgetColorZone.Green;
        if (percentageUsed >= 0.95)
            colorZone = ContextBudgetColorZone.Red;
        else if (percentageUsed >= 0.80)
            colorZone = ContextBudgetColorZone.Yellow;

        return new ContextBudgetIndicator
        {
            MaximumTokens = budget.MaximumTokens,
            UsedTokens = usedTokens,
            RemainingTokens = Math.Max(0, budget.RemainingTokens),
            PercentageUsed = (float)(percentageUsed * 100.0),
            ColorZone = colorZone,
            CompressionStrategy = budget.CompressionStrategy,
            BudgetAllocationSummary = budget.GetAllocationSummary()
        };
    }

    /// <inheritdoc />
    public Task SetBudgetForChatAsync(Guid chatId, int maxTokens)
    {
        if (maxTokens <= 0)
            throw new ArgumentException("Max tokens must be positive.", nameof(maxTokens));

        var newBudget = ChatBudgetState.CreateDefault(chatId, maxTokens);

        // Try to update first; if it doesn't exist, add it with the default
        if (!_budgetStates.TryGetValue(chatId, out _))
            _budgetStates.TryAdd(chatId, newBudget);
        else
            _budgetStates[chatId] = CreateOrExpandBudget(chatId, maxTokens, _budgetStates[chatId]);

        _logger?.LogDebug("Set budget for chat {ChatId}: maxTokens={MaxTokens}", chatId, maxTokens);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SetCompressionStrategyForChatAsync(Guid chatId, CompressionLevel strategy)
    {
        if (!_budgetStates.TryGetValue(chatId, out var budget))
            return;

        budget.CompressionStrategy = strategy;

        // If switching to more aggressive compression, try to evict some segments now
        if (strategy == CompressionLevel.Aggressive && budget.RemainingTokens < 0)
        {
            await TryAutoEvictAsync(budget);
        }

        _logger?.LogDebug("Set compression strategy for chat {ChatId}: {Strategy}", chatId, strategy);
    }

    /// <inheritdoc />
    public async Task UpdateSegmentRelevanceScoresAsync(Guid chatId, IEnumerable<ContextSegment> segments)
    {
        if (!_budgetStates.TryGetValue(chatId, out var budget))
            return;

        foreach (var segment in segments)
        {
            // Recompute relevance score based on recency and other factors
            var score = ComputeRelevanceScore(segment);

            await budget.SetSegmentRelevanceAsync(segment.Id, score);
        }
    }

    public void Dispose()
    {
        _budgetStates.Clear();
    }

    // ---- Private Helpers ----

    private static ChatBudgetState CreateOrExpandBudget(Guid chatId, int maxTokens, ChatBudgetState existing)
    {
        if (existing.MaximumTokens == maxTokens)
            return existing; // Budget unchanged

        var newBudget = ChatBudgetState.CreateDefault(chatId, maxTokens);

        // Preserve allocation proportions for system prompt portion
        newBudget.BudgetAllocation = existing.BudgetAllocation.ToDictionary();

        return newBudget;
    }

    private static async Task TryAutoEvictAsync(ChatBudgetState budget)
    {
        while (budget.RemainingTokens < 0 && !budget.IsAllSegmentsPinned())
        {
            // Evict lowest-relevance non-pinned segment
            var target = budget.Segments
                .Where(s => !s.IsPinned && s.InjectionType != ContextInjectionType.SystemPrompt)
                .OrderByDescending(s => s.RelevanceScore)
                .FirstOrDefault();

            if (target == null)
                break; // All remaining segments are pinned or none exist

            budget.MarkSegmentForEviction(target.Id, target.TokenCount);
        }
    }

    private static float ComputeRelevanceScore(ContextSegment segment)
    {
        // Base score from relevance property of the segment (if set by ContextRelevanceEngine)
        var baseScore = segment.RelevanceScore;

        // Boost for pinned segments (they can't be evicted anyway, but useful to know)
        if (segment.IsPinned)
            baseScore += 100.0f;

        return baseScore;
    }

    private async Task<ChatBudgetState> GetOrCreateBudgetInternalAsync(Guid chatId)
    {
        if (!_budgetStates.TryGetValue(chatId, out var budget))
        {
            lock (_budgetStates)
            {
                _budgetStates.TryGetValue(chatId, out budget);
            }

            if (budget == null)
            {
                budget = ChatBudgetState.CreateDefault(chatId, 8192);
                _budgetStates[chatId] = budget;
            }
        }

        return budget;
    }
}

/// <summary>
/// Per-chat budget state tracking tokens used and segments under management.</summary>
internal class ChatBudgetState : IDisposable
{
    public Guid ChatId { get; init; }
    public long MaximumTokens { get; private set; }
    public long RemainingTokens { get; private set; }
    public Dictionary<ContextInjectionType, long> BudgetAllocation { get; set; } = new();
    public CompressionLevel CompressionStrategy { get; set; } = CompressionLevel.None;

    // Segment tracking for eviction (relevance score per segment ID)
    private readonly ConcurrentDictionary<Guid, float> _segmentRelevanceScores = new();
    private readonly ConcurrentBag<(Guid SegmentId, long TokenCount)> _evictionCandidates = new();

    public List<ContextSegment> Segments =>
        _segmentRelevanceScores.Keys.Select(id => ContextSegment.CreateEmptyWithRelevance(id)).ToList();

    internal static ChatBudgetState CreateDefault(Guid chatId, int maxTokens)
    {
        return new ChatBudgetState
        {
            ChatId = chatId,
            MaximumTokens = maxTokens,
            RemainingTokens = maxTokens,
            BudgetAllocation = new Dictionary<ContextInjectionType, long>
            { [ContextInjectionType.SystemPrompt] = (long)(maxTokens / 4.0) }
        };
    }

    internal void Deduct(ContextInjectionType injectionType, long tokens)
    {
        // Check if this injection type has an allocation limit
        if (BudgetAllocation.TryGetValue(injectionType, out var allocated))
        {
            // Allow deduction beyond allocation but log warning
        }

        RemainingTokens -= tokens;

        // Track per-type allocation
        BudgetAllocation[injectionType] = BudgetAllocation.GetValueOrDefault(injectionType) + tokens;
    }

    internal void MarkSegmentForEviction(Guid segmentId, long tokenCount)
    {
        _evictionCandidates.Add((segmentId, tokenCount));

        // Remove from tracking and add back tokens to budget
        if (_segmentRelevanceScores.TryRemove(segmentId, out _))
            RemainingTokens += tokenCount;
    }

    internal async Task SetSegmentRelevanceAsync(Guid segmentId, float score)
    {
        _segmentRelevanceScores[segmentId] = score;
    }

    internal bool IsAllSegmentsPinned()
    {
        return _segmentRelevanceScores.Count == 0 ||
               Segments.All(s => s.IsPinned);
    }

    public Dictionary<string, long> GetAllocationSummary()
    {
        return BudgetAllocation.ToDictionary(k => k.Key.ToString(), v => v.Value);
    }

    public void Dispose()
    {
        _segmentRelevanceScores.Clear();
        _evictionCandidates.Clear();
    }
}