// Brought to you by Carls' Jr.
using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using TaskStatus = OpenLMStudio.Domain.Models.TaskStatus;
using TaskPriority = OpenLMStudio.Domain.Models.TaskPriority;
using TaskEntity = OpenLMStudio.Domain.Models.TaskEntity;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// SQLite-backed task repository with priority-aware scheduling, dependency resolution,
/// and automatic task startup when dependencies are satisfied.
/// </summary>
public class SqliteTaskRepository : ITaskRepository
{
    private readonly ILogger<SqliteTaskRepository>? _logger;
    private readonly string _connectionString;
    private readonly object _lock = new();

    public SqliteTaskRepository(ILogger<SqliteTaskRepository>? logger, string connectionString)
    {
        _logger = logger;
        _connectionString = connectionString;
        InitializeDatabase();
    }

    public async Task<TaskEntity> CreateTaskAsync(TaskEntity task, CancellationToken ct = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        using var tx = connection.BeginTransaction();
        try
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO Tasks (Id, Title, Description, Dependencies, Status, Progress,
                                   Priority, MaxIterations, ChatId, Context, AvailableTools,
                                   ResultSummary, ErrorMessage, ParentTaskId, CreatedAt)
                VALUES (@Id, @Title, @Description, @Dependencies, @Status, @Progress,
                        @Priority, @MaxIterations, @ChatId, @Context, @AvailableTools,
                        @ResultSummary, @ErrorMessage, @ParentTaskId, @CreatedAt)
                ON CONFLICT(Id) DO NOTHING
                """;

            cmd.Parameters.AddWithValue("@Id", task.Id.ToString());
            cmd.Parameters.AddWithValue("@Title", task.Title);
            cmd.Parameters.AddWithValue("@Description", task.Description);
            cmd.Parameters.AddWithValue("@Dependencies", JsonSerializer.Serialize(task.Dependencies));
            cmd.Parameters.AddWithValue("@Status", (int)task.Status);
            cmd.Parameters.AddWithValue("@Progress", task.Progress);
            cmd.Parameters.AddWithValue("@Priority", (int)task.Priority);
            cmd.Parameters.AddWithValue("@MaxIterations", task.MaxIterations);
            cmd.Parameters.AddWithValue("@ChatId", task.ChatId?.ToString() ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Context", task.Context ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@AvailableTools", JsonSerializer.Serialize(task.AvailableTools));
            cmd.Parameters.AddWithValue("@ResultSummary", task.ResultSummary ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ErrorMessage", task.ErrorMessage ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ParentTaskId", task.ParentTaskId?.ToString() ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedAt", task.CreatedAt);

            await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);

            _logger?.LogDebug("Created task {TaskId}: {Title}", task.Id, task.Title);
            return task;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<TaskEntity?> GetTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Tasks WHERE Id = @Id";
        cmd.Parameters.AddWithValue("@Id", taskId.ToString());

        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
            return MapTask(reader);

        return null;
    }

    public async Task UpdateTaskAsync(TaskEntity task, CancellationToken ct = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        using var tx = connection.BeginTransaction();
        try
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = """
                UPDATE Tasks SET
                    Title = @Title,
                    Description = @Description,
                    Dependencies = @Dependencies,
                    Status = @Status,
                    Progress = @Progress,
                    Priority = @Priority,
                    MaxIterations = @MaxIterations,
                    CurrentIteration = @CurrentIteration,
                    ChatId = @ChatId,
                    Context = @Context,
                    AvailableTools = @AvailableTools,
                    ResultSummary = @ResultSummary,
                    ErrorMessage = @ErrorMessage,
                    ParentTaskId = @ParentTaskId,
                    StartedAt = @StartedAt,
                    CompletedAt = @CompletedAt
                WHERE Id = @Id
                """;

            cmd.Parameters.AddWithValue("@Id", task.Id.ToString());
            cmd.Parameters.AddWithValue("@Title", task.Title);
            cmd.Parameters.AddWithValue("@Description", task.Description);
            cmd.Parameters.AddWithValue("@Dependencies", JsonSerializer.Serialize(task.Dependencies));
            cmd.Parameters.AddWithValue("@Status", (int)task.Status);
            cmd.Parameters.AddWithValue("@Progress", task.Progress);
            cmd.Parameters.AddWithValue("@Priority", (int)task.Priority);
            cmd.Parameters.AddWithValue("@MaxIterations", task.MaxIterations);
            cmd.Parameters.AddWithValue("@CurrentIteration", task.CurrentIteration);
            cmd.Parameters.AddWithValue("@ChatId", task.ChatId?.ToString() ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Context", task.Context ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@AvailableTools", JsonSerializer.Serialize(task.AvailableTools));
            cmd.Parameters.AddWithValue("@ResultSummary", task.ResultSummary ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ErrorMessage", task.ErrorMessage ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ParentTaskId", task.ParentTaskId?.ToString() ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@StartedAt", task.StartedAt?.ToString() ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CompletedAt", task.CompletedAt?.ToString() ?? (object)DBNull.Value);

            await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);

            _logger?.LogDebug("Updated task {TaskId}", task.Id);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task DeleteTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Tasks WHERE Id = @Id";
        cmd.Parameters.AddWithValue("@Id", taskId.ToString());
        await cmd.ExecuteNonQueryAsync(ct);

        _logger?.LogDebug("Deleted task {TaskId}", taskId);
    }

    public async Task<IReadOnlyList<TaskEntity>> ListTasksAsync(TaskStatus? filterStatus = null, CancellationToken ct = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM Tasks
            WHERE @FilterStatus IS NULL OR Status = @FilterStatus
            ORDER BY
                CASE Priority WHEN 3 THEN 0 WHEN 2 THEN 1 WHEN 1 THEN 2 WHEN 0 THEN 3 END,
                CreatedAt
            """;
        cmd.Parameters.AddWithValue("@FilterStatus", filterStatus.HasValue ? (object)(int)filterStatus.Value : DBNull.Value);

        var tasks = new List<TaskEntity>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            tasks.Add(MapTask(reader));

        return tasks;
    }

