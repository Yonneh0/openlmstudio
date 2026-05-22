using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Service that provides context-aware suggestions for the next best action based on
/// the current state of the conversation, task context, and project state.
/// </summary>
public interface IContextAwareSuggestionService
{
    /// <summary>
    /// Gets context-aware suggestions for the next best action.
    /// </summary>
    /// <param name="conversationHistory">The current conversation messages.</param>
    /// <param name="taskContext">The current task context (if any).</param>
    /// <param name="projectState">The current project state as a directory path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of suggested actions with descriptions and confidence scores.</returns>
    Task<IReadOnlyList<ContextSuggestion>> GetNextActionSuggestionsAsync(
        IReadOnlyList<Message> conversationHistory,
        TaskContextSnapshot? taskContext,
        string? projectState,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A context-aware suggestion for the next action.
/// </summary>
public record ContextSuggestion(
    string Id,
    string Description,
    double Confidence,
    string Category,
    IReadOnlyList<string> Tags = default!);
