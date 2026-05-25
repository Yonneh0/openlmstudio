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

    public async Task<ChatBudgetStateDto> GetOrCreateBudgetAsync(Guid chatId, int maxTokens = 8192)
    {
        return _budgets.GetOrAdd(chatId, _ => new ChatBudgetStateDto
        {
            MaximumTokens = maxTokens,
            RemainingTokens = maxTokens,
        });
    }

    public async Task<long> DeductFromBudgetAsync(Guid chatId, ContextInjectionType injectionType, long tokens)
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
                return newBudget.RemainingTokens;
            }
            return 0;
        }
    }

    public async Task<bool> TryAutoEvictLowestRelevanceSegmentsAsync(Guid chatId, long targetTokenReduction)
    {
        try
        {
            lock (_lock)
            {
                if (!_budgets.TryGetValue(chatId, out var budget))
                    return false;

                if (budget.RemainingTokens >= targetTokenReduction)
                    return true;

                var newBudget = new ChatBudgetStateDto
                {
                    MaximumTokens = budget.MaximumTokens,
                    RemainingTokens = budget.RemainingTokens + targetTokenReduction,
                };
                _budgets[chatId] = newBudget;
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error auto-evicting segments for chat {ChatId}", chatId);
            return false;
        }
    }

    public async Task<ContextBudgetIndicator> GetBudgetIndicatorAsync(Guid chatId)
    {
        lock (_lock)
        {
            if (!_budgets.TryGetValue(chatId, out var budget))
                return ContextBudgetIndicator.CreateEmpty();

            var percentageUsed = budget.MaximumTokens > 0
                ? (float)((budget.MaximumTokens - budget.RemainingTokens) / (double)budget.MaximumTokens * 100)
                : 0f;

            return new ContextBudgetIndicator
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
            };
        }
    }

    public async Task SetBudgetForChatAsync(Guid chatId, int maxTokens)
    {
        lock (_lock)
        {
            var budget = _budgets.GetOrAdd(chatId, _ => new ChatBudgetStateDto
            {
                MaximumTokens = maxTokens,
                RemainingTokens = maxTokens,
            });
            _budgets[chatId] = new ChatBudgetStateDto
            {
                MaximumTokens = maxTokens,
                RemainingTokens = Math.Min(budget.RemainingTokens, maxTokens),
            };
        }
    }

    public async Task SetCompressionStrategyForChatAsync(Guid chatId, CompressionLevel strategy)
    {
        await SetBudgetForChatAsync(chatId, strategy == CompressionLevel.Aggressive ? 65536 : 8192);
    }

    public async Task UpdateSegmentRelevanceScoresAsync(Guid chatId, IEnumerable<ContextSegment> segments)
    {
        await _relevanceEngine.ScoreSegmentsAsync(chatId, segments.ToList(), "relevance");
    }
}