using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Manages per-chat conversation context with compression/injection capabilities.
/// </summary>
public interface IChatContextManager : IDisposable
{
    /// <summary>
    /// Gets the current compressed context window for a chat session.
    /// </summary>
    Task<ContextWindow> GetCompressedContextAsync(Guid chatId, CompressionLevel compressionLevel = CompressionLevel.Medium);

    /// <summary>
    /// Pins a segment from being compressed or reordered.
    /// </summary>
    Task PinSegmentAsync(Guid chatId, Guid segmentId);

    /// <summary>
    /// Unpins a previously pinned segment.
    /// </summary>
    Task UnpinSegmentAsync(Guid chatId, Guid segmentId);

    /// <summary>
    /// Suppresses a segment so it won't be sent to the AI (but remains in local history).
    /// </summary>
    Task SuppressSegmentAsync(Guid chatId, Guid segmentId);

    /// <summary>
    /// Reveals a previously suppressed segment.
    /// </summary>
    Task RevealSegmentAsync(Guid chatId, Guid segmentId);

    /// <summary>
    /// Injects custom context into the conversation (system prompt, file contents, etc.).
    /// </summary>
    Task<ContextSegment> InjectCustomContextAsync(Guid chatId, string content, ContextInjectionType injectionType);

    /// <summary>
    /// Removes injected custom context by segment ID.
    /// </summary>
    Task RemoveCustomContextAsync(Guid chatId, Guid segmentId);
}