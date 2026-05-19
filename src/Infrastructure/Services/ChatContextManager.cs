using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Manages per-chat conversation context with compression/injection capabilities.
/// Implements IChatContextManager using SQLite-backed persistence for segment state.
/// </summary>
public class ChatContextManager : IChatContextManager, IDisposable
{
    private readonly ILogger<ChatContextManager> _logger;
    private readonly AppDataDirectoryResolver _resolver;
    private readonly SqliteDatabaseFactory _dbFactory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly string MessagesTableName = "ChatMessages";
    private static readonly string PinDbTableName = "PinSegmentStates";
    private static readonly string SuppressDbTableName = "SuppressSegmentStates";
    private static readonly string CustomInjectionsDbTableName = "CustomInjections";

    public ChatContextManager(
        ILogger<ChatContextManager> logger,
        AppDataDirectoryResolver resolver,
        SqliteDatabaseFactory dbFactory)
    {
        _logger = logger;
        _resolver = resolver;
        _dbFactory = dbFactory;

        // Fire-and-forget initialization — errors are logged and don't prevent construction
        _ = InitializeDatabasesAsync().ContinueWith(
            t => _logger?.LogError(t.Exception?.GetBaseException(), "Failed to initialize databases"),
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

        // Ensure all appdata subdirectories are created on first run
        _resolver.InitializeSubdirectories();
    }

    /// <inheritdoc />
    public async Task<ContextWindow> GetCompressedContextAsync(Guid chatId, CompressionLevel compressionLevel = CompressionLevel.Medium)
    {
        var window = ContextWindow.CreateEmpty();

        try
        {
            await using var connection = _dbFactory.CreateConnection(_resolver.GetConversationDatabasePath(chatId.ToString()));
            await connection.OpenAsync();

            // Get pinned segments (always included at full detail)
            var pinnedSegments = await GetPinnedSegmentsInternalAsync(connection, chatId);

            // Get suppressed segments to exclude them
            var suppressedSegmentIds = await GetSuppressedSegmentsInternalAsync(connection, chatId);

            // Get all regular context segments
            var allSegments = await GetAllContextSegmentsInternalAsync(connection, chatId);

            // Filter out suppressed segments from the main set
            var regularSegments = allSegments.Where(s => !suppressedSegmentIds.Contains(s.Id)).ToList();

            // Apply compression if needed (only to non-pinned segments)
            List<ContextSegment> compressedRegular;
            if (compressionLevel == CompressionLevel.None)
            {
                compressedRegular = regularSegments;
            }
            else
            {
                var compressor = new ConversationContextCompressor((ILogger<ConversationContextCompressor>?)null);
                var compressionResult = await compressor.CompressAsync(regularSegments, compressionLevel);

                // Keep pinned segments uncompressed and merge with compressed regular segments
                window.Segments.AddRange(pinnedSegments);
                window.Segments.AddRange(compressionResult.CompressedSegments);
            }

            // Get custom injections (always included at full detail)
            var customInjections = await GetCustomInjectionsInternalAsync(connection, chatId);
            window.Segments.AddRange(customInjections);

            // Calculate total token count
            foreach (var segment in window.Segments)
                window.TotalTokenCount += segment.TokenCount;

            window.OverallCompression = compressionLevel;

            _logger?.LogDebug("Compressed context retrieved for chat {ChatId}: {SegmentCount} segments, {TotalTokens} tokens",
                chatId, window.Segments.Count, window.TotalTokenCount);
        }
        catch (Exception ex) when (ex is IOException or Microsoft.Data.Sqlite.SqliteException)
        {
            _logger?.LogError(ex, "Failed to retrieve compressed context for chat: {ChatId}", chatId);
        }

        return window;
    }

    /// <inheritdoc />
    public Task PinSegmentAsync(Guid chatId, Guid segmentId) => UpdatePinStateInternal(chatId, segmentId, isPinned: true);

    /// <inheritdoc />
    public Task UnpinSegmentAsync(Guid chatId, Guid segmentId) => UpdatePinStateInternal(chatId, segmentId, isPinned: false);

    /// <inheritdoc />
    public Task SuppressSegmentAsync(Guid chatId, Guid segmentId) => UpdateSuppressStateInternal(chatId, segmentId, isSuppressed: true);

    /// <inheritdoc />
    public Task RevealSegmentAsync(Guid chatId, Guid segmentId) => UpdateSuppressStateInternal(chatId, segmentId, isSuppressed: false);

    /// <inheritdoc />
    public async Task<ContextSegment> InjectCustomContextAsync(Guid chatId, string content, ContextInjectionType injectionType)
    {
        try
        {
            var connection = _dbFactory.CreateConnection(_resolver.GetConversationDatabasePath(chatId.ToString()));
            await connection.OpenAsync();

            // Ensure custom injections table exists
            await EnsureCustomInjectionsTableExists(connection);

            var segment = new ContextSegment
            {
                Id = Guid.NewGuid(),
                Content = content,
                Role = MessageRole.System,
                IsPinned = true,
                IsSuppressed = false,
                TokenCount = EstimateTokenCount(content),
                InjectionType = injectionType
            };

            var sql = $@"INSERT INTO ""{CustomInjectionsDbTableName}"" (ChatId, SegmentId, Content, Role, TokenCount, InjectionType, CreatedAt) 
                        VALUES (@chatId, @segmentId, @content, @role, @tokenCount, @injectionType, @createdAt)";

            using var cmd = new Microsoft.Data.Sqlite.SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@chatId", chatId.ToString());
            cmd.Parameters.AddWithValue("@segmentId", segment.Id.ToString());
            cmd.Parameters.AddWithValue("@content", content);
            cmd.Parameters.AddWithValue("@role", ((int)segment.Role).ToString());
            cmd.Parameters.AddWithValue("@tokenCount", segment.TokenCount);
            cmd.Parameters.AddWithValue("@injectionType", ((int)segment.InjectionType).ToString());
            cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("o"));

            await cmd.ExecuteNonQueryAsync();

            _logger?.LogDebug("Custom context injected for chat {ChatId}: Type={InjectionType}",
                chatId, injectionType);

            return segment;
        }
        catch (Exception ex) when (ex is IOException or Microsoft.Data.Sqlite.SqliteException)
        {
            _logger?.LogError(ex, "Failed to inject custom context for chat: {ChatId}", chatId);
            throw;
        }
    }

