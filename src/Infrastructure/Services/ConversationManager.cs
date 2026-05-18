using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// File-based implementation of conversation persistence and management.
/// Uses Chat domain model exclusively - implements IConversationManager interface.
/// </summary>
public class FileConversationManager : IConversationManager, IDisposable
{
    private readonly ILogger<FileConversationManager> _logger;
    private readonly string _storagePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Creates a new file-based conversation manager.
    /// </summary>
    public FileConversationManager(ILogger<FileConversationManager> logger)
    {
        _logger = logger;
        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenLMStudio");
        _storagePath = Path.Combine(appDataPath, "chats");
        Directory.CreateDirectory(_storagePath);
    }

    public async Task<Chat> CreateChatAsync(string name, string? modelId = null)
    {
        var chat = new Chat
        {
            Id = Guid.NewGuid(),
            Name = name ?? "Untitled Chat",
            ModelId = modelId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await SaveChatAsync(chat);

        _logger.LogInformation("Created new conversation: {ChatId} - '{Name}'", chat.Id, chat.Name);
        return chat;
    }

    public async Task<Chat?> LoadChatAsync(Guid chatId)
    {
        var filePath = GetChatFilePath(chatId);

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Conversation not found: {Id}", chatId);
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var chat = JsonSerializer.Deserialize<Chat>(json, JsonOptions);

            if (chat != null)
            {
                // Ensure messages list is initialized
                chat.Messages ??= new List<Message>();
            }

            return chat;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load conversation: {Id}", chatId);
            return null;
        }
    }

