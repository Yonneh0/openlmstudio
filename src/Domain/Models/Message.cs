namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Defines the role of a message participant in a conversation.
/// </summary>
public enum MessageRole
{
    /// <summary>
    /// The user who submitted the query or prompt.
    /// </summary>
    User,

    /// <summary>
    /// The AI assistant providing responses.
    /// </summary>
    Assistant,

    /// <summary>
    /// System-level instructions that configure behavior.
    /// </summary>
    System,

    /// <summary>
    /// Tool execution results returned to the conversation.
    /// </summary>
    Tool
}

/// <summary>
/// Represents a single tool invocation within an assistant message.
/// </summary>
public record ToolCall(
    string Id,
    string FunctionName,
    string ArgumentsJson,
    string? Result = null
);

/// <summary>
/// Represents a message in a conversation thread with support for content, tool calls, and multi-modal outputs.
/// </summary>
public class Message
{
    /// <summary>
    /// Unique identifier for this message (GUID).
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The role of the message sender.
    /// </summary>
    public MessageRole Role { get; set; }

    /// <summary>
    /// The text content or prompt submitted by this participant.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Collection of tool calls made during message generation (for assistant messages).
    /// </summary>
    public List<ToolCall> ToolCalls { get; set; } = new();

    /// <summary>
    /// Generated image outputs (for image generation responses).
    /// </summary>
    public List<ImageOutput> ImageOutputs { get; set; } = new();

    /// <summary>
    /// Number of tokens consumed by this message (approximate count).
    /// </summary>
    public int TokenCount { get; set; }

    /// <summary>
    /// Timestamp when the message was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this message represents a streaming response being built up token-by-token.
    /// </summary>
    public bool IsStreaming { get; set; }
}
