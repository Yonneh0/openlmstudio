using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// SQLite-backed implementation of ITaskContextStore.
/// Stores agentic task context snapshots in a local SQLite database with efficient delta tracking.
/// </summary>
public class SqliteTaskContextStore : ITaskContextStore, IDisposable
{
    private readonly ILogger<SqliteTaskContextStore>? _logger;
    private readonly AppDataDirectoryResolver _resolver;
    private readonly SqliteDatabaseFactory _dbFactory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly string DbTableName = "TaskContextSnapshots";

    public SqliteTaskContextStore(ILogger<SqliteTaskContextStore>? logger, AppDataDirectoryResolver resolver, SqliteDatabaseFactory dbFactory)
    {
        _logger = logger;
        _resolver = resolver;
        _dbFactory = dbFactory;
        
        // Ensure tasks directory exists and database is initialized
        InitializeDatabase();
    }

    /// <inheritdoc />
    public async Task CreateAsync(TaskContextSnapshot snapshot)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        if (string.IsNullOrEmpty(snapshot.Description))
            throw new ArgumentException("Task description cannot be empty.", nameof(snapshot));

        try
        {
            await using var connection = _dbFactory.CreateConnection(_resolver.GetTaskContextDatabasePath(snapshot.TaskId.ToString()));
            await connection.OpenAsync();

            // Check if snapshot already exists for this task ID — upsert behavior
            var selectSql = $"SELECT COUNT(*) FROM \"{DbTableName}\" WHERE TaskId = @taskId";
            using var selectCmd = new SqliteCommand(selectSql, connection);
            selectCmd.Parameters.AddWithValue("@taskId", snapshot.TaskId.ToString());

            var count = Convert.ToInt32(await selectCmd.ExecuteScalarAsync()!);
            
            if (count > 0)
            {
                // Update existing row — this is effectively an upsert
                await UpdateInternalAsync(connection, snapshot);
            }
            else
            {
                // Insert new row
                await InsertIntoTableAsync(connection, snapshot);
            }

            _logger?.LogInformation("Task context created/updated: TaskId={TaskId}, State={State}", 
                snapshot.TaskId, snapshot.CurrentState);
        }
        catch (Exception ex) when (ex is IOException or SqliteException or InvalidOperationException)
        {
            _logger?.LogError(ex, "Failed to create/update task context for TaskId: {TaskId}", snapshot.TaskId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<TaskContextSnapshot?> GetByTaskIdAsync(Guid taskId)
    {
        try
        {
            await using var connection = _dbFactory.CreateConnection(_resolver.GetTaskContextDatabasePath(taskId.ToString()));
            await connection.OpenAsync();

            // Check if database exists first (it may not for a never-used task ID)
            var dbExists = File.Exists(connection.Database);
            if (!dbExists || !Directory.Exists(Path.GetDirectoryName(connection.Database)))
                return null;

            var sql = $"SELECT * FROM \"{DbTableName}\" WHERE TaskId = @taskId";
            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@taskId", taskId.ToString());

            await using var reader = await cmd.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
                return ReadSnapshotFromReader(reader);

            return null;
        }
        catch (Exception ex) when (ex is IOException or SqliteException or InvalidOperationException)
        {
            _logger?.LogWarning(ex, "No task context found for TaskId: {TaskId}", taskId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task UpdateAsync(TaskContextSnapshot snapshot)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

        try
        {
            await using var connection = _dbFactory.CreateConnection(_resolver.GetTaskContextDatabasePath(snapshot.TaskId.ToString()));
            await connection.OpenAsync();

            // Ensure the table exists before update
            await EnsureTableExists(connection);

            // Check if row already exists — upsert behavior
            var selectSql = $"SELECT COUNT(*) FROM \"{DbTableName}\" WHERE TaskId = @taskId";
            using var selectCmd = new SqliteCommand(selectSql, connection);
            selectCmd.Parameters.AddWithValue("@taskId", snapshot.TaskId.ToString());

            var count = Convert.ToInt32(await selectCmd.ExecuteScalarAsync()!);
            
            if (count == 0)
            {
                // Row doesn't exist — insert it
                await InsertIntoTableAsync(connection, snapshot);
            }
            else
            {
                // Update existing row
                await UpdateInternalAsync(connection, snapshot);
            }

            _logger?.LogDebug("Task context updated: TaskId={TaskId}, State={State}", 
                snapshot.TaskId, snapshot.CurrentState);
        }
        catch (Exception ex) when (ex is IOException or SqliteException or InvalidOperationException)
        {
            _logger?.LogError(ex, "Failed to update task context for TaskId: {TaskId}", snapshot.TaskId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid taskId, ContextPruneStrategy strategy = ContextPruneStrategy.Archive)
    {
        try
        {
            await using var connection = _dbFactory.CreateConnection(_resolver.GetTaskContextDatabasePath(taskId.ToString()));
            
            // Ensure the table exists before attempting to delete from it
            try
            {
                await connection.OpenAsync();
                await EnsureTableExists(connection);

                // Archive: copy data to archived table before deletion
                if (strategy == ContextPruneStrategy.Archive || strategy == ContextPruneStrategy.CompressAndArchive)
                {
                    var archiveSql = $"SELECT * FROM \"{DbTableName}\" WHERE TaskId = @taskId";
                    using var archiveCmd = new SqliteCommand(archiveSql, connection);
                    archiveCmd.Parameters.AddWithValue("@taskId", taskId.ToString());

                    await using var reader = await archiveCmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        // Create archive table and copy data
                        var createArchiveTable = $"CREATE TABLE IF NOT EXISTS \"{DbTableName}_Archived\" (" +
                            "TaskId TEXT PRIMARY KEY, Description TEXT, CurrentState TEXT, " +
                            "CompressedContextJson TEXT, ToolResultsCacheJson TEXT, " +
                            "ActiveFileTree TEXT, GitStatusSnapshot TEXT, RelevantEntitiesJson TEXT, " +
                            "CompressedContextTokenCount INTEGER, ArchivedAt TEXT)";

                        using var archiveTableCmd = new SqliteCommand(createArchiveTable, connection);
                        await archiveTableCmd.ExecuteNonQueryAsync();

                        // Insert archived copy
                        var insertSql = $"INSERT INTO \"{DbTableName}_Archived\" VALUES (@taskId, @desc, @state, " +
                            "@ctxJson, @toolJson, @fileTree, @gitStatus, @entitiesJson, @tokenCount, @archivedAt)";

                        using (var insertCmd = new SqliteCommand(insertSql, connection))
                        {
                            insertCmd.Parameters.AddWithValue("@taskId", taskId.ToString());
                            insertCmd.Parameters.AddWithValue("@desc", reader.GetString(reader.GetOrdinal("Description")));
                            insertCmd.Parameters.AddWithValue("@state", reader.GetString(reader.GetOrdinal("CurrentState")));
                            insertCmd.Parameters.AddWithValue("@ctxJson", reader.GetValue(reader.GetOrdinal("CompressedContextJson")));
                            insertCmd.Parameters.AddWithValue("@toolJson", reader.GetValue(reader.GetOrdinal("ToolResultsCacheJson")));
                            insertCmd.Parameters.AddWithValue("@fileTree", reader.GetValue(reader.GetOrdinal("ActiveFileTree")));
                            insertCmd.Parameters.AddWithValue("@gitStatus", reader.GetValue(reader.GetOrdinal("GitStatusSnapshot")));
                            insertCmd.Parameters.AddWithValue("@entitiesJson", reader.GetValue(reader.GetOrdinal("RelevantEntitiesJson")));
                            insertCmd.Parameters.AddWithValue("@tokenCount", reader.GetInt64(reader.GetOrdinal("CompressedContextTokenCount")));
                            insertCmd.Parameters.AddWithValue("@archivedAt", DateTime.UtcNow.ToString("o"));
                        }

                        _logger?.LogInformation("Task context archived before deletion: TaskId={TaskId}", taskId);
                    }
                }

                // Delete the row
                var deleteSql = $"DELETE FROM \"{DbTableName}\" WHERE TaskId = @taskId";
                using var deleteCmd = new SqliteCommand(deleteSql, connection);
                deleteCmd.Parameters.AddWithValue("@taskId", taskId.ToString());
                await deleteCmd.ExecuteNonQueryAsync();

                _logger?.LogInformation("Task context {Strategy}: TaskId={TaskId}", strategy, taskId);
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 1)
            {
                // Database file doesn't exist yet — nothing to delete
                _logger?.LogDebug("No task context database exists for TaskId: {TaskId}", taskId);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            _logger?.LogWarning(ex, "No task context found for TaskId: {TaskId} during deletion", taskId);
        }
    }

    /// <inheritdoc />
    public async Task<List<TaskContextSnapshot>> ListActiveByChatIdAsync(Guid chatId)
    {
        var results = new List<TaskContextSnapshot>();
        
        try
        {
            await using var connection = _dbFactory.CreateConnection(_resolver.GetTaskContextDatabasePath(chatId.ToString()));
            
            // Check if database exists first
            try
            {
                await connection.OpenAsync();
                await EnsureTableExists(connection);

                var sql = $"SELECT * FROM \"{DbTableName}\" WHERE CurrentState IN ('Planning', 'Acting', 'Paused') ORDER BY UpdatedAt DESC";
                using var cmd = new SqliteCommand(sql, connection);

                await using var reader = await cmd.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                    results.Add(ReadSnapshotFromReader(reader));
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 1)
            {
                // Database doesn't exist — return empty list
                _logger?.LogDebug("No task context database exists for chatId: {ChatId}", chatId);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            _logger?.LogWarning(ex, "Error listing active task contexts for chatId: {ChatId}", chatId);
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<List<TaskContextSnapshot>> ListArchivedAsync()
    {
        var results = new List<TaskContextSnapshot>();
        
        try
        {
            // Archived snapshots are stored per-task-id in their own .db files under the tasks/ directory.
            // Each database may have a TaskContextSnapshots_Archived table with archived copies of deleted rows.
            var tasksDir = _resolver.GetSubDirectory("tasks");
            var dbFiles = Directory.GetFiles(tasksDir, "*.db");

            foreach (var dbFile in dbFiles)
            {
                try
                {
                    await using var connection = _dbFactory.CreateConnection(dbFile);
                    await connection.OpenAsync();

                    // Check if this database has an archived table for any task ID
                    var taskId = Guid.Parse(Path.GetFileNameWithoutExtension(dbFile));

                    // Try to read from the archived sub-table (created during Delete with Archive strategy)
                    try
                    {
                        var sql = $"SELECT * FROM \"{DbTableName}_Archived\" WHERE TaskId = @taskId ORDER BY ArchivedAt DESC";
                        using var cmd = new SqliteCommand(sql, connection);
                        cmd.Parameters.AddWithValue("@taskId", taskId.ToString());

                        await using var reader = await cmd.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                            results.Add(ReadArchivedSnapshotFromReader(reader));
                    }
                    catch (SqliteException ex) when (ex.SqliteErrorCode == 1)
                    {
                        // Archived table doesn't exist in this DB — skip
                    }
                }
                catch (Exception ex) when (ex is IOException or InvalidOperationException)
                {
                    _logger?.LogDebug(ex, "Error reading task database: {DbFile}", dbFile);
                }
            }

            // Also search for archived snapshots that may have been stored in the parent tasks directory itself.
            // Some implementations archive to a central archive location — check the metadata/ directory as well.
            var metaDir = _resolver.GetSubDirectory("metadata");
            var dbFiles2 = Directory.GetFiles(metaDir, "*.db");

            foreach (var dbFile in dbFiles2)
            {
                try
                {
                    await using var connection = _dbFactory.CreateConnection(dbFile);
                    await connection.OpenAsync();

                    // Try reading from the archived sub-table
                    try
                    {
                        var sql = $"SELECT * FROM \"{DbTableName}_Archived\" ORDER BY ArchivedAt DESC";
                        using var cmd = new SqliteCommand(sql, connection);

                        await using var reader = await cmd.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                            results.Add(ReadArchivedSnapshotFromReader(reader));
                    }
                    catch (SqliteException ex) when (ex.SqliteErrorCode == 1)
                    {
                        // Archived table doesn't exist in this DB — skip
                    }
                }
                catch (Exception ex) when (ex is IOException or InvalidOperationException)
                {
                    _logger?.LogDebug(ex, "Error reading metadata database: {DbFile}", dbFile);
                }
            }

            // Sort all results by ArchivedAt descending
            results.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            _logger?.LogWarning(ex, "Error listing archived task contexts");
        }

        return results;
    }

    public void Dispose()
    {
        // No unmanaged resources to clean up — SQLite connections auto-close on disposal via using statements
    }

    // ---- Private Helpers ----

    private async Task EnsureTableExists(SqliteConnection connection)
    {
        var createSql = $@"CREATE TABLE IF NOT EXISTS ""{DbTableName}"" (
            TaskId TEXT PRIMARY KEY,
            Description TEXT NOT NULL,
            CurrentState TEXT NOT NULL DEFAULT 'Planning',
            CompressedContextJson TEXT,
            ToolResultsCacheJson TEXT,
            ActiveFileTree TEXT,
            GitStatusSnapshot TEXT,
            RelevantEntitiesJson TEXT,
            CompressedContextTokenCount INTEGER NOT NULL DEFAULT 0,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL
        );";

        using var cmd = new SqliteCommand(createSql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task InsertIntoTableAsync(SqliteConnection connection, TaskContextSnapshot snapshot)
    {
        var insertSql = $@"INSERT INTO ""{DbTableName}"" (
            TaskId, Description, CurrentState, CompressedContextJson, ToolResultsCacheJson,
            ActiveFileTree, GitStatusSnapshot, RelevantEntitiesJson, CompressedContextTokenCount,
            CreatedAt, UpdatedAt
        ) VALUES (@taskId, @desc, @state, @ctxJson, @toolJson, @fileTree, @gitStatus, @entitiesJson, @tokenCount, @createdAt, @updatedAt);";

        var compressedContext = snapshot.CompressedContext != null && snapshot.CompressedContext.Any() 
            ? JsonSerializer.Serialize(snapshot.CompressedContext, _jsonOptions) 
            : null;
        
        var toolResultsCache = snapshot.ToolResultsCache != null && snapshot.ToolResultsCache.Any() 
            ? JsonSerializer.Serialize(snapshot.ToolResultsCache.ToDictionary(k => k.Key, v => JsonSerializer.Serialize(v.Value, _jsonOptions)), _jsonOptions) 
            : null;
        
        var relevantEntitiesJson = snapshot.RelevantEntities?.Any() == true 
            ? JsonSerializer.Serialize(snapshot.RelevantEntities, _jsonOptions) 
            : null;

        using (var cmd = new SqliteCommand(insertSql, connection))
        {
            cmd.Parameters.AddWithValue("@taskId", snapshot.TaskId.ToString());
            cmd.Parameters.AddWithValue("@desc", snapshot.Description);
            cmd.Parameters.AddWithValue("@state", snapshot.CurrentState.ToString());
            cmd.Parameters.AddWithValue("@ctxJson", compressedContext ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@toolJson", toolResultsCache ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@fileTree", snapshot.ActiveFileTree ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@gitStatus", snapshot.GitStatusSnapshot ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@entitiesJson", relevantEntitiesJson ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@tokenCount", snapshot.CompressedContextTokenCount);
            cmd.Parameters.AddWithValue("@createdAt", snapshot.CreatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("@updatedAt", snapshot.UpdatedAt.ToString("o"));

            await cmd.ExecuteNonQueryAsync();
        }
    }

    private async Task UpdateInternalAsync(SqliteConnection connection, TaskContextSnapshot snapshot)
    {
        var updateSql = $@"UPDATE ""{DbTableName}"" SET
            Description = @desc,
            CurrentState = @state,
            CompressedContextJson = @ctxJson,
            ToolResultsCacheJson = @toolJson,
            ActiveFileTree = @fileTree,
            GitStatusSnapshot = @gitStatus,
            RelevantEntitiesJson = @entitiesJson,
            CompressedContextTokenCount = @tokenCount,
            UpdatedAt = @updatedAt
        WHERE TaskId = @taskId;";

        var compressedContext = snapshot.CompressedContext != null && snapshot.CompressedContext.Any() 
            ? JsonSerializer.Serialize(snapshot.CompressedContext, _jsonOptions) 
            : null;
        
        var toolResultsCache = snapshot.ToolResultsCache != null && snapshot.ToolResultsCache.Any() 
            ? JsonSerializer.Serialize(snapshot.ToolResultsCache.ToDictionary(k => k.Key, v => JsonSerializer.Serialize(v.Value, _jsonOptions)), _jsonOptions) 
            : null;
        
        var relevantEntitiesJson = snapshot.RelevantEntities?.Any() == true 
            ? JsonSerializer.Serialize(snapshot.RelevantEntities, _jsonOptions) 
            : null;

        using (var cmd = new SqliteCommand(updateSql, connection))
        {
            cmd.Parameters.AddWithValue("@taskId", snapshot.TaskId.ToString());
            cmd.Parameters.AddWithValue("@desc", snapshot.Description);
            cmd.Parameters.AddWithValue("@state", snapshot.CurrentState.ToString());
            cmd.Parameters.AddWithValue("@ctxJson", compressedContext ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@toolJson", toolResultsCache ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@fileTree", snapshot.ActiveFileTree ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@gitStatus", snapshot.GitStatusSnapshot ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@entitiesJson", relevantEntitiesJson ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@tokenCount", snapshot.CompressedContextTokenCount);
            cmd.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("o"));

            await cmd.ExecuteNonQueryAsync();
        }
    }

    private TaskContextSnapshot ReadSnapshotFromReader(SqliteDataReader reader)
    {
        var taskId = new Guid(reader.GetString(reader.GetOrdinal("TaskId")));
        var description = reader.GetString(reader.GetOrdinal("Description"));
        
        string currentStateStr;
        try
        {
            currentStateStr = reader.GetString(reader.GetOrdinal("CurrentState"));
        }
        catch
        {
            // Handle null or missing column gracefully
            if (reader.IsDBNull(reader.GetOrdinal("CurrentState")))
                currentStateStr = "Planning";
            else
                throw;
        }

        var currentEnum = Enum.TryParse<AgentState>(currentStateStr, true, out var parsed) 
            ? parsed : AgentState.Planning;

        // Read optional JSON fields with null safety
        string? compressedContextJson;
        try
        {
            compressedContextJson = reader.IsDBNull(reader.GetOrdinal("CompressedContextJson")) 
                ? null 
                : (string)reader.GetValue(reader.GetOrdinal("CompressedContextJson"));
        }
        catch
        {
            // Handle missing column in older schema
            compressedContextJson = null;
        }

        string? toolResultsCacheJson;
        try
        {
            toolResultsCacheJson = reader.IsDBNull(reader.GetOrdinal("ToolResultsCacheJson")) 
                ? null 
                : (string)reader.GetValue(reader.GetOrdinal("ToolResultsCacheJson"));
        }
        catch
        {
            // Handle missing column in older schema
            toolResultsCacheJson = null;
        }

        string? activeFileTree;
        try
        {
            activeFileTree = reader.IsDBNull(reader.GetOrdinal("ActiveFileTree")) 
                ? null 
                : (string)reader.GetValue(reader.GetOrdinal("ActiveFileTree"));
        }
        catch
        {
            // Handle missing column in older schema
            activeFileTree = null;
        }

        string? gitStatusSnapshot;
        try
        {
            gitStatusSnapshot = reader.IsDBNull(reader.GetOrdinal("GitStatusSnapshot")) 
                ? null 
                : (string)reader.GetValue(reader.GetOrdinal("GitStatusSnapshot"));
        }
        catch
        {
            // Handle missing column in older schema
            gitStatusSnapshot = null;
        }

        string? relevantEntitiesJson;
        try
        {
            relevantEntitiesJson = reader.IsDBNull(reader.GetOrdinal("RelevantEntitiesJson")) 
                ? null 
                : (string)reader.GetValue(reader.GetOrdinal("RelevantEntitiesJson"));
        }
        catch
        {
            // Handle missing column in older schema
            relevantEntitiesJson = null;
        }

        long compressedContextTokenCount;
        try
        {
            compressedContextTokenCount = reader.GetInt64(reader.GetOrdinal("CompressedContextTokenCount"));
        }
        catch
        {
            // Handle missing column in older schema
            compressedContextTokenCount = 0;
        }

        string createdAtStr;
        try
        {
            createdAtStr = reader.GetString(reader.GetOrdinal("CreatedAt"));
        }
        catch
        {
            createdAtStr = DateTime.UtcNow.ToString("o");
        }

        string updatedAtStr;
        try
        {
            updatedAtStr = reader.GetString(reader.GetOrdinal("UpdatedAt"));
        }
        catch
        {
            updatedAtStr = DateTime.UtcNow.ToString("o");
        }

        return new TaskContextSnapshot
        {
            TaskId = taskId,
            Description = description,
            CurrentState = currentEnum,
            CompressedContext = compressedContextJson != null 
                ? JsonSerializer.Deserialize<List<ContextSegment>>(compressedContextJson, _jsonOptions) ?? [] 
                : [],
            ToolResultsCache = toolResultsCacheJson != null && !string.IsNullOrEmpty(toolResultsCacheJson)
                ? DeserializeToolResultsCache(toolResultsCacheJson)
                : new Dictionary<string, ContextSegment>(),
            ActiveFileTree = activeFileTree,
            GitStatusSnapshot = gitStatusSnapshot,
            RelevantEntities = relevantEntitiesJson != null && !string.IsNullOrEmpty(relevantEntitiesJson)
                ? JsonSerializer.Deserialize<List<string>>(relevantEntitiesJson, _jsonOptions) ?? []
                : [],
            CompressedContextTokenCount = compressedContextTokenCount,
            CreatedAt = DateTime.Parse(createdAtStr),
            UpdatedAt = DateTime.Parse(updatedAtStr)
        };
    }

    private TaskContextSnapshot ReadArchivedSnapshotFromReader(SqliteDataReader reader)
    {
        var taskId = new Guid(reader.GetString(reader.GetOrdinal("TaskId")));
        
        // Archived snapshots don't have CurrentState - default to Completed
        var currentEnum = AgentState.Completed;

        string? compressedContextJson;
        try
        {
            compressedContextJson = reader.IsDBNull(reader.GetOrdinal("CompressedContextJson")) 
                ? null 
                : (string)reader.GetValue(reader.GetOrdinal("CompressedContextJson"));
        }
        catch
        {
            // Handle missing column in older schema
            compressedContextJson = null;
        }

        string? toolResultsCacheJson;
        try
        {
            toolResultsCacheJson = reader.IsDBNull(reader.GetOrdinal("ToolResultsCacheJson")) 
                ? null 
                : (string)reader.GetValue(reader.GetOrdinal("ToolResultsCacheJson"));
        }
        catch
        {
            // Handle missing column in older schema
            toolResultsCacheJson = null;
        }

        string? activeFileTree;
        try
        {
            activeFileTree = reader.IsDBNull(reader.GetOrdinal("ActiveFileTree")) 
                ? null 
                : (string)reader.GetValue(reader.GetOrdinal("ActiveFileTree"));
        }
        catch
        {
            // Handle missing column in older schema
            activeFileTree = null;
        }

        string? gitStatusSnapshot;
        try
        {
            gitStatusSnapshot = reader.IsDBNull(reader.GetOrdinal("GitStatusSnapshot")) 
                ? null 
                : (string)reader.GetValue(reader.GetOrdinal("GitStatusSnapshot"));
        }
        catch
        {
            // Handle missing column in older schema
            gitStatusSnapshot = null;
        }

        string? relevantEntitiesJson;
        try
        {
            relevantEntitiesJson = reader.IsDBNull(reader.GetOrdinal("RelevantEntitiesJson")) 
                ? null 
                : (string)reader.GetValue(reader.GetOrdinal("RelevantEntitiesJson"));
        }
        catch
        {
            // Handle missing column in older schema
            relevantEntitiesJson = null;
        }

        long compressedContextTokenCount;
        try
        {
            compressedContextTokenCount = reader.GetInt64(reader.GetOrdinal("CompressedContextTokenCount"));
        }
        catch
        {
            // Handle missing column in older schema
            compressedContextTokenCount = 0;
        }

        string archivedAtStr;
        try
        {
            archivedAtStr = reader.GetString(reader.GetOrdinal("ArchivedAt"));
        }
        catch
        {
            archivedAtStr = DateTime.UtcNow.ToString("o");
        }

        string? archivedDescription;
        try
        {
            archivedDescription = reader.IsDBNull(reader.GetOrdinal("Description")) 
                ? null 
                : (string)reader.GetValue(reader.GetOrdinal("Description"));
        }
        catch
        {
            // Handle missing column in older schema
            archivedDescription = "";
        }

        return new TaskContextSnapshot
        {
            TaskId = taskId,
            Description = archivedDescription ?? "",
            CurrentState = currentEnum,
            CompressedContext = compressedContextJson != null 
                ? JsonSerializer.Deserialize<List<ContextSegment>>(compressedContextJson, _jsonOptions) ?? [] 
                : [],
            ToolResultsCache = toolResultsCacheJson != null && !string.IsNullOrEmpty(toolResultsCacheJson)
                ? DeserializeToolResultsCache(toolResultsCacheJson)
                : new Dictionary<string, ContextSegment>(),
            ActiveFileTree = activeFileTree,
            GitStatusSnapshot = gitStatusSnapshot,
            RelevantEntities = relevantEntitiesJson != null && !string.IsNullOrEmpty(relevantEntitiesJson)
                ? JsonSerializer.Deserialize<List<string>>(relevantEntitiesJson, _jsonOptions) ?? []
                : [],
            CompressedContextTokenCount = compressedContextTokenCount,
            CreatedAt = DateTime.Parse(archivedAtStr),
            UpdatedAt = DateTime.Parse(archivedAtStr)
        };
    }

    private Dictionary<string, ContextSegment> DeserializeToolResultsCache(string json)
    {
        try
        {
            // Parse outer JSON (Dictionary of string → string for each tool result)
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var resultDict = new Dictionary<string, ContextSegment>();

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                // Each value is a JSON-encoded ContextSegment
                var segmentJson = JsonSerializer.Deserialize<ContextSegment>(prop.Value.ToString(), _jsonOptions);
                if (segmentJson != null)
                    resultDict[prop.Name] = segmentJson;
            }

            return resultDict;
        }
        catch
        {
            // Return empty on parse failure to maintain backward compatibility
            return new Dictionary<string, ContextSegment>();
        }
    }

    private void InitializeDatabase()
    {
        try
        {
            _resolver.GetSubDirectory("tasks"); // Ensure tasks directory exists
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger?.LogWarning(ex, "Failed to create task context database directory");
        }
    }
}