    public async Task DeleteChatAsync(Guid chatId)
    {
        var filePath = GetChatFilePath(chatId);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation("Deleted conversation: {Id}", chatId);
        }
    }

    public async Task AddMessageAsync(Guid chatId, Message message)
    {
        var chat = await LoadChatAsync(chatId);

        if (chat == null)
        {
            _logger.LogWarning("Cannot add message - conversation not found: {ChatId}", chatId);
            return;
        }

        // Initialize messages list if needed
        chat.Messages ??= new List<Message>();

        // Set message ID and timestamp if not set
        if (message.Id == default)
            message.Id = Guid.NewGuid();
        if (!message.CreatedAt.Equals(default))
            message.CreatedAt = DateTime.UtcNow;

        chat.Messages.Add(message);
        chat.MessageCount = chat.Messages.Count;

        // Update token count for this message using consistent estimation method
        message.TokenCount = EstimateTokenCount(message.Content);

        chat.TotalTokenCount += message.TokenCount;
        chat.LastMessageAt = DateTime.UtcNow;
        chat.UpdatedAt = DateTime.UtcNow;

        await SaveChatAsync(chat);
    }

    public async Task<List<Message>> GetMessagesAsync(Guid chatId, int? limit = null)
    {
        var chat = await LoadChatAsync(chatId);

        if (chat == null || chat.Messages == null || chat.Messages.Count == 0)
            return new List<Message>();

        var messages = limit.HasValue
            ? chat.Messages.Skip(Math.Max(0, chat.Messages.Count - limit.Value)).ToList()
            : chat.Messages.ToList();

        return messages;
    }

    public async Task<IEnumerable<Chat>> SearchChatsAsync(string query)
    {
        try
        {
            var lowerQuery = query.ToLowerInvariant();
            var chats = await ListChatsAsync();

            // Search by name or content
            var results = new List<Chat>();
            foreach (var chat in chats)
            {
                if (!string.IsNullOrEmpty(chat.Name) &&
                    chat.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(chat);
                    continue;
                }

                // Also search message content
                if (chat.Messages != null)
                {
                    foreach (var msg in chat.Messages)
                    {
                        if (msg.Content?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            results.Add(chat);
                            break;
                        }
                    }
                }
            }

            return results.OrderByDescending(c => c.UpdatedAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching conversations");
            return Enumerable.Empty<Chat>();
        }
    }

    public async Task UpdateChatAsync(Guid chatId, object updates)
    {
        var chat = await LoadChatAsync(chatId);

        if (chat == null)
        {
            _logger.LogWarning("Cannot update - conversation not found: {ChatId}", chatId);
            return;
        }

        // Update properties based on the updates object using reflection
        foreach (var prop in updates.GetType().GetProperties())
        {
            var value = prop.GetValue(updates);

            switch (prop.Name.ToLowerInvariant())
            {
                case "name":
                    chat.Name = value as string ?? chat.Name;
                    break;
                case "modelid":
                    if (value != null)
                        chat.ModelId = Convert.ToString(value);
                    break;
                case "isactive":
                    if (value is bool isActive)
                        chat.IsActive = isActive;
                    break;
            }
        }

        chat.UpdatedAt = DateTime.UtcNow;
        await SaveChatAsync(chat);
    }

    public async Task<int> CalculateTotalTokenCountAsync(Guid chatId)
    {
        var messages = GetMessagesAsync(chatId).GetAwaiter().GetResult();

        return messages.Sum(m => m.TokenCount > 0 ? m.TokenCount : EstimateTokenCount(m.Content));
    }

    public async Task ExportChatAsync(Guid chatId, string destinationPath)
    {
        var chat = await LoadChatAsync(chatId);

        if (chat == null)
        {
            _logger.LogWarning("Cannot export - conversation not found: {ChatId}", chatId);
            return;
        }

        try
        {
            // Create a copy of messages with streaming state cleared for persistence
            var persistedChat = new Chat
            {
                Id = chat.Id,
                Name = chat.Name,
                StoragePath = chat.StoragePath,
                ModelId = chat.ModelId,
                SystemPrompt = chat.SystemPrompt,
                Temperature = chat.Temperature,
                MaxTokens = chat.MaxTokens,
                IsActive = false, // Never persist active state
                CreatedAt = chat.CreatedAt,
                UpdatedAt = DateTime.UtcNow,
                TotalTokenCount = chat.TotalTokenCount,
                MessageCount = chat.Messages?.Count ?? 0,
                Tags = new List<string>(chat.Tags),
                Messages = chat.Messages != null
                    ? chat.Messages.Select(m => new Message
                    {
                        Id = m.Id,
                        Role = m.Role,
                        Content = m.Content,
                        ToolCalls = m.ToolCalls != null ? new List<ToolCall>(m.ToolCalls) : new List<ToolCall>(),
                        TokenCount = m.TokenCount > 0 ? m.TokenCount : EstimateTokenCount(m.Content),
                        CreatedAt = m.CreatedAt,
                        IsStreaming = false // Never persist streaming state
                    }).ToList()
                    : new List<Message>()
            };

            var jsonContent = JsonSerializer.Serialize(persistedChat, JsonOptions);

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? _storagePath);
            await File.WriteAllTextAsync(destinationPath, jsonContent);

            _logger.LogInformation("Exported conversation: {ChatId} -> {DestinationPath}", chatId, destinationPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting conversation: {ChatId}", chatId);
        }
    }

    public async Task<Chat?> ImportChatAsync(string sourcePath)
    {
        try
        {
            // Read the imported data
            var jsonContent = await File.ReadAllTextAsync(sourcePath);
            var chat = JsonSerializer.Deserialize<Chat>(jsonContent, JsonOptions);

            if (chat == null)
                return null;

            // Generate a new ID to avoid conflicts and save to current directory
            chat.Id = Guid.NewGuid();
            chat.Messages ??= new List<Message>();

            Directory.CreateDirectory(_storagePath);
            await File.WriteAllTextAsync(
                Path.Combine(_storagePath, $"{chat.Id}.json"),
                JsonSerializer.Serialize(chat, JsonOptions));

            _logger.LogInformation("Imported conversation: {SourcePath} -> {ChatId}", sourcePath, chat.Id);
            return chat;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing conversation from: {SourcePath}", sourcePath);
            return null;
        }
    }

    public Task<IEnumerable<Chat>> ListChatsAsync()
    {
        try
        {
            if (!Directory.Exists(_storagePath))
                return Task.FromResult(Enumerable.Empty<Chat>());

            var allFiles = Directory.GetFiles(_storagePath, "*.json");

            var chats = new List<Chat>();
            foreach (var file in allFiles)
            {
                try
                {
                    var jsonContent = File.ReadAllText(file);
                    var chat = JsonSerializer.Deserialize<Chat>(jsonContent, JsonOptions);

                    if (chat != null && !string.IsNullOrEmpty(chat.Id.ToString()))
                        chats.Add(chat);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading conversation file: {FilePath}", file);
                }
            }

            // Sort by last update time descending
            var orderedChats = chats.OrderByDescending(c => c.UpdatedAt);
            return Task.FromResult<IEnumerable<Chat>>(orderedChats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing conversations");
            return Task.FromResult(Enumerable.Empty<Chat>());
        }
    }

    public long GetConversationTokenCount(string chatId)
    {
        try
        {
            var guid = Guid.TryParse(chatId, out var parsedGuid) ? parsedGuid : default(Guid);

            if (guid == default)
                return 0;

            return CalculateTotalTokenCountAsync(guid).GetAwaiter().GetResult();
        }
        catch
        {
            return 0;
        }
    }

    private async Task SaveChatAsync(Chat chat)
    {
        var filePath = GetChatFilePath(chat.Id);

        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? _storagePath);

        // Create a copy of messages with streaming state cleared for persistence
        // Also ensure ToolCalls is never null to avoid serialization issues
        var persistedChat = new Chat
        {
            Id = chat.Id,
            Name = chat.Name,
            StoragePath = chat.StoragePath,
            ModelId = chat.ModelId,
            SystemPrompt = chat.SystemPrompt,
            Temperature = chat.Temperature,
            MaxTokens = chat.MaxTokens,
            IsActive = false, // Never persist active state
            CreatedAt = chat.CreatedAt,
            UpdatedAt = DateTime.UtcNow, // Always update the timestamp on save
            TotalTokenCount = chat.TotalTokenCount,
            MessageCount = chat.MessageCount,
            Tags = new List<string>(chat.Tags ?? new List<string>()),
            Messages = chat.Messages?.Select(m => new Message
            {
                Id = m.Id,
                Role = m.Role,
                Content = m.Content,
                ToolCalls = m.ToolCalls != null ? new List<ToolCall>(m.ToolCalls) : new List<ToolCall>(),
                TokenCount = m.TokenCount > 0 ? m.TokenCount : EstimateTokenCount(m.Content),
                CreatedAt = m.CreatedAt,
                IsStreaming = false // Never persist streaming state
            }).ToList() ?? new List<Message>()
        };

        var jsonContent = JsonSerializer.Serialize(persistedChat, JsonOptions);
        await File.WriteAllTextAsync(filePath, jsonContent);
    }

    private string GetChatFilePath(Guid id)
        => Path.Combine(_storagePath, $"{id}.json");

    /// <summary>
    /// Standardized token counting method using consistent estimation: ~1 token per 4 characters for English.
    /// </summary>
    private static int EstimateTokenCount(string text) =>
        string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;

    public void Dispose()
    {
        // No unmanaged resources to clean up
    }
}