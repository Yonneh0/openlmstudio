namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Represents a conversation session, which contains a sequence of messages exchanged with the LLM.
/// A chat can contain multiple conversations organized by topic or task.
/// </summary>
public class Conversation
{
    /// <summary>
    /// Unique identifier for this conversation (GUID).
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Associated chat session ID that contains this conversation.
    /// </summary>
    public Guid ChatId { get; set; }

    /// <summary>
    /// Display name for the conversation (typically derived from first user message).
    /// </summary>
    public string Name { get; set; } = "New Conversation";

    /// <summary>
    /// First user message content used as preview in chat lists.
    /// </summary>
    public string? PreviewMessage { get; set; }

    /// <summary>
    /// Number of messages in this conversation session.
    /// </summary>
    public int MessageCount { get; set; }

    /// <summary>
    /// Total token count across all messages in this conversation.
    /// </summary>
    public int TotalTokenCount { get; set; }

    /// <summary>
    /// Timestamp when the conversation was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp of the last message sent in this conversation.
    /// </summary>
    public DateTime? LastMessageAt { get; set; }

    /// <summary>
    /// Whether this is currently the active/selected conversation.
    /// </summary>
    public bool IsActive { get; set; }
}