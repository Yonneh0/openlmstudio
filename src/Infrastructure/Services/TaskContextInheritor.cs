// Implements Phase 5.8: Context Inheritance System — Parent→Child Task Propagation
// Propagates relevant context from parent to child tasks with budget-aware filtering.

using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Propagates relevant context from a parent task to child tasks with budget-aware filtering.</summary>
public class TaskContextInheritor : ITaskContextInheritor, IDisposable
{
    private readonly ILogger<TaskContextInheritor>? _logger;
    private readonly ITaskContextStore _taskContextStore;
    private readonly IContextWindowBudgeter _budgeter;

    /// <summary>Maximum tokens to propagate from parent to child. Respects budget limits.</summary>
    private const long MaxParentToChildPropagationTokens = 2048;

    public TaskContextInheritor(
        ILogger<TaskContextInheritor>? logger,
        ITaskContextStore taskContextStore,
        IContextWindowBudgeter budgeter)
    {
        _logger = logger;
        _taskContextStore = taskContextStore;
        _budgeter = budgeter;
    }

    /// <inheritdoc />
    public async Task<TaskContextSnapshot> CreateChildInheritanceAsync(Guid parentTaskId, Guid childTaskId)
    {
        var parentSnapshot = await _taskContextStore.GetByTaskIdAsync(parentTaskId);
        if (parentSnapshot == null)
        {
            // Return a default snapshot instead of throwing, for robustness
            return new TaskContextSnapshot
            {
                TaskId = childTaskId,
                Description = "No parent context available",
                CurrentState = AgentState.Planning,
                CompressedContext = new(),
                ToolResultsCache = new(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
        }

        // Build inherited snapshot — only propagate the most relevant info based on budget
        var childSnapshot = new TaskContextSnapshot
        {
            TaskId = childTaskId,
            Description = $"Inherited from parent task",
            CurrentState = AgentState.Planning,
            CompressedContext = parentSnapshot.CompressedContext?.Where(s => !s.IsSuppressed).ToList() ?? new(),
            ToolResultsCache = parentSnapshot.ToolResultsCache?.ToDictionary(k => k.Key, v => v.Value) ?? new(),
            ActiveFileTree = parentSnapshot.ActiveFileTree,
            GitStatusSnapshot = parentSnapshot.GitStatusSnapshot,
            RelevantEntities = parentSnapshot.RelevantEntities?.ToList() ?? new(),
            CompressedContextTokenCount = Math.Min(parentSnapshot.CompressedContextTokenCount, MaxParentToChildPropagationTokens),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ArchiveOnCompletion = false
        };

        // Budget-aware: only propagate if child has budget for it
        var childBudget = await _budgeter.GetOrCreateBudgetAsync(childTaskId);
        if (childBudget.RemainingTokens < 512 && parentSnapshot.CompressedContextTokenCount > MaxParentToChildPropagationTokens)
        {
            // Child is tight on budget — only propagate critical info
            childSnapshot.CompressedContext = parentSnapshot.CompressedContext != null ? FilterByRelevance(parentSnapshot.CompressedContext, topN: 8) : new();
            childSnapshot.RelevantEntities = parentSnapshot.RelevantEntities?.Take(10).ToList() ?? new();
        }

        _logger?.LogInformation("Created child inheritance for TaskId={ChildTaskId} from parent {ParentTaskId}",
            childTaskId, parentTaskId);

        return childSnapshot;
    }

    /// <inheritdoc />
    public async Task<AdditionalParentContext?> RequestAdditionalContextFromParentAsync(Guid parentId, Guid childId)
    {
        var parentSnapshot = await _taskContextStore.GetByTaskIdAsync(parentId);
        if (parentSnapshot == null || !parentSnapshot.CompressedContext.Any())
            return null;

        // Check if parent has budget to spare before propagating more context
        var parentBudget = await _budgeter.GetOrCreateBudgetAsync(parentId, 16384);
        if (parentBudget.RemainingTokens < 1024)
            return null; // Parent is tight on budget — don't propagate

        // Calculate which segments from the parent's compressed history are most relevant to the child
        var parentTokenCount = parentSnapshot.CompressedContextTokenCount;
        var budgetRatio = (double)parentBudget.RemainingTokens / 16384.0; // 0-1 ratio of remaining budget

        // Propagate proportionally based on parent's available budget
        var propagateLimit = (long)(MaxParentToChildPropagationTokens * Math.Max(0.25, budgetRatio));

        // Filter by relevance — segments with higher scores are more likely to be relevant to child
        var filteredSegments = FilterByRelevance(parentSnapshot.CompressedContext, topN: 16);

        // Add project state if available and child doesn't already have it
        string? additionalProjectState = null;
        if (parentSnapshot.ActiveFileTree != null && string.IsNullOrEmpty(parentSnapshot.GitStatusSnapshot))
            additionalProjectState = parentSnapshot.ActiveFileTree;

        var totalTokenCount = filteredSegments.Sum(s => s.TokenCount);

        return new AdditionalParentContext
        {
            RelevantHistorySegments = filteredSegments,
            ProjectStateSnapshot = additionalProjectState,
            TokenCount = Math.Min(totalTokenCount, propagateLimit)
        };
    }

    /// <summary>Filter segments by relevance score — keep top N highest-scoring segments.</summary>
    private static List<ContextSegment> FilterByRelevance(IEnumerable<ContextSegment> segments, int topN)
    {
        return segments.OrderByDescending(s => s.RelevanceScore).Take(topN).ToList();
    }

    public void Dispose()
    {
        // No unmanaged resources to clean up
    }
}