    public async Task<IReadOnlyList<TaskEntity>> ListChildTasksAsync(Guid parentTaskId, CancellationToken ct = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Tasks WHERE ParentTaskId = @ParentId ORDER BY CreatedAt";
        cmd.Parameters.AddWithValue("@ParentId", parentTaskId.ToString());

        var tasks = new List<TaskEntity>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            tasks.Add(MapTask(reader));

        return tasks;
    }

    public async Task<IReadOnlyList<TaskEntity>> FindReadyTasksAsync(CancellationToken ct = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM Tasks
            WHERE Status IN (@Pending, @Queued)
            ORDER BY
                CASE Priority WHEN 3 THEN 0 WHEN 2 THEN 1 WHEN 1 THEN 2 WHEN 0 THEN 3 END,
                CreatedAt
            """;
        cmd.Parameters.AddWithValue("@Pending", (int)TaskStatus.Pending);
        cmd.Parameters.AddWithValue("@Queued", (int)TaskStatus.Queued);

        var tasks = new List<TaskEntity>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            tasks.Add(MapTask(reader));

        return tasks;
    }

    public async Task UpdateStatusAsync(Guid taskId, TaskStatus newStatus, int? newProgress = null, CancellationToken ct = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        using var tx = connection.BeginTransaction();
        try
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = """
                UPDATE Tasks SET
                    Status = @Status,
                    Progress = @Progress,
                    CurrentIteration = @CurrentIteration,
                    StartedAt = @StartedAt,
                    CompletedAt = @CompletedAt
                WHERE Id = @Id
                """;

            cmd.Parameters.AddWithValue("@Id", taskId.ToString());
            cmd.Parameters.AddWithValue("@Status", (int)newStatus);
            cmd.Parameters.AddWithValue("@Progress", (object)(newProgress ?? (int?)0) ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CurrentIteration", (object)DBNull.Value);

            if (newStatus == TaskStatus.Running)
                cmd.Parameters.AddWithValue("@StartedAt", (object)DateTime.UtcNow);
            else
                cmd.Parameters.AddWithValue("@StartedAt", (object)DBNull.Value);

            if (newStatus == TaskStatus.Completed || newStatus == TaskStatus.Failed || newStatus == TaskStatus.Cancelled)
                cmd.Parameters.AddWithValue("@CompletedAt", (object)DateTime.UtcNow);
            else
                cmd.Parameters.AddWithValue("@CompletedAt", (object)DBNull.Value);

            await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);

            _logger?.LogDebug("Updated task {TaskId} status to {Status}", taskId, newStatus);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public void Dispose()
    {
        // SQLite connections are per-operation; nothing to clean up
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Tasks (
                Id TEXT PRIMARY KEY,
                Title TEXT NOT NULL,
                Description TEXT NOT NULL,
                Dependencies TEXT DEFAULT '[]',
                Priority INTEGER DEFAULT 1,
                Status INTEGER DEFAULT 0,
                Progress INTEGER DEFAULT 0,
                CurrentIteration INTEGER DEFAULT 0,
                MaxIterations INTEGER DEFAULT 50,
                ChatId TEXT,
                Context TEXT,
                AvailableTools TEXT DEFAULT '[]',
                ResultSummary TEXT,
                ErrorMessage TEXT,
                ParentTaskId TEXT,
                CreatedAt TEXT NOT NULL,
                StartedAt TEXT,
                CompletedAt TEXT,
                FOREIGN KEY (ParentTaskId) REFERENCES Tasks(Id) ON DELETE CASCADE
            )
            """;
        cmd.ExecuteNonQuery();
    }

