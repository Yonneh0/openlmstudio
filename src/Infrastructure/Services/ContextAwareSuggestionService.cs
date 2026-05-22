using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;
using Microsoft.Extensions.Logging;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Provides context-aware suggestions for the next best action based on
/// the current state of the conversation, task context, and project state.
/// </summary>
public class ContextAwareSuggestionService : IContextAwareSuggestionService
{
    private readonly ILogger<ContextAwareSuggestionService>? _logger;

    public ContextAwareSuggestionService(ILogger<ContextAwareSuggestionService>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContextSuggestion>> GetNextActionSuggestionsAsync(
        IReadOnlyList<Message> conversationHistory,
        TaskContextSnapshot? taskContext,
        string? projectState,
        CancellationToken cancellationToken = default)
    {
        var suggestions = new List<ContextSuggestion>();

        // 1. Conversation-based suggestions
        var conversationSuggestions = GenerateConversationSuggestions(conversationHistory);
        suggestions.AddRange(conversationSuggestions);

        // 2. Task-based suggestions
        if (taskContext != null)
        {
            var taskSuggestions = GenerateTaskSuggestions(taskContext);
            suggestions.AddRange(taskSuggestions);
        }

        // 3. Project state suggestions
        if (!string.IsNullOrEmpty(projectState))
        {
            var projectSuggestions = GenerateProjectSuggestions(projectState);
            suggestions.AddRange(projectSuggestions);
        }

        // Sort by confidence descending
        suggestions.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));

        _logger?.LogDebug("Generated {Count} context-aware suggestions", suggestions.Count);

        return suggestions.AsReadOnly();
    }

    private IEnumerable<ContextSuggestion> GenerateConversationSuggestions(IReadOnlyList<Message> history)
    {
        if (history.Count == 0)
        {
            yield return new ContextSuggestion(
                $"{Guid.NewGuid():N}",
                "Start a new conversation — type a prompt or question",
                0.95,
                "Conversation");
            yield break;
        }

        var lastMessage = history[history.Count - 1];

        // User just sent a message — suggest responding
        if (lastMessage.Role == MessageRole.User)
        {
            yield return new ContextSuggestion(
                $"{Guid.NewGuid():N}",
                "Generate AI response to the last user message",
                0.90,
                "Conversation");

            // Check if user message contains a command indicator
            if (lastMessage.Content.Contains("/run") || lastMessage.Content.Contains("/execute"))
            {
                yield return new ContextSuggestion(
                    $"{Guid.NewGuid():N}",
                    "Execute the command specified in the user message",
                    0.85,
                    "Conversation",
                    new[] { "command", "execution" });
            }
        }

        // AI just responded — suggest follow-up
        if (lastMessage.Role == MessageRole.Assistant)
        {
            yield return new ContextSuggestion(
                $"{Guid.NewGuid():N}",
                "Ask a follow-up question or provide feedback",
                0.80,
                "Conversation");

            // If AI offered to do something, suggest asking for that
            if (lastMessage.Content.Contains("I can") || lastMessage.Content.Contains("I'll"))
            {
                yield return new ContextSuggestion(
                    $"{Guid.NewGuid():N}",
                    "Ask the AI to perform the action it offered",
                    0.75,
                    "Conversation");
            }
        }

        // If conversation is long, suggest compression
        if (history.Count > 20)
        {
            yield return new ContextSuggestion(
                $"{Guid.NewGuid():N}",
                "Compress conversation history to reduce token usage",
                0.70,
                "Context Management");
        }
    }

    private IEnumerable<ContextSuggestion> GenerateTaskSuggestions(TaskContextSnapshot taskContext)
    {
        switch (taskContext.CurrentState)
        {
            case AgentState.Idle:
                yield return new ContextSuggestion(
                    $"{Guid.NewGuid():N}",
                    "Start a new task or assign one to the agent",
                    0.85,
                    "Task");
                break;

            case AgentState.Planning:
                yield return new ContextSuggestion(
                    $"{Guid.NewGuid():N}",
                    "Review the agent's proposed plan",
                    0.85,
                    "Task");
                yield return new ContextSuggestion(
                    $"{Guid.NewGuid():N}",
                    "Approve the plan and start execution",
                    0.80,
                    "Task");
                break;

            case AgentState.Acting:
                yield return new ContextSuggestion(
                    $"{Guid.NewGuid():N}",
                    "Monitor the agent's progress",
                    0.90,
                    "Task");
                yield return new ContextSuggestion(
                    $"{Guid.NewGuid():N}",
                    "Review tool output from the last action",
                    0.80,
                    "Task",
                    new[] { "review", "tool-output" });
                break;

            case AgentState.Paused:
                yield return new ContextSuggestion(
                    $"{Guid.NewGuid():N}",
                    "Resume the agent's execution",
                    0.85,
                    "Task");
                yield return new ContextSuggestion(
                    $"{Guid.NewGuid():N}",
                    "Cancel the paused task",
                    0.70,
                    "Task");
                break;

            case AgentState.Completed:
                yield return new ContextSuggestion(
                    $"{Guid.NewGuid():N}",
                    "Review completed changes",
                    0.85,
                    "Task");
                yield return new ContextSuggestion(
                    $"{Guid.NewGuid():N}",
                    "Archive task context",
                    0.70,
                    "Task",
                    new[] { "context", "archive" });
                break;

            case AgentState.Failed:
                yield return new ContextSuggestion(
                    $"{Guid.NewGuid():N}",
                    "Review error message and retry the task",
                    0.90,
                    "Task");
                yield return new ContextSuggestion(
                    $"{Guid.NewGuid():N}",
                    "Analyze failure and adjust approach",
                    0.75,
                    "Task");
                break;
        }

        // If task has been running for a while, suggest checking progress
        if (taskContext.CreatedAt != default && (DateTime.UtcNow - taskContext.CreatedAt).TotalMinutes > 5)
        {
            yield return new ContextSuggestion(
                $"{Guid.NewGuid():N}",
                "Check task progress and consider checkpointing",
                0.65,
                "Task");
        }
    }

    private List<ContextSuggestion> GenerateProjectSuggestions(string projectState)
    {
        // Check if project state path contains code files
        var codeFiles = new[] { ".cs", ".py", ".js", ".ts", ".rs", ".go", ".java" };

        try
        {
            var files = Directory.GetFiles(projectState, "*.*", SearchOption.AllDirectories)
                .Where(f => codeFiles.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .Take(10)
                .ToArray();

            if (files.Length > 0)
            {
                return new List<ContextSuggestion>
                {
                    new($"{Guid.NewGuid():N}", $"Found {files.Length} source file(s) in project", 0.80, "Project State", new[] { "code-files", "review" })
                };
            }
        }
        catch (UnauthorizedAccessException)
        {
            return new List<ContextSuggestion>
            {
                new($"{Guid.NewGuid():N}", "Project path exists but cannot be read — check permissions", 0.60, "Project State")
            };
        }
        catch (DirectoryNotFoundException)
        {
            return new List<ContextSuggestion>
            {
                new($"{Guid.NewGuid():N}", "Project path not found — check project state is current", 0.50, "Project State")
            };
        }

        return new List<ContextSuggestion>();
    }
}