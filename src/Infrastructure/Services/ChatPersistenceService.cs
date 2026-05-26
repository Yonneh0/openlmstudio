using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Consolidated, robust implementation of <see cref="IConversationManager"/>.
/// Persists chat conversations to JSON files on disk.
/// Merges the best of <see cref="ChatPersistenceService"/> and <see cref="FileConversationManager"/>.
/// </summary>
public class ChatPersistenceService : IConversationManager, IDisposable
{
    private readonly ILogger<ChatPersistenceService> _logger;
    private readonly string _conversationsDirectory;

    /// <summary>JSON serialization options for consistent formatting.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Standardized token counting: ~1 token per 4 characters for English text.
    /// </summary>
    private static int EstimateTokenCount(string text) =>
        string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;

    public ChatPersistenceService(ILogger<ChatPersistenceService> logger)
    {
        _logger = logger;

        var userDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenLMStudio",
            "conversations");

        _conversationsDirectory = userDataPath;
        Directory.CreateDirectory(_conversationsDirectory);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Chat>> ListChatsAsync()
    {
        try
        {
            var allFiles = Directory.GetFiles(_conversationsDirectory, "*.json", SearchOption.AllDirectories);

            var chats = new List<Chat>();
            foreach (var file in allFiles)
            {
                try
                {
                    var jsonContent = await File.ReadAllTextAsync(file).ConfigureAwait(false);
                    var chat = JsonSerializer.Deserialize<Chat>(jsonContent, JsonOptions);

                    if (chat != null)
                        chats.Add(chat);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading conversation file: {FilePath}", file);
                }
            }

            return chats.OrderByDescending(c => c.UpdatedAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing conversations");
            return Enumerable.Empty<Chat>();
        }
    }

    /// <inheritdoc />
    public async Task<Chat> CreateChatAsync(string name, string? modelId = null)
    {
        var chat = new Chat
        {
            Id = Guid.NewGuid(),
            Name = name ?? $"Conversation {DateTime.UtcNow:yyyyMMdd-HHmmss}",
            ModelId = modelId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            StoragePath = Path.Combine(_conversationsDirectory, $"{Guid.NewGuid():N}"),
        };

        await SaveChatAsync(chat);

        _logger.LogInformation("Created new conversation: {ChatId} - '{Name}'", chat.Id, chat.Name);
        return chat;
    }

    /// <inheritdoc />
    public async Task<Chat?> LoadChatAsync(Guid chatId)
    {
        var files = Directory.GetFiles(_conversationsDirectory, "*.json", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            try
            {
                var jsonContent = await File.ReadAllTextAsync(file).ConfigureAwait(false);
                var chat = JsonSerializer.Deserialize<Chat>(jsonContent, JsonOptions);

                if (chat != null && chat.Id == chatId)
                {
                    chat.Messages ??= new List<Message>();
                    return chat;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing conversation file: {FilePath}", file);
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async Task DeleteChatAsync(Guid chatId)
    {
        var files = Directory.GetFiles(_conversationsDirectory, "*.json", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            try
            {
                var jsonContent = await File.ReadAllTextAsync(file).ConfigureAwait(false);
                var chat = JsonSerializer.Deserialize<Chat>(jsonContent, JsonOptions);

                if (chat != null && chat.Id == chatId)
                {
                    File.Delete(file);
                    _logger.LogInformation("Deleted conversation: {ChatId}", chatId);
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting conversation file: {FilePath}", file);
            }
        }
    }

    /// <inheritdoc />
    public async Task AddMessageAsync(Guid chatId, Message message)
    {
        var chat = await LoadChatAsync(chatId).ConfigureAwait(false);

        if (chat == null)
        {
            _logger.LogWarning("Cannot add message - conversation not found: {ChatId}", chatId);
            return;
        }

        chat.Messages ??= new List<Message>();

        // Set message ID and timestamp if not set
        if (message.Id == default)
            message.Id = Guid.NewGuid();
        if (message.CreatedAt == default)
            message.CreatedAt = DateTime.UtcNow;

        // Update token count using the standardized formula
        message.TokenCount = EstimateTokenCount(message.Content);

        chat.Messages.Add(message);
        chat.MessageCount = chat.Messages.Count;
        chat.TotalTokenCount += message.TokenCount;
        chat.LastMessageAt = DateTime.UtcNow;
        chat.UpdatedAt = DateTime.UtcNow;

        await SaveChatAsync(chat);
    }

    /// <inheritdoc />
    public async Task<List<Message>> GetMessagesAsync(Guid chatId, int? limit = null)
    {
        var chat = await LoadChatAsync(chatId).ConfigureAwait(false);

        if (chat == null || chat.Messages == null || chat.Messages.Count == 0)
            return new List<Message>();

        var messages = limit.HasValue
            ? chat.Messages.Skip(Math.Max(0, chat.Messages.Count - limit.Value)).ToList()
            : chat.Messages.ToList();

        return messages;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Chat>> SearchChatsAsync(string query)
    {
        try
        {
            var lowerQuery = query.ToLowerInvariant();
            var chats = await ListChatsAsync().ConfigureAwait(false);

            var results = new List<Chat>();
            foreach (var chat in chats)
            {
                if (!string.IsNullOrEmpty(chat.Name) &&
                    chat.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(chat);
                    continue;
                }

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

    /// <inheritdoc />
    public async Task<IReadOnlyList<Message>> SearchMessagesInChatAsync(Guid chatId, string query)
    {
        var chat = await LoadChatAsync(chatId).ConfigureAwait(false);

        if (chat == null || chat.Messages == null || chat.Messages.Count == 0)
            return new List<Message>();

        try
        {
            var lowerQuery = query.ToLowerInvariant();
            var results = new List<Message>();

            foreach (var msg in chat.Messages)
            {
                if (msg.Content?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                    results.Add(msg);
            }

            return results.OrderBy(m => m.CreatedAt).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching messages in conversation: {ChatId}", chatId);
            return new List<Message>();
        }
    }

    /// <inheritdoc />
    public async Task UpdateChatAsync(Guid chatId, object updates)
    {
        var chat = await LoadChatAsync(chatId).ConfigureAwait(false);

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
                case "description":
                    if (value != null)
                        chat.Description = Convert.ToString(value);
                    break;
                case "systemprompt":
                    if (value != null)
                        chat.SystemPrompt = Convert.ToString(value);
                    break;
                case "temperature":
                    if (value is double temp)
                        chat.Temperature = temp;
                    break;
                case "maxtokens":
                    if (value is int maxTokens)
                        chat.MaxTokens = maxTokens;
                    break;
            }
        }

        chat.UpdatedAt = DateTime.UtcNow;
        await SaveChatAsync(chat);
    }

    /// <inheritdoc />
    public async Task<int> CalculateTotalTokenCountAsync(Guid chatId)
    {
        var messages = await GetMessagesAsync(chatId).ConfigureAwait(false);
        return messages.Sum(m => m.TokenCount > 0 ? m.TokenCount : EstimateTokenCount(m.Content));
    }

    /// <inheritdoc />
    public async Task ExportChatAsync(Guid chatId, string destinationPath)
    {
        var chat = await LoadChatAsync(chatId).ConfigureAwait(false);

        if (chat == null)
        {
            _logger.LogWarning("Cannot export - conversation not found: {ChatId}", chatId);
            return;
        }

        try
        {
            var persistedChat = new Chat
            {
                Id = chat.Id,
                Name = chat.Name,
                StoragePath = chat.StoragePath,
                ModelId = chat.ModelId,
                SystemPrompt = chat.SystemPrompt,
                Temperature = chat.Temperature,
                MaxTokens = chat.MaxTokens,
                Description = chat.Description,
                IsActive = false,
                CreatedAt = chat.CreatedAt,
                UpdatedAt = DateTime.UtcNow,
                TotalTokenCount = chat.TotalTokenCount,
                MessageCount = chat.Messages?.Count ?? 0,
                Tags = chat.Tags != null ? new List<string>(chat.Tags) : new List<string>(),
                ImageOutputs = chat.ImageOutputs != null ? new List<ImageOutput>(chat.ImageOutputs) : new List<ImageOutput>(),
                EmbeddingOutputs = chat.EmbeddingOutputs != null ? new List<byte[]>(chat.EmbeddingOutputs) : new List<byte[]>(),
                Messages = chat.Messages != null
                    ? chat.Messages.Select(m => new Message
                    {
                        Id = m.Id,
                        Role = m.Role,
                        Content = m.Content,
                        ToolCalls = m.ToolCalls != null ? new List<ToolCall>(m.ToolCalls) : new List<ToolCall>(),
                        ImageOutputs = m.ImageOutputs != null ? new List<ImageOutput>(m.ImageOutputs) : new List<ImageOutput>(),
                        EmbeddingOutputs = m.EmbeddingOutputs != null ? new List<float[]>(m.EmbeddingOutputs) : new List<float[]>(),
                        TokenCount = m.TokenCount > 0 ? m.TokenCount : EstimateTokenCount(m.Content),
                        CreatedAt = m.CreatedAt,
                        IsStreaming = false
                    }).ToList()
                    : new List<Message>()
            };

            var jsonContent = JsonSerializer.Serialize(persistedChat, JsonOptions);

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? _conversationsDirectory);
            await File.WriteAllTextAsync(destinationPath, jsonContent);

            _logger.LogInformation("Exported conversation: {ChatId} -> {DestinationPath}", chatId, destinationPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting conversation: {ChatId}", chatId);
        }
    }

    /// <inheritdoc />
    public async Task<Chat?> ImportChatAsync(string sourcePath)
    {
        try
        {
            var jsonContent = await File.ReadAllTextAsync(sourcePath);
            var chat = JsonSerializer.Deserialize<Chat>(jsonContent, JsonOptions);

            if (chat == null)
                return null;

            chat.Id = Guid.NewGuid();
            chat.Messages ??= new List<Message>();
            chat.Name ??= "Untitled Chat";

            Directory.CreateDirectory(_conversationsDirectory);
            var filePath = Path.Combine(_conversationsDirectory, $"{chat.Id}.json");
            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(chat, JsonOptions));

            chat.StoragePath = filePath;

            _logger.LogInformation("Imported conversation '{Name}' ({SourcePath}) -> {ChatId}", chat.Name, sourcePath, chat.Id);
            return chat;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing conversation from: {SourcePath}", sourcePath);
            return null;
        }
    }

    /// <inheritdoc />
    public long GetConversationTokenCount(string chatId)
    {
        try
        {
            if (!Guid.TryParse(chatId, out var parsedGuid))
            {
                _logger.LogWarning("Invalid chatId format: {ChatId}", chatId);
                return 0;
            }

            return (int)CalculateTotalTokenCountAsync(parsedGuid).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting token count for {ChatId}", chatId);
            return 0;
        }
    }

    /// <inheritdoc />
    public async Task RenameChatAsync(Guid chatId, string newName)
    {
        var chat = await LoadChatAsync(chatId).ConfigureAwait(false);
        if (chat == null)
        {
            _logger.LogWarning("Cannot rename - conversation not found: {ChatId}", chatId);
            return;
        }

        chat.Name = newName;
        chat.UpdatedAt = DateTime.UtcNow;
        await SaveChatAsync(chat);

        _logger.LogInformation("Renamed chat {ChatId} to \"{Name}\"", chatId, newName);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // No unmanaged resources to clean up
    }

    // ---- Private Helpers ----

    private async Task SaveChatAsync(Chat chat)
    {
        var directory = !string.IsNullOrEmpty(chat.StoragePath)
            ? Path.GetDirectoryName(chat.StoragePath)
            ?? _conversationsDirectory
            : _conversationsDirectory;

        Directory.CreateDirectory(directory);

        var filePath = Path.Combine(directory, $"{chat.Id}.json");

        // Create a copy of messages with all properties for persistence
        var persistedChat = new Chat
        {
            Id = chat.Id,
            Name = chat.Name,
            StoragePath = filePath,
            ModelId = chat.ModelId,
            SystemPrompt = chat.SystemPrompt,
            Temperature = chat.Temperature,
            MaxTokens = chat.MaxTokens,
            Description = chat.Description,
            IsActive = chat.IsActive,
            CreatedAt = chat.CreatedAt,
            UpdatedAt = DateTime.UtcNow,
            TotalTokenCount = chat.TotalTokenCount,
            MessageCount = chat.MessageCount,
            Tags = chat.Tags != null ? new List<string>(chat.Tags) : new List<string>(),
            ImageOutputs = chat.ImageOutputs != null ? new List<ImageOutput>(chat.ImageOutputs) : new List<ImageOutput>(),
            EmbeddingOutputs = chat.EmbeddingOutputs != null ? new List<byte[]>(chat.EmbeddingOutputs) : new List<byte[]>(),
            Messages = chat.Messages?.Select(m => new Message
            {
                Id = m.Id,
                Role = m.Role,
                Content = m.Content,
                ToolCalls = m.ToolCalls != null ? new List<ToolCall>(m.ToolCalls) : new List<ToolCall>(),
                ImageOutputs = m.ImageOutputs != null ? new List<ImageOutput>(m.ImageOutputs) : new List<ImageOutput>(),
                EmbeddingOutputs = m.EmbeddingOutputs != null ? new List<float[]>(m.EmbeddingOutputs) : new List<float[]>(),
                TokenCount = m.TokenCount > 0 ? m.TokenCount : EstimateTokenCount(m.Content),
                CreatedAt = m.CreatedAt,
                IsStreaming = false
            }).ToList() ?? new List<Message>()
        };

        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(persistedChat, JsonOptions));
    }
}