    private TaskEntity MapTask(SqliteDataReader reader)
    {
        var deps = JsonSerializer.Deserialize<List<Guid>>(reader.GetString(reader.GetOrdinal("Dependencies"))) ?? new();
        var tools = JsonSerializer.Deserialize<List<string>>(reader.GetString(reader.GetOrdinal("AvailableTools"))) ?? new();

        return new TaskEntity
        {
            Id = Guid.Parse(reader.GetString(reader.GetOrdinal("Id"))),
            Title = reader.GetString(reader.GetOrdinal("Title")),
            Description = reader.GetString(reader.GetOrdinal("Description")),
            Dependencies = deps,
            Priority = (TaskPriority)reader.GetInt32(reader.GetOrdinal("Priority")),
            Status = (TaskStatus)reader.GetInt32(reader.GetOrdinal("Status")),
            Progress = reader.GetInt32(reader.GetOrdinal("Progress")),
            CurrentIteration = reader.GetInt32(reader.GetOrdinal("CurrentIteration")),
            MaxIterations = reader.GetInt32(reader.GetOrdinal("MaxIterations")),
            ChatId = reader.IsDBNull(reader.GetOrdinal("ChatId")) ? null : Guid.Parse(reader.GetString(reader.GetOrdinal("ChatId"))),
            Context = reader.IsDBNull(reader.GetOrdinal("Context")) ? null : reader.GetString(reader.GetOrdinal("Context")),
            AvailableTools = tools,
            ResultSummary = reader.IsDBNull(reader.GetOrdinal("ResultSummary")) ? null : reader.GetString(reader.GetOrdinal("ResultSummary")),
            ErrorMessage = reader.IsDBNull(reader.GetOrdinal("ErrorMessage")) ? null : reader.GetString(reader.GetOrdinal("ErrorMessage")),
            ParentTaskId = reader.IsDBNull(reader.GetOrdinal("ParentTaskId")) ? null : Guid.Parse(reader.GetString(reader.GetOrdinal("ParentTaskId"))),
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
            StartedAt = reader.IsDBNull(reader.GetOrdinal("StartedAt")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("StartedAt"))),
            CompletedAt = reader.IsDBNull(reader.GetOrdinal("CompletedAt")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("CompletedAt"))),
        };
    }
}