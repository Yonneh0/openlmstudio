using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

public class ChatService : Application.Interfaces.IChatService
{
    private readonly IConversationManager _conversationManager;
    private readonly ILogger<ChatService> _logger;

    public ChatService(IConversationManager conversationManager, ILogger<ChatService> logger)
    {
        _conversationManager = conversationManager;
        _logger = logger;
    }

    public async Task<Chat> CreateChatAsync(string title, Guid modelId)
    {
        try
        {
            var chat = await _conversationManager.CreateChatAsync(title, modelId.ToString());
            _logger.LogInformation("Created new chat: {ChatId} - '{Title}'", chat.Id, chat.Name);
            return chat;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create chat: {Title}", title);
            throw;
        }
    }

    public async Task<Chat?> GetChatAsync(Guid chatId)
    {
        try
        {
            var chat = await _conversationManager.LoadChatAsync(chatId);
            if (chat == null)
                _logger.LogWarning("Chat not found: {ChatId}", chatId);
            return chat;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load chat: {ChatId}", chatId);
            return null;
        }
    }

    public async Task<IReadOnlyList<Chat>> ListChatsAsync(Guid? folderId = null)
    {
        try
        {
            var chats = await _conversationManager.ListChatsAsync();
            return chats.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list chats");
            return new List<Chat>();
        }
    }

    public async Task DeleteChatAsync(Guid chatId)
    {
        try
        {
            await _conversationManager.DeleteChatAsync(chatId);
            _logger.LogInformation("Deleted chat: {ChatId}", chatId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete chat: {ChatId}", chatId);
            throw;
        }
    }

    public async Task<Message> AddMessageAsync(Chat chat, string role, string content)
    {
        try
        {
            var message = new Message
            {
                Role = Enum.Parse<MessageRole>(role, true),
                Content = content,
                TokenCount = EstimateTokenCount(content),
                CreatedAt = DateTime.UtcNow
            };

            await _conversationManager.AddMessageAsync(chat.Id, message);
            _logger.LogInformation("Added {Role} message to chat {ChatId}", role, chat.Id);
            return message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add message to chat {ChatId}", chat.Id);
            throw;
        }
    }

    private static int EstimateTokenCount(string text) =>
        string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;
}