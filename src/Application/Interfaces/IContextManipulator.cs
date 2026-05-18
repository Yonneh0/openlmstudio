using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Defines the actions a user can perform on context segments.
/// </summary>
public enum ContextManipulationAction
{
    /// <summary>Pin/freeze segment from being compressed or reordered.</summary>
    Pin,
    /// <summary>Unpin previously pinned segment.</summary>
    Unpin,
    /// <summary>Suppress/reveal toggle — exclude segment from AI context without deleting history.</summary>
    SuppressToggle,
    /// <summary>Remove segment from AI context (but preserve in local chat history).</summary>
    RemoveFromContext,
    /// <summary>Add custom context injection (system prompt, file contents, etc.).</summary>
    AddCustomContext
}

/// <summary>
/// Defines a user-driven manipulation request on a context segment.
/// </summary>
public record ContextManipulationRequest(
    Guid ChatId,
    Guid? SegmentId,  // null for AddCustomContext actions
    ContextManipulationAction Action,
    string? Content = null,  // content for AddCustomContext action
    ContextInjectionType InjectionType = ContextInjectionType.CustomInjection)
{
    public static ContextManipulationRequest PinSegment(Guid chatId, Guid segmentId) =>
        new(chatId, segmentId, ContextManipulationAction.Pin);

    public static ContextManipulationRequest UnpinSegment(Guid chatId, Guid segmentId) =>
        new(chatId, segmentId, ContextManipulationAction.Unpin);

    public static ContextManipulationRequest SuppressToggleSegment(Guid chatId, Guid segmentId) =>
        new(chatId, segmentId, ContextManipulationAction.SuppressToggle);

    public static ContextManipulationRequest RemoveFromContext(Guid chatId, Guid segmentId) =>
        new(chatId, segmentId, ContextManipulationAction.RemoveFromContext);

    public static ContextManipulationRequest AddCustomContext(Guid chatId, string content, ContextInjectionType injectionType = ContextInjectionType.CustomInjection) =>
        new(chatId, null!, ContextManipulationAction.AddCustomContext, content, injectionType);
}

/// <summary>
/// Defines a user-driven manipulation service for context segment control.
/// </summary>
public interface IContextManipulator : IDisposable
{
    /// <summary>
    /// Processes a user request to manipulate context (pin, suppress, add custom context).
    /// </summary>
    Task<ContextSegment?> ManipulateAsync(ContextManipulationRequest request);

    /// <summary>
    /// Gets all pinned segments for a chat.
    /// </summary>
    Task<List<ContextSegment>> GetPinnedSegmentsAsync(Guid chatId);

    /// <summary>
    /// Gets all suppressed segments for a chat.
    /// </summary>
    Task<List<ContextSegment>> GetSuppressedSegmentsAsync(Guid chatId);

    /// <summary>
    /// Gets all custom context injections for a chat (excluding system prompt).
    /// </summary>
    Task<List<ContextSegment>> GetCustomInjectionsAsync(Guid chatId, ContextInjectionType? injectionType = null);
}