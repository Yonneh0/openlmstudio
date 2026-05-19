using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Service interface for chat conversation management.
/// Provides operations for creating, loading, and managing chat conversations.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Creates a new chat conversation.
    /// </summary>
    /// <param name="title">The chat title.</param>
    /// <param name="modelId">The model to use for this chat.</param>
    /// <returns>The created chat entity.</returns>
    Task<Chat> CreateChatAsync(string title, Guid modelId);

    /// <summary>
    /// Gets a chat by ID.
    /// </summary>
    /// <param name="chatId">The chat ID.</param>
    /// <returns>The chat, or null if not found.</returns>
    Task<Chat?> GetChatAsync(Guid chatId);

    /// <summary>
    /// Lists all chat conversations.
    /// </summary>
    /// <param name="folderId">Optional folder to filter by.</param>
    /// <returns>List of chats.</returns>
    Task<IReadOnlyList<Chat>> ListChatsAsync(Guid? folderId = null);

    /// <summary>
    /// Deletes a chat conversation.
    /// </summary>
    /// <param name="chatId">The chat ID.</param>
    Task DeleteChatAsync(Guid chatId);

    /// <summary>
    /// Adds a message to a chat conversation.
    /// </summary>
    /// <param name="chat">The chat to add the message to.</param>
    /// <param name="role">The message role (user/assistant/system).</param>
    /// <param name="content">The message content.</param>
    /// <returns>The created message.</returns>
    Task<Message> AddMessageAsync(Chat chat, string role, string content);
}