    /// <inheritdoc />
    public Task RemoveCustomContextAsync(Guid chatId, Guid segmentId) => DeleteCustomInjectionInternal(chatId, segmentId);

    public void Dispose()
    {
        // No unmanaged resources to clean up — SQLite connections auto-close on disposal via using statements
    }

    private async Task UpdatePinStateInternal(Guid chatId, Guid segmentId, bool isPinned)
    {
        try
        {
            var connection = _dbFactory.CreateConnection(_resolver.GetConversationDatabasePath(chatId.ToString()));
            await connection.OpenAsync();

            // Ensure pin table exists
            await EnsurePinTableExists(connection);

            var sql = $@"INSERT OR REPLACE INTO ""{PinDbTableName}"" (ChatId, SegmentId, IsPinned) VALUES (@chatId, @segmentId, @isPinned)";

            using var cmd = new Microsoft.Data.Sqlite.SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@chatId", chatId.ToString());
            cmd.Parameters.AddWithValue("@segmentId", segmentId.ToString());
            cmd.Parameters.AddWithValue("@isPinned", isPinned ? 1 : 0);

            await cmd.ExecuteNonQueryAsync();

            _logger?.LogDebug("Pin state updated for segment {SegmentId} in chat {ChatId}: IsPinned={IsPinned}",
                segmentId, chatId, isPinned);
        }
        catch (Exception ex) when (ex is IOException or Microsoft.Data.Sqlite.SqliteException)
        {
            _logger?.LogError(ex, "Failed to update pin state for segment: {SegmentId} in chat: {ChatId}", segmentId, chatId);
        }
    }

