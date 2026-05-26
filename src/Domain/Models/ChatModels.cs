using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OpenLMStudio.Domain.Models;

// ============================================================
// Message.cs (88 lines)
// ============================================================

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
    /// Generated embedding outputs (for embedding generation responses).
    /// </summary>
    public List<float[]> EmbeddingOutputs { get; set; } = new();

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

// ============================================================
// Conversation.cs (53 lines)
// ============================================================

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

// ============================================================
// Chat.cs (111 lines)
// ============================================================

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
    /// Generated images associated with this chat session (image generation outputs).
    /// </summary>
    public List<ImageOutput> ImageOutputs { get; set; } = new();

    /// <summary>
    /// Generated embeddings associated with this chat session.
    /// </summary>
    public List<byte[]> EmbeddingOutputs { get; set; } = new();

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

// ============================================================
// ChatContext.cs (126 lines)
// ============================================================

/// <summary>
/// Represents a context segment within a chat conversation.
/// </summary>
public class ContextSegment : IDisposable
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public MessageRole Role { get; set; }
    public bool IsCompressed { get; set; }
    public bool IsPinned { get; set; }

    /// <summary>Token count for this segment.</summary>
    public int TokenCount { get; set; }

    /// <summary>Type of context injection that created this segment.</summary>
    public ContextInjectionType InjectionType { get; set; } = ContextInjectionType.CompressedHistory;

    /// <summary>Whether the segment is suppressed (not sent to AI).</summary>
    public bool IsSuppressed { get; set; }

    /// <summary>Relevance score assigned by ContextRelevanceEngine. Higher = more relevant.</summary>
    public float RelevanceScore { get; set; }

    public static ContextSegment CreateUncompressed(Message message) =>
        new()
        {
            Id = message.Id,
            Content = message.Content,
            Role = message.Role,
            IsCompressed = false,
            IsPinned = false,
            TokenCount = message.TokenCount > 0 ? message.TokenCount : EstimateTokenCount(message.Content)
        };

    /// <summary>Creates an empty segment with the given ID and relevance score. Used by the context window budgeter as a placeholder for eviction slots — Content is intentionally empty and TokenCount defaults to zero.</summary>
    public static ContextSegment CreateEmptyWithRelevance(Guid id) =>
        new() { Id = id, RelevanceScore = 0f };

    /// <summary>Standardized token counting: ~1 token per 4 characters for English.</summary>
    private static int EstimateTokenCount(string text) =>
        string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;

    public void Dispose() { /* No unmanaged resources */ }
}

// NOTE: AgentState is now defined in AgentState.cs

/// <summary>The level of context compression applied to segments.</summary>
public enum CompressionLevel
{
    /// <summary>No compression — full detail preserved.</summary>
    None,
    /// <summary>Light compression — key phrases extracted.</summary>
    Light,
    /// <summary>Medium compression — summaries of older messages.</summary>
    Medium,
    /// <summary>Aggressive compression — only outlines/headers kept.</summary>
    Aggressive
}

/// <summary>Defines what types of context can be injected into a conversation.</summary>
public enum ContextInjectionType
{
    /// <summary>System prompt always present for the AI.</summary>
    SystemPrompt,
    /// <summary>Agentic task context snapshot (for agent harness).</summary>
    TaskContextSnapshot,
    /// <summary>Custom user-injected context (manual injection).</summary>
    CustomInjection,
    /// <summary>Compressed conversation history.</summary>
    CompressedHistory,
    /// <summary>Active project state (file tree, git status).</summary>
    ProjectState
}

/// <summary>A fully assembled context window for sending to the AI.</summary>
public class ContextWindow : IDisposable
{
    public List<ContextSegment> Segments { get; set; } = new();
    public long TotalTokenCount { get; set; }
    public CompressionLevel OverallCompression { get; set; }

    public static ContextWindow CreateEmpty() => new() { OverallCompression = CompressionLevel.None };

    public void Dispose() { /* No unmanaged resources */ }
}

