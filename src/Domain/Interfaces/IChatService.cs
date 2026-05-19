namespace OpenLMStudio.Domain.Interfaces;

using Models;

/// <summary>
/// Service interface for chat conversation management.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Creates a new chat conversation.
    /// </summary>
    Task<Chat> CreateChatAsync(string title, string? modelId = null);

    /// <summary>
    /// Retrieves a chat by its identifier.
    /// </summary>
    Task<Chat?> GetChatAsync(Guid chatId);

    /// <summary>
    /// Lists all chats, optionally filtered by folder.
    /// </summary>
    Task<IReadOnlyList<Chat>> ListChatsAsync(Guid? folderId = null);

    /// <summary>
    /// Deletes a chat and all its associated messages.
    /// </summary>
    Task DeleteChatAsync(Guid chatId);

    /// <summary>
    /// Adds a message to an existing chat conversation.
    /// </summary>
    Task<Message> AddMessageAsync(Chat chat, string role, string content);
}