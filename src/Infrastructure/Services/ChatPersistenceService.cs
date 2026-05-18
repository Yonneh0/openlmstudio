using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Persists and manages chat conversations to/from JSON files on disk.
/// Implements IConversationManager with Guid-based identifiers for full compatibility.
/// </summary>
public class ChatPersistenceService : IConversationManager, IDisposable
{
    private readonly ILogger<ChatPersistenceService> _logger;
    private readonly string _conversationsDirectory;

    // JSON serialization options for consistent formatting
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    // Token counting constants (approximate for English text)
    private const int TokensPerCharacter = 4;

    public ChatPersistenceService(ILogger<ChatPersistenceService> logger)
    {
        _logger = logger;
        
        // Default to user's OpenLMStudio data directory
        var userDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenLMStudio",
            "conversations");

        _conversationsDirectory = userDataPath;

        // Create the directory if it doesn't exist
        Directory.CreateDirectory(_conversationsDirectory);
    }

    /// <inheritdoc />
    public Task<IEnumerable<Chat>> ListChatsAsync()
    {
        try
        {
            var allFiles = Directory.GetFiles(_conversationsDirectory, "*.json", SearchOption.AllDirectories);
            
            var chats = new List<Chat>();
            foreach (var file in allFiles)
            {
                try
                {
                    var jsonContent = File.ReadAllText(file);
                    var chat = JsonSerializer.Deserialize<Chat>(jsonContent, JsonOptions);
                    
                    if (chat != null)
                        chats.Add(chat);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading conversation file: {FilePath}", file);
                }
            }

            // Sort by last update time descending
            return Task.FromResult<IEnumerable<Chat>>(chats.OrderByDescending(c => c.UpdatedAt));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing conversations");
            return Task.FromResult<IEnumerable<Chat>>(Enumerable.Empty<Chat>());
        }
    }

    /// <inheritdoc />
    public async Task<Chat> CreateChatAsync(string name, string? modelId = null)
    {
        var chat = new Chat
        {
            Id = Guid.NewGuid(),
            Name = name ?? $"Conversation {DateTime.UtcNow:yyyyMMdd-HHmmss}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Save the initial empty conversation file
        await SaveChatAsync(chat);

        _logger.LogInformation("Created new conversation: {ChatId} - '{Name}'", chat.Id, chat.Name);
        return chat;
    }

    /// <inheritdoc />
    public async Task<Chat?> LoadChatAsync(Guid chatId)
    {
        // Search in all folders for the chat file
        var files = Directory.GetFiles(_conversationsDirectory, "*.json", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            try
            {
                var jsonContent = File.ReadAllText(file);
                var chat = JsonSerializer.Deserialize<Chat>(jsonContent, JsonOptions);
                
                if (chat != null && chat.Id == chatId)
                    return chat;
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
                var jsonContent = File.ReadAllText(file);
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
        var chat = await LoadChatAsync(chatId);
        
        if (chat == null)
        {
            _logger.LogWarning("Cannot add message - conversation not found: {ChatId}", chatId);
            return;
        }

        // Add the message to the conversation
        chat.Messages ??= new List<Message>();

        chat.Messages.Add(message);
        chat.UpdatedAt = DateTime.UtcNow;

        // Update token count for the message
        message.TokenCount = EstimateTokenCount(message.Content);

        await SaveChatAsync(chat);
    }

    /// <inheritdoc />
    public Task<List<Message>> GetMessagesAsync(Guid chatId, int? limit = null)
    {
        var chat = LoadChatAsync(chatId).GetAwaiter().GetResult();
        
        if (chat == null || chat.Messages == null)
            return Task.FromResult(new List<Message>());

        var messages = limit.HasValue 
            ? chat.Messages.TakeLast(limit.Value).ToList() 
            : chat.Messages.ToList();

        return Task.FromResult(messages);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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
            }
        }

        chat.UpdatedAt = DateTime.UtcNow;
        await SaveChatAsync(chat);
    }

    /// <inheritdoc />
    public Task<int> CalculateTotalTokenCountAsync(Guid chatId)
    {
        var messages = GetMessagesAsync(chatId).GetAwaiter().GetResult();
        
        return Task.FromResult(messages.Sum(m => m.TokenCount > 0 ? m.TokenCount : EstimateTokenCount(m.Content)));
    }

    /// <inheritdoc />
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
            // Determine source file path for the chat (use StoragePath if available, otherwise search directories)
            string? sourcePath = null;
            
            if (!string.IsNullOrEmpty(chat.StoragePath))
            {
                var dir = Path.GetDirectoryName(chat.StoragePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    sourcePath = Path.Combine(dir, $"{chat.Id}.json");
                    
                    if (!File.Exists(sourcePath))
                    {
                        // Try root directory as fallback
                        sourcePath = Path.Combine(_conversationsDirectory, $"{chat.Id}.json");
                    }
                }
            }

            if (sourcePath == null || !File.Exists(sourcePath))
            {
                // Search in all folders for the chat file (same logic as LoadChatAsync)
                var files = Directory.GetFiles(_conversationsDirectory, "*.json", SearchOption.AllDirectories);
                
                foreach (var file in files)
                {
                    try
                    {
                        var jsonContent = File.ReadAllText(file);
                        var loadedChat = JsonSerializer.Deserialize<Chat>(jsonContent, JsonOptions);
                        
                        if (loadedChat != null && loadedChat.Id == chatId)
                        {
                            sourcePath = file;
                            break;
                        }
                    }
                    catch
                    {
                        // Skip files that can't be read
                    }
                }
                
                sourcePath ??= Path.Combine(_conversationsDirectory, $"{chat.Id}.json");
            }

            if (!File.Exists(sourcePath))
            {
                _logger.LogWarning("Chat file not found for export: {ChatId}", chatId);
                return;
            }

            // Copy to destination path (File.CopyAsync not available in .NET 8)
            File.Copy(sourcePath, destinationPath, overwrite: true);

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
            // Read the imported data
            var jsonContent = await File.ReadAllTextAsync(sourcePath);
            var chat = JsonSerializer.Deserialize<Chat>(jsonContent, JsonOptions);

            if (chat == null)
                return null;

            // Generate a new ID to avoid conflicts and save to current directory
            chat.Id = Guid.NewGuid();
            
            Directory.CreateDirectory(_conversationsDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(_conversationsDirectory, $"{chat.Id}.json"), 
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

    /// <inheritdoc />
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
        
        // Create a copy of messages with updated token counts and timestamps for persistence
        var persistedChat = new Chat
        {
            Id = chat.Id,
            Name = chat.Name,
            StoragePath = chat.StoragePath,
            CreatedAt = chat.CreatedAt,
            UpdatedAt = DateTime.UtcNow, // Always update the timestamp on save
            Messages = chat.Messages != null 
                ? chat.Messages.Select(m => new Message
                {
                    Id = m.Id,
                    Role = m.Role,
                    Content = m.Content,
                    ToolCalls = m.ToolCalls != null && m.ToolCalls.Any() 
                        ? new List<ToolCall>(m.ToolCalls.Select(tc => new ToolCall(tc.Id, tc.FunctionName, tc.ArgumentsJson, tc.Result)))
                        : new List<ToolCall>(),
                    TokenCount = m.TokenCount > 0 ? m.TokenCount : EstimateTokenCount(m.Content),
                    CreatedAt = m.CreatedAt,
                    IsStreaming = false // Never persist streaming state
                }).ToList() 
                : new List<Message>()
        };

        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(persistedChat, JsonOptions));
    }

    private static int EstimateTokenCount(string text) => 
        string.IsNullOrEmpty(text) ? 0 : Math.Max(1, (text.Length + TokensPerCharacter - 1) / TokensPerCharacter);
}