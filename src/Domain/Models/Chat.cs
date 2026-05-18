namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Represents a chat session grouping, containing related conversations organized by topic or project.
/// </summary>
public class Chat
{
    /// <summary>
    /// Unique identifier for this chat session (GUID).
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Name of the chat session.
    /// </summary>
    public string Name { get; set; } = "Untitled Chat";

    /// <summary>
    /// Optional description or purpose for this chat.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Folder path where this chat's messages are stored on disk.
    /// </summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>
    /// Associated model ID for this chat session (null means system default).
    /// </summary>
    public string? ModelId { get; set; }

    /// <summary>
    /// System prompt override for this specific chat.
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// Temperature setting for response generation (0.0 - 2.0).
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// Maximum number of tokens to generate in a single response.
    /// </summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    /// Whether this chat is currently active/selected.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Timestamp when the chat was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp of the last message sent in this chat.
    /// </summary>
    public DateTime? LastMessageAt { get; set; }

    /// <summary>
    /// Timestamp when the chat was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Total token count across all messages in this chat.
    /// </summary>
    public int TotalTokenCount { get; set; }

    /// <summary>
    /// Number of messages in this chat session.
    /// </summary>
    public int MessageCount { get; set; }

    /// <summary>
    /// Collection of messages in this chat session.
    /// </summary>
    public List<Message>? Messages { get; set; } = new();

    /// <summary>
    /// Tags for organizing and categorizing chats.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    // ---- Legacy property aliases for backward compatibility ----

    /// <summary>
    /// Conversation folder ID (alias for StoragePath).
    /// </summary>
    [Obsolete("Use StoragePath instead. Kept for serialization compatibility with existing chat files.")]
    public string? ConversationFolderId => !string.IsNullOrEmpty(StoragePath) ? Path.GetFileNameWithoutExtension(StoragePath) : null;

    /// <summary>
    /// Alias for ModelId for backward compatibility.
    /// </summary>
    [Obsolete("Use ModelId instead. Kept for serialization compatibility with existing chat files.")]
    public string? ModelIdentifier => ModelId;
}
