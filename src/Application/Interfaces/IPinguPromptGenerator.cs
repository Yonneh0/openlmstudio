using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Represents a Pingu task with its type and description.
/// </summary>
public record PinguTask(
    Guid Id,
    string Description,
    PinguTaskType TaskType,
    string? ContextHint = null);

/// <summary>
/// Configuration for dynamic system prompt generation.
/// </summary>
public record PinguPromptContext(
    string? CurrentTaskDescription,
    IReadOnlyList<PinguTask> AssignedTasks,
    string? AvailableToolDescriptions,
    string? ProjectStateSummary,
    string? ActiveTab,
    bool HasGgufModel,
    bool IsModelLoaded,
    string? CurrentModelName,
    string? CurrentPinguMood,
    string? ActivePanel,
    string? CurrentConversationSummary,
    IReadOnlyList<EngineLogEntry> RecentLogEntries = null!);

/// <summary>
/// Generates context-aware system prompts for Pingu based on its assigned tasks and current state.
/// </summary>
public interface IPinguPromptGenerator
{
    /// <summary>
    /// Generates the full system prompt for Pingu given the current context.
    /// </summary>
    string GenerateFullPrompt(PinguPromptContext context);

    /// <summary>
    /// Generates a compressed system prompt when context window is tight.
    /// </summary>
    string GenerateCompressedPrompt(PinguPromptContext context);

    /// <summary>
    /// Generates a task-specific prompt for a single assigned task type.
    /// </summary>
    string GenerateForTaskType(PinguTaskType taskType, PinguPromptContext context);
}