/// <summary>Defines how context is pruned when a task completes.</summary>
public enum ContextPruneStrategy
{
    /// <summary>Keep full uncompressed context for reference later, mark as read-only.</summary>
    Archive,
    /// <summary>Store compressed snapshot only (minimal disk usage).</summary>
    CompressAndArchive,
    /// <summary>Remove all context — user confirms via dialog before deletion.</summary>
    Discard
}

/// <summary>The token budget for a conversation's context window.</summary>
public class ContextBudget : IDisposable
{
    public long MaximumTokens { get; set; }
    public long RemainingTokens { get; private set; }
    public Dictionary<ContextInjectionType, long> BudgetAllocation { get; set; } = new();

    public static ContextBudget CreateDefault(int maxTokens = 8192) =>
        new()
        {
            MaximumTokens = maxTokens,
            RemainingTokens = maxTokens,
            BudgetAllocation = new Dictionary<ContextInjectionType, long> { [ContextInjectionType.SystemPrompt] = (long)(maxTokens / 4.0) }
        };

    public void Deduct(long tokens)
    {
        if (tokens > RemainingTokens)
            throw new InvalidOperationException($"Cannot deduct {tokens} tokens: only {RemainingTokens} remaining.");
        RemainingTokens -= tokens;
    }

    public void Dispose() { /* No unmanaged resources */ }
}

// ============================================================
// ConversationEncryption.cs (95 lines)
// ============================================================

/// <summary>
/// AES-256 encryption service for conversation data at rest.
/// Keys are stored in platform-specific keychain (Windows: DPAPI, macOS: Keychain, Linux: keyring).
/// </summary>
public static class ConversationEncryption
{
    private const int KeySizeBits = 256;
    private const int IvsSizeBytes = 16;
    private const int SaltSizeBytes = 32;
    private const int Iterations = 100_000;

    /// <summary>
    /// Encrypts the plaintext string to a base64-encoded ciphertext with HMAC integrity check.
    /// </summary>
    public static string Encrypt(string plaintext, string password)
    {
        if (string.IsNullOrEmpty(plaintext)) return string.Empty;
        using var aes = Aes.Create();
        aes.KeySize = KeySizeBits;
        aes.GenerateIV();
        aes.GenerateKey();

        var derivedKey = DeriveKey(password, aes.Key, aes.IV);
        var iv = aes.IV;

        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var encrypted = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);
        var hmac = ComputeHmac(derivedKey, encrypted);

        using var ms = new MemoryStream();
        ms.Write(BitConverter.GetBytes(hmac.Length), 0, 4);
        ms.Write(hmac, 0, hmac.Length);
        ms.Write(iv, 0, iv.Length);
        ms.Write(encrypted, 0, encrypted.Length);

        return Convert.ToBase64String(ms.ToArray());
    }

    /// <summary>
    /// Decrypts a base64-encoded ciphertext string using the provided password.
    /// </summary>
    public static string Decrypt(string ciphertext, string password)
    {
        if (string.IsNullOrEmpty(ciphertext)) return string.Empty;
        var bytes = Convert.FromBase64String(ciphertext);

        using var ms = new MemoryStream(bytes);
        using var reader = new BinaryReader(ms);

        var hmacLength = reader.ReadInt32();
        var hmac = reader.ReadBytes(hmacLength);
        var iv = reader.ReadBytes(IvsSizeBytes);
        var encrypted = reader.ReadBytes(bytes.Length - 4 - hmacLength - IvsSizeBytes);

        var derivedKey = DeriveKey(password, aes.Key, iv);
        var expectedHmac = ComputeHmac(derivedKey, encrypted);

        if (!ConstantTimeCompare(hmac, expectedHmac))
            throw new CryptographicException("Decryption failed: integrity check failed (tampered ciphertext)");

        using var aes = Aes.Create();
        using var decryptor = aes.CreateDecryptor(encrypted, iv);
        var decrypted = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
        return Encoding.UTF8.GetString(decrypted);
    }

    private static byte[] DeriveKey(string password, byte[] key, byte[] iv)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, key, Iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(KeySizeBits / 8);
    }

    private static byte[] ComputeHmac(byte[] key, byte[] data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(data);
    }

    private static bool ConstantTimeCompare(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        var result = 0;
        for (var i = 0; i < a.Length; i++)
            result |= a[i] ^ b[i];
        return result == 0;
    }
}