    private async Task UpdateSuppressStateInternal(Guid chatId, Guid segmentId, bool isSuppressed)
    {
        try
        {
            var connection = _dbFactory.CreateConnection(_resolver.GetConversationDatabasePath(chatId.ToString()));
            await connection.OpenAsync();

            // Ensure suppress table exists
            await EnsureSuppressTableExists(connection);

            if (isSuppressed)
            {
                // Insert or replace suppression state
                var sql = $@"INSERT OR REPLACE INTO ""{SuppressDbTableName}"" (ChatId, SegmentId, IsSuppressed) VALUES (@chatId, @segmentId, @isSuppressed)";
                using var cmd = new Microsoft.Data.Sqlite.SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@chatId", chatId.ToString());
                cmd.Parameters.AddWithValue("@segmentId", segmentId.ToString());
                cmd.Parameters.AddWithValue("@isSuppressed", 1);
                await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                // Remove suppression state (un-suppress)
                var sql = $@"DELETE FROM ""{SuppressDbTableName}"" WHERE ChatId = @chatId AND SegmentId = @segmentId";
                using var cmd = new Microsoft.Data.Sqlite.SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@chatId", chatId.ToString());
                cmd.Parameters.AddWithValue("@segmentId", segmentId.ToString());
                await cmd.ExecuteNonQueryAsync();
            }

            _logger?.LogDebug("Suppress state updated for segment {SegmentId} in chat {ChatId}: IsSuppressed={IsSuppressed}",
                segmentId, chatId, isSuppressed);
        }
        catch (Exception ex) when (ex is IOException or Microsoft.Data.Sqlite.SqliteException)
        {
            _logger?.LogError(ex, "Failed to update suppress state for segment: {SegmentId} in chat: {ChatId}", segmentId, chatId);
        }
    }

    private async Task DeleteCustomInjectionInternal(Guid chatId, Guid segmentId)
    {
        try
        {
            var connection = _dbFactory.CreateConnection(_resolver.GetConversationDatabasePath(chatId.ToString()));
            await connection.OpenAsync();

            // Ensure custom injections table exists
            await EnsureCustomInjectionsTableExists(connection);

            var sql = $@"DELETE FROM ""{CustomInjectionsDbTableName}"" WHERE ChatId = @chatId AND SegmentId = @segmentId";
            using var cmd = new Microsoft.Data.Sqlite.SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@chatId", chatId.ToString());
            cmd.Parameters.AddWithValue("@segmentId", segmentId.ToString());

            await cmd.ExecuteNonQueryAsync();

            _logger?.LogDebug("Custom context removed for segment {SegmentId} in chat {ChatId}", segmentId, chatId);
        }
        catch (Exception ex) when (ex is IOException or Microsoft.Data.Sqlite.SqliteException)
        {
            _logger?.LogError(ex, "Failed to remove custom context for segment: {SegmentId} in chat: {ChatId}", segmentId, chatId);
        }
    }

    private async Task<List<ContextSegment>> GetPinnedSegmentsInternalAsync(Microsoft.Data.Sqlite.SqliteConnection connection, Guid chatId)
    {
        var pinned = new List<ContextSegment>();

        try
        {
            await EnsurePinTableExists(connection);

            // Fetch the segment IDs that are pinned for this chat
            await using (var cmd = new Microsoft.Data.Sqlite.SqliteCommand(
                $@"SELECT SegmentId FROM ""{PinDbTableName}"" WHERE ChatId = @chatId AND IsPinned = 1", connection))
            {
                cmd.Parameters.AddWithValue("@chatId", chatId.ToString());

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    pinned.Add(new ContextSegment
                    {
                        Id = new Guid(reader.GetString(reader.GetOrdinal("SegmentId"))),
                        IsPinned = true,
                        Role = MessageRole.System // Pinned segments default to system role context
                    });
                }
            }

            // Now fetch Content for each pinned segment from ChatMessages table
            if (pinned.Count > 0)
            {
                await EnsureChatMessagesTableExists(connection);

                foreach (var segment in pinned)
                {
                    try
                    {
                        using var cmd = new Microsoft.Data.Sqlite.SqliteCommand(
                            $@"SELECT Content, Role, TokenCount FROM ""{MessagesTableName}"" WHERE ChatId = @chatId AND SegmentId = @segmentId", connection);
                        cmd.Parameters.AddWithValue("@chatId", chatId.ToString());
                        cmd.Parameters.AddWithValue("@segmentId", segment.Id.ToString());

                        await using var reader = await cmd.ExecuteReaderAsync();
                        if (await reader.ReadAsync())
                        {
                            segment.Content = reader.IsDBNull(reader.GetOrdinal("Content")) ? string.Empty : reader.GetString(reader.GetOrdinal("Content"));
                            segment.Role = (MessageRole)int.Parse(reader.GetString(reader.GetOrdinal("Role")), System.Globalization.NumberStyles.Integer);
                            segment.TokenCount = reader.IsDBNull(reader.GetOrdinal("TokenCount")) ? 0 : Convert.ToInt32(reader.GetString(reader.GetOrdinal("TokenCount")));
                        }
                    }
                    catch (Exception ex) when (!(ex is IOException or Microsoft.Data.Sqlite.SqliteException))
                    {
                        _logger?.LogWarning(ex, "Failed to fetch content for pinned segment {SegmentId} in chat {ChatId}", segment.Id, chatId);
                    }
                }
            }
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 1)
        {
            // Table doesn't exist — no pinned segments
        }

        return pinned;
    }

    private async Task<List<Guid>> GetSuppressedSegmentsInternalAsync(Microsoft.Data.Sqlite.SqliteConnection connection, Guid chatId)
    {
        var suppressed = new List<Guid>();

        try
        {
            await EnsureSuppressTableExists(connection);

            var sql = $@"SELECT SegmentId FROM ""{SuppressDbTableName}"" WHERE ChatId = @chatId AND IsSuppressed = 1";
            using var cmd = new Microsoft.Data.Sqlite.SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@chatId", chatId.ToString());

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                suppressed.Add(new Guid(reader.GetString(reader.GetOrdinal("SegmentId"))));
            }
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 1)
        {
            // Table doesn't exist — no suppressed segments
        }

        return suppressed;
    }

    private async Task<List<ContextSegment>> GetCustomInjectionsInternalAsync(Microsoft.Data.Sqlite.SqliteConnection connection, Guid chatId)
    {
        var injections = new List<ContextSegment>();

        try
        {
            await EnsureCustomInjectionsTableExists(connection);

            var sql = $@"SELECT SegmentId, Content, Role, TokenCount, InjectionType FROM ""{CustomInjectionsDbTableName}"" WHERE ChatId = @chatId ORDER BY CreatedAt DESC";
            using var cmd = new Microsoft.Data.Sqlite.SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@chatId", chatId.ToString());

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string roleStr;
                try { roleStr = reader.GetString(reader.GetOrdinal("Role")); }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to read Role column for segment in chat {ChatId}", chatId);
                    continue;
                }

                int injectionTypeInt;
                try { injectionTypeInt = Convert.ToInt32(reader.GetString(reader.GetOrdinal("InjectionType"))); }
                catch (Exception ex) when (ex is FormatException or OverflowException)
                {
                    _logger?.LogWarning(ex, "Failed to read InjectionType column for segment in chat {ChatId}", chatId);
                    continue;
                }

                var segment = new ContextSegment
                {
                    Id = new Guid(reader.GetString(reader.GetOrdinal("SegmentId"))),
                    Content = reader.IsDBNull(reader.GetOrdinal("Content")) ? string.Empty : reader.GetString(reader.GetOrdinal("Content")),
                    Role = (MessageRole)int.Parse(roleStr, System.Globalization.NumberStyles.Integer),
                    TokenCount = reader.IsDBNull(reader.GetOrdinal("TokenCount")) ? 0 : Convert.ToInt32(reader.GetString(reader.GetOrdinal("TokenCount"))),
                    InjectionType = (ContextInjectionType)injectionTypeInt,
                    IsPinned = true // Custom injections are always pinned by default
                };

                injections.Add(segment);
            }
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 1)
        {
            // Table doesn't exist — no custom injections
        }

        return injections;
    }

    private async Task<List<ContextSegment>> GetAllContextSegmentsInternalAsync(Microsoft.Data.Sqlite.SqliteConnection connection, Guid chatId)
    {
        var segments = new List<ContextSegment>();

        try
        {
            await EnsureCustomInjectionsTableExists(connection);

            await EnsureChatMessagesTableExists(connection);

            // Get all regular context segments from the ChatMessages table (maps conversation messages to context segments)
            var msgSql = $@"SELECT SegmentId, Content, Role, TokenCount FROM ""{MessagesTableName}"" WHERE ChatId = @chatId ORDER BY CreatedAt DESC";
            using var cmd = new Microsoft.Data.Sqlite.SqliteCommand(msgSql, connection);
            cmd.Parameters.AddWithValue("@chatId", chatId.ToString());

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string roleStr;
                try { roleStr = reader.GetString(reader.GetOrdinal("Role")); }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to read Role column for segment in chat {ChatId}", chatId);
                    continue;
                }

                segments.Add(new ContextSegment
                {
                    Id = new Guid(reader.GetString(reader.GetOrdinal("SegmentId"))),
                    Content = reader.IsDBNull(reader.GetOrdinal("Content")) ? string.Empty : reader.GetString(reader.GetOrdinal("Content")),
                    Role = (MessageRole)int.Parse(roleStr, System.Globalization.NumberStyles.Integer),
                    TokenCount = reader.IsDBNull(reader.GetOrdinal("TokenCount")) ? 0 : Convert.ToInt32(reader.GetString(reader.GetOrdinal("TokenCount")))
                });
            }
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 1)
        {
            // Table doesn't exist yet — no context segments
        }

        return segments;
    }

    private async Task EnsurePinTableExists(Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        var createSql = $@"CREATE TABLE IF NOT EXISTS ""{PinDbTableName}"" (
            ChatId TEXT PRIMARY KEY,
            SegmentId TEXT PRIMARY KEY,
            IsPinned INTEGER NOT NULL DEFAULT 0,
            CONSTRAINT PK_PinSegmentStates PRIMARY KEY (ChatId, SegmentId)
        );";

        using var cmd = new Microsoft.Data.Sqlite.SqliteCommand(createSql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task EnsureSuppressTableExists(Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        var createSql = $@"CREATE TABLE IF NOT EXISTS ""{SuppressDbTableName}"" (
            ChatId TEXT PRIMARY KEY,
            SegmentId TEXT PRIMARY KEY,
            IsSuppressed INTEGER NOT NULL DEFAULT 0,
            CONSTRAINT PK_SuppressSegmentStates PRIMARY KEY (ChatId, SegmentId)
        );";

        using var cmd = new Microsoft.Data.Sqlite.SqliteCommand(createSql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task EnsureCustomInjectionsTableExists(Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        var createSql = $@"CREATE TABLE IF NOT EXISTS ""{CustomInjectionsDbTableName}"" (
            ChatId TEXT PRIMARY KEY,
            SegmentId TEXT PRIMARY KEY,
            Content TEXT NOT NULL,
            Role INTEGER NOT NULL DEFAULT 0,
            TokenCount INTEGER NOT NULL DEFAULT 0,
            InjectionType INTEGER NOT NULL DEFAULT 0,
            CreatedAt TEXT NOT NULL,
            CONSTRAINT PK_CustomInjections PRIMARY KEY (ChatId, SegmentId)
        );";

        using var cmd = new Microsoft.Data.Sqlite.SqliteCommand(createSql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task InitializeDatabasesAsync()
    {
        try
        {
            // Ensure contexts directory exists - per-chat DB files will be created lazily when needed
            _resolver.GetSubDirectory("contexts");

            // Ensure metadata directory exists for future use (settings, config)
            var metadataDir = Path.Combine(_resolver.GetAppDataDirectory(), "metadata");
            Directory.CreateDirectory(metadataDir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger?.LogWarning(ex, "Failed to create context database directory");
        }
    }

    private async Task EnsureChatMessagesTableExists(Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        var createSql = $@"CREATE TABLE IF NOT EXISTS ""{MessagesTableName}"" (
            ChatId TEXT NOT NULL,
            SegmentId TEXT PRIMARY KEY,
            Content TEXT NOT NULL DEFAULT '',
            Role INTEGER NOT NULL DEFAULT 0,
            TokenCount INTEGER NOT NULL DEFAULT 0,
            IsCompressed INTEGER NOT NULL DEFAULT 0,
            InjectionType INTEGER NOT NULL DEFAULT 0,
            CreatedAt TEXT NOT NULL
        );";

        using var cmd = new Microsoft.Data.Sqlite.SqliteCommand(createSql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    private static int EstimateTokenCount(string text) =>
        string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;
}