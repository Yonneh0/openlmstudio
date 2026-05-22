using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using AgenticTask = OpenLMStudio.Domain.Models.AgenticTask;
using TaskPriority = OpenLMStudio.Domain.Models.TaskPriority;
using TaskStatus = OpenLMStudio.Domain.Models.TaskStatus;
using TaskPhase = OpenLMStudio.Domain.Models.TaskPhase;
using AgentToolCallRecord = OpenLMStudio.Domain.Models.AgentToolCallRecord;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// SQLite-backed task scheduler with priority-aware scheduling, dependency resolution,
/// batch task injection, and auto-start on dependency satisfaction.
/// </summary>
public class TaskSchedulerService : ITaskScheduler, IDisposable
{
    private readonly ILogger<TaskSchedulerService> _logger;
    private readonly string _connectionString;
    private readonly object _lock = new();

    public TaskSchedulerService(ILogger<TaskSchedulerService> logger, string connectionString)
    {
        _logger = logger;
        _connectionString = connectionString;
        InitializeDatabase();
    }

    public async Task InjectTasksAsync(Guid branchId, IEnumerable<AgenticTask> tasks, CancellationToken ct = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        using var tx = connection.BeginTransaction();
        try
        {
            foreach (var task in tasks)
            {
                var cmd = connection.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO Tasks (Id, Description, Dependencies, Priority, Status, MaxIterations, BranchId, Phase,
                                       Instructions, ValidationCriteria, OutputFields, CreatedAt)
                    VALUES (@Id, @Description, @Dependencies, @Priority, @Status, @MaxIterations, @BranchId, @Phase,
                            @Instructions, @ValidationCriteria, @OutputFields, @CreatedAt)
                    ON CONFLICT(Id) DO NOTHING
                    """;
                cmd.Parameters.AddWithValue("@Id", task.Id.ToString());
                cmd.Parameters.AddWithValue("@Description", task.Description);
                cmd.Parameters.AddWithValue("@Dependencies", SerializeDependencies(task.Dependencies));
                cmd.Parameters.AddWithValue("@Priority", (int)(TaskPriority)task.Priority);
                cmd.Parameters.AddWithValue("@Status", (int)(TaskStatus)task.Status);
                cmd.Parameters.AddWithValue("@MaxIterations", task.MaxIterations);
                cmd.Parameters.AddWithValue("@BranchId", branchId.ToString());
                cmd.Parameters.AddWithValue("@Phase", (int)(TaskPhase)task.Phase);
                cmd.Parameters.AddWithValue("@Instructions", task.Instructions ?? "");
                cmd.Parameters.AddWithValue("@ValidationCriteria", task.ValidationCriteria ?? "");
                cmd.Parameters.AddWithValue("@OutputFields", task.OutputFields ?? "");
                cmd.Parameters.AddWithValue("@CreatedAt", task.CreatedAt);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
            _logger.LogDebug("Injected {Count} tasks into branch {BranchId}", tasks.Count(), branchId);

            // Auto-start tasks whose dependencies are satisfied
            foreach (var task in tasks)
            {
                await CheckAndStartDependentTasksAsync(task.Id, ct);
            }
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<List<AgenticTask>> GetScheduledTasksAsync(Guid branchId, CancellationToken ct = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Description, Dependencies, Priority, Status, Progress, MaxIterations, Summary,
                   ErrorMessage, ContextSnapshotId, ParentTaskId, BranchId, Phase, Instructions,
                   ValidationCriteria, OutputFields, CreatedAt, StartedAt, CompletedAt, AutoCompleted
            FROM Tasks WHERE BranchId = @BranchId
            ORDER BY
                CASE Priority WHEN 4 THEN 0 WHEN 3 THEN 1 WHEN 2 THEN 2 WHEN 1 THEN 3 WHEN 0 THEN 4 END,
                CreatedAt
            """;
        cmd.Parameters.AddWithValue("@BranchId", branchId.ToString());

        var tasks = new List<AgenticTask>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            tasks.Add(MapTask(reader));
        }
        return tasks;
    }

    public async Task<List<AgenticTask>> GetAllScheduledTasksAsync(CancellationToken ct = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Description, Dependencies, Priority, Status, Progress, MaxIterations, Summary,
                   ErrorMessage, ContextSnapshotId, ParentTaskId, BranchId, Phase, Instructions,
                   ValidationCriteria, OutputFields, CreatedAt, StartedAt, CompletedAt, AutoCompleted
            FROM Tasks
            ORDER BY
                CASE Priority WHEN 4 THEN 0 WHEN 3 THEN 1 WHEN 2 THEN 2 WHEN 1 THEN 3 WHEN 0 THEN 4 END,
                CreatedAt
            """;

        var tasks = new List<AgenticTask>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            tasks.Add(MapTask(reader));
        }
        return tasks;
    }

    public async Task UpdateTaskStatusAsync(Guid taskId, TaskStatus newStatus, string? errorMessage = null, CancellationToken ct = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        using var tx = connection.BeginTransaction();
        try
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = """
                UPDATE Tasks SET Status = @Status, ErrorMessage = @ErrorMessage
                WHERE Id = @Id
                """;
            cmd.Parameters.AddWithValue("@Id", taskId.ToString());
            cmd.Parameters.AddWithValue("@Status", (int)newStatus);
            cmd.Parameters.AddWithValue("@ErrorMessage", errorMessage ?? "");
            if (newStatus == TaskStatus.Running)
            {
                cmd.CommandText += ", StartedAt = @StartedAt";
                cmd.Parameters.AddWithValue("@StartedAt", DateTime.UtcNow);
            }
            if (newStatus == TaskStatus.Completed)
            {
                cmd.CommandText += ", CompletedAt = @CompletedAt";
                cmd.Parameters.AddWithValue("@CompletedAt", DateTime.UtcNow);
            }
            await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            _logger.LogDebug("Updated task {TaskId} status to {Status}", taskId, newStatus);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task UpdateTaskProgressAsync(Guid taskId, int progress, CancellationToken ct = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Tasks SET Progress = @Progress WHERE Id = @Id";
        cmd.Parameters.AddWithValue("@Progress", progress);
        cmd.Parameters.AddWithValue("@Id", taskId.ToString());
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task RegisterToolCallAsync(Guid taskId, AgentToolCallRecord record, CancellationToken ct = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        using var tx = connection.BeginTransaction();
        try
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO ToolCalls (Id, TaskId, ToolName, Parameters, Result, Success, DurationMs, Timestamp)
                VALUES (@Id, @TaskId, @ToolName, @Parameters, @Result, @Success, @DurationMs, @Timestamp)
                """;
            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("@TaskId", taskId.ToString());
            cmd.Parameters.AddWithValue("@ToolName", record.ToolName);
            cmd.Parameters.AddWithValue("@Parameters", JsonSerializer.Serialize(record.Parameters));
            cmd.Parameters.AddWithValue("@Result", record.Result);
            cmd.Parameters.AddWithValue("@Success", record.Success);
            cmd.Parameters.AddWithValue("@DurationMs", record.DurationMs);
            cmd.Parameters.AddWithValue("@Timestamp", record.Timestamp);
            await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<List<AgenticTask>> GetReadyTasksAsync(CancellationToken ct = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Description, Dependencies, Priority, Status, Progress, MaxIterations, Summary,
                   ErrorMessage, ContextSnapshotId, ParentTaskId, BranchId, Phase, Instructions,
                   ValidationCriteria, OutputFields, CreatedAt, StartedAt, CompletedAt, AutoCompleted
            FROM Tasks
            WHERE Status = @Pending
            ORDER BY
                CASE Priority WHEN 4 THEN 0 WHEN 3 THEN 1 WHEN 2 THEN 2 WHEN 1 THEN 3 WHEN 0 THEN 4 END,
                CreatedAt
            """;
        cmd.Parameters.AddWithValue("@Pending", (int)TaskStatus.Pending);

        var tasks = new List<AgenticTask>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            tasks.Add(MapTask(reader));
        }
        return tasks;
    }

    public async Task CheckAndStartDependentTasksAsync(Guid completedTaskId, CancellationToken ct = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Dependencies FROM Tasks WHERE Status = 0
            """;

        using var reader = await cmd.ExecuteReaderAsync(ct);
        var pendingTaskIds = new List<string>();
        while (await reader.ReadAsync(ct))
        {
            pendingTaskIds.Add(reader.GetString(0));
        }

        foreach (var taskId in pendingTaskIds)
        {
            var deps = await GetTaskDependenciesAsync(connection, taskId, ct);
            if (deps == null || !deps.Contains(completedTaskId))
                continue;

            // Check if all dependencies are completed
            var allDepsCompleted = true;
            foreach (var depId in deps)
            {
                var statusCmd = connection.CreateCommand();
                statusCmd.CommandText = "SELECT Status FROM Tasks WHERE Id = @Id";
                statusCmd.Parameters.AddWithValue("@Id", depId);
                var statusObj = await statusCmd.ExecuteScalarAsync(ct);
                var status = statusObj != null ? (int)statusObj : -1;
                if (status != (int)(TaskStatus)TaskStatus.Completed)
                {
                    allDepsCompleted = false;
                    break;
                }
            }

            if (allDepsCompleted)
            {
                // Start this task
                var startCmd = connection.CreateCommand();
                startCmd.CommandText = "UPDATE Tasks SET Status = @Running, StartedAt = @StartedAt WHERE Id = @Id";
                startCmd.Parameters.AddWithValue("@Running", (int)(TaskStatus)TaskStatus.Running);
                startCmd.Parameters.AddWithValue("@StartedAt", DateTime.UtcNow);
                startCmd.Parameters.AddWithValue("@Id", taskId);
                await startCmd.ExecuteNonQueryAsync(ct);
                _logger.LogDebug("Auto-started task {TaskId} after dependency {CompletedTaskId} completed", taskId, completedTaskId);
            }
        }
    }

    public async Task AbandonBranchAsync(Guid branchId, CancellationToken ct = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Tasks SET Status = @Cancelled WHERE BranchId = @BranchId AND Status IN (@Pending, @Running)";
        cmd.Parameters.AddWithValue("@Cancelled", (int)(TaskStatus)TaskStatus.Cancelled);
        cmd.Parameters.AddWithValue("@BranchId", branchId.ToString());
        await cmd.ExecuteNonQueryAsync(ct);
        _logger.LogDebug("Abandoned branch {BranchId}", branchId);
    }

    public async Task PauseBranchAsync(Guid branchId, CancellationToken ct = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Tasks SET Status = @Paused WHERE BranchId = @BranchId AND Status = @Running";
        cmd.Parameters.AddWithValue("@Paused", (int)(TaskStatus)TaskStatus.Paused);
        cmd.Parameters.AddWithValue("@BranchId", branchId.ToString());
        await cmd.ExecuteNonQueryAsync(ct);
        _logger.LogDebug("Paused branch {BranchId}", branchId);
    }

    public async Task ResumeBranchAsync(Guid branchId, CancellationToken ct = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Tasks SET Status = @Pending WHERE BranchId = @BranchId AND Status = @Paused";
        cmd.Parameters.AddWithValue("@Pending", (int)(TaskStatus)TaskStatus.Pending);
        cmd.Parameters.AddWithValue("@BranchId", branchId.ToString());
        await cmd.ExecuteNonQueryAsync(ct);
        _logger.LogDebug("Resumed branch {BranchId}", branchId);
    }

    public void Dispose()
    {
        // SQLite connections are disposed per-operation; nothing to clean up
    }

    /// <summary>
    /// Registers tasks from a branch for in-memory caching by a scheduler wrapper.
    /// </summary>
    public void RegisterBranch(Guid branchId, List<AgenticTask> tasks)
    {
        // No-op in the persistence service; the TaskScheduler wrapper handles caching.
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var createTasks = connection.CreateCommand();
        createTasks.CommandText = """
            CREATE TABLE IF NOT EXISTS Tasks (
                Id TEXT PRIMARY KEY,
                Description TEXT NOT NULL,
                Dependencies TEXT DEFAULT '[]',
                Priority INTEGER DEFAULT 1,
                Status INTEGER DEFAULT 0,
                Progress INTEGER DEFAULT 0,
                MaxIterations INTEGER DEFAULT 50,
                Summary TEXT DEFAULT '',
                ErrorMessage TEXT DEFAULT '',
                ContextSnapshotId TEXT,
                ParentTaskId TEXT,
                BranchId TEXT NOT NULL,
                Phase INTEGER DEFAULT 1,
                Instructions TEXT DEFAULT '',
                ValidationCriteria TEXT DEFAULT '',
                OutputFields TEXT DEFAULT '',
                CreatedAt TEXT NOT NULL,
                StartedAt TEXT,
                CompletedAt TEXT,
                AutoCompleted INTEGER DEFAULT 0
            )
            """;
        createTasks.ExecuteNonQuery();

        var createToolCalls = connection.CreateCommand();
        createToolCalls.CommandText = """
            CREATE TABLE IF NOT EXISTS ToolCalls (
                Id TEXT PRIMARY KEY,
                TaskId TEXT NOT NULL,
                ToolName TEXT NOT NULL,
                Parameters TEXT NOT NULL,
                Result TEXT NOT NULL,
                Success INTEGER NOT NULL,
                DurationMs REAL NOT NULL,
                Timestamp TEXT NOT NULL,
                FOREIGN KEY (TaskId) REFERENCES Tasks(Id) ON DELETE CASCADE
            )
            """;
        createToolCalls.ExecuteNonQuery();

        var createBranches = connection.CreateCommand();
        createBranches.CommandText = """
            CREATE TABLE IF NOT EXISTS Branches (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Description TEXT DEFAULT '',
                ParentBranchId TEXT,
                Status INTEGER DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (ParentBranchId) REFERENCES Branches(Id) ON DELETE CASCADE
            )
            """;
        createBranches.ExecuteNonQuery();
    }

    private static AgenticTask MapTask(SqliteDataReader reader)
    {
        var deps = DeserializeDependencies(reader.GetString(reader.GetOrdinal("Dependencies")));
        return new AgenticTask
        {
            Id = Guid.Parse(reader.GetString(reader.GetOrdinal("Id"))),
            Description = reader.GetString(reader.GetOrdinal("Description")),
            Dependencies = deps,
            Priority = (TaskPriority)(int)reader.GetInt32(reader.GetOrdinal("Priority")),
            Status = (TaskStatus)(int)reader.GetInt32(reader.GetOrdinal("Status")),
            Progress = reader.GetInt32(reader.GetOrdinal("Progress")),
            MaxIterations = reader.GetInt32(reader.GetOrdinal("MaxIterations")),
            Summary = reader.IsDBNull(reader.GetOrdinal("Summary")) ? null : reader.GetString(reader.GetOrdinal("Summary")),
            ErrorMessage = reader.IsDBNull(reader.GetOrdinal("ErrorMessage")) ? null : reader.GetString(reader.GetOrdinal("ErrorMessage")),
            ContextSnapshotId = reader.IsDBNull(reader.GetOrdinal("ContextSnapshotId")) ? null : Guid.Parse(reader.GetString(reader.GetOrdinal("ContextSnapshotId"))),
            ParentTaskId = reader.IsDBNull(reader.GetOrdinal("ParentTaskId")) ? null : Guid.Parse(reader.GetString(reader.GetOrdinal("ParentTaskId"))),
            BranchId = Guid.Parse(reader.GetString(reader.GetOrdinal("BranchId"))),
            Phase = (TaskPhase)reader.GetInt32(reader.GetOrdinal("Phase")),
            Instructions = reader.GetString(reader.GetOrdinal("Instructions")),
            ValidationCriteria = reader.IsDBNull(reader.GetOrdinal("ValidationCriteria")) ? null : reader.GetString(reader.GetOrdinal("ValidationCriteria")),
            OutputFields = reader.IsDBNull(reader.GetOrdinal("OutputFields")) ? null : reader.GetString(reader.GetOrdinal("OutputFields")),
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
            StartedAt = reader.IsDBNull(reader.GetOrdinal("StartedAt")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("StartedAt"))),
            CompletedAt = reader.IsDBNull(reader.GetOrdinal("CompletedAt")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("CompletedAt"))),
            AutoCompleted = reader.GetInt32(reader.GetOrdinal("AutoCompleted")) == 1,
        };
    }

    private static List<Guid> DeserializeDependencies(string deps)
    {
        try
        {
            return string.IsNullOrEmpty(deps) || deps == "[]"
                ? new List<Guid>()
                : JsonSerializer.Deserialize<List<Guid>>(deps) ?? new List<Guid>();
        }
        catch
        {
            return new List<Guid>();
        }
    }

    private static string SerializeDependencies(List<Guid> deps)
    {
        return JsonSerializer.Serialize(deps);
    }

    private async Task<List<Guid>?> GetTaskDependenciesAsync(SqliteConnection connection, string taskId, CancellationToken ct)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Dependencies FROM Tasks WHERE Id = @Id";
        cmd.Parameters.AddWithValue("@Id", taskId);
        var raw = (string?)await cmd.ExecuteScalarAsync(ct);
        return DeserializeDependencies(raw ?? "[]");
    }
}