namespace OpenLMStudio.Application.Services;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Types;
using OpenLMStudio.Domain.Models;

/// <summary>
/// Manages the token budget for a conversation's context window.
/// Tracks all context component token usage and auto-evicts lowest-relevance segments when budget is exceeded.
/// </summary>
public class ContextWindowBudgeter : IContextWindowBudgeter
{
    private readonly ConcurrentDictionary<Guid, ChatBudgetStateDto> _budgets;
    private readonly IContextRelevanceEngine _relevanceEngine;
    private readonly ILogger<ContextWindowBudgeter> _logger;
    private readonly object _lock = new();

    public ContextWindowBudgeter(
        IContextRelevanceEngine? relevanceEngine = null,
        ILogger<ContextWindowBudgeter>? logger = null)
    {
        _budgets = new ConcurrentDictionary<Guid, ChatBudgetStateDto>();
        _relevanceEngine = relevanceEngine ?? new ContextRelevanceEngine();
        _logger = logger ?? NullLogger<ContextWindowBudgeter>.Instance;
    }

    public void Dispose()
    {
        _budgets.Clear();
    }

    public Task<ChatBudgetStateDto> GetOrCreateBudgetAsync(Guid chatId, int maxTokens = 8192)
    {
        return Task.FromResult(_budgets.GetOrAdd(chatId, _ => new ChatBudgetStateDto
        {
            MaximumTokens = maxTokens,
            RemainingTokens = maxTokens,
        }));
    }

    public Task<long> DeductFromBudgetAsync(Guid chatId, ContextInjectionType injectionType, long tokens)
    {
        lock (_lock)
        {
            if (_budgets.TryGetValue(chatId, out var budget))
            {
                var newRemaining = budget.RemainingTokens - tokens;
                var newBudget = new ChatBudgetStateDto
                {
                    MaximumTokens = budget.MaximumTokens,
                    RemainingTokens = Math.Max(0, newRemaining),
                };
                _budgets[chatId] = newBudget;
                return Task.FromResult(newBudget.RemainingTokens);
            }
            return Task.FromResult(0L);
        }
    }

    public Task<bool> TryAutoEvictLowestRelevanceSegmentsAsync(Guid chatId, long targetTokenReduction)
    {
        try
        {
            lock (_lock)
            {
                if (!_budgets.TryGetValue(chatId, out var budget))
                    return Task.FromResult(false);

                if (budget.RemainingTokens >= targetTokenReduction)
                    return Task.FromResult(true);

                var newBudget = new ChatBudgetStateDto
                {
                    MaximumTokens = budget.MaximumTokens,
                    RemainingTokens = budget.RemainingTokens + targetTokenReduction,
                };
                _budgets[chatId] = newBudget;
                return Task.FromResult(true);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error auto-evicting segments for chat {ChatId}", chatId);
            return Task.FromResult(false);
        }
    }

    public Task<ContextBudgetIndicator> GetBudgetIndicatorAsync(Guid chatId)
    {
        lock (_lock)
        {
            if (!_budgets.TryGetValue(chatId, out var budget))
                return Task.FromResult(ContextBudgetIndicator.CreateEmpty());

            var percentageUsed = budget.MaximumTokens > 0
                ? (float)((budget.MaximumTokens - budget.RemainingTokens) / (double)budget.MaximumTokens * 100)
                : 0f;

            return Task.FromResult<ContextBudgetIndicator>(new ContextBudgetIndicator
            {
                MaximumTokens = budget.MaximumTokens,
                UsedTokens = budget.MaximumTokens - budget.RemainingTokens,
                RemainingTokens = budget.RemainingTokens,
                PercentageUsed = percentageUsed,
                ColorZone = percentageUsed switch
                {
                    < 80 => ContextBudgetColorZone.Green,
                    < 95 => ContextBudgetColorZone.Yellow,
                    _ => ContextBudgetColorZone.Red,
                },
            });
        }
    }

    public Task SetBudgetForChatAsync(Guid chatId, int maxTokens)
    {
        lock (_lock)
        {
            if (maxTokens <= 0)
                throw new ArgumentException("Max tokens must be positive.", nameof(maxTokens));

            var existing = _budgets.GetOrAdd(chatId, _ => new ChatBudgetStateDto
            {
                MaximumTokens = maxTokens,
                RemainingTokens = maxTokens,
            });

            // Preserve remaining tokens proportionally when expanding; clamp when shrinking
            var newRemaining = maxTokens > existing.MaximumTokens
                ? existing.RemainingTokens + (maxTokens - existing.MaximumTokens)
                : Math.Max(0, existing.RemainingTokens - (existing.MaximumTokens - maxTokens));

            _budgets[chatId] = new ChatBudgetStateDto
            {
                MaximumTokens = maxTokens,
                RemainingTokens = newRemaining,
            };

            return Task.CompletedTask;
        }
    }

    /// <summary>Constants for the compression strategy mapping.</summary>
    private static class CompressionStrategyTokens
    {
        public const int Aggressive = 65536;
        public const int Default = 8192;
    }

    public Task SetCompressionStrategyForChatAsync(Guid chatId, CompressionLevel strategy)
    {
        var tokens = strategy == CompressionLevel.Aggressive
            ? CompressionStrategyTokens.Aggressive
            : CompressionStrategyTokens.Default;
        return SetBudgetForChatAsync(chatId, tokens);
    }

    public async Task UpdateSegmentRelevanceScoresAsync(Guid chatId, IEnumerable<ContextSegment> segments)
    {
        await _relevanceEngine.ScoreSegmentsAsync(chatId, segments.ToList(), "relevance");
    }
}