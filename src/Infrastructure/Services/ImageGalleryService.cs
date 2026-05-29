using System.Text.Json;
using Microsoft.Data.Sqlite;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Types;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// SQLite-backed image gallery service.
/// </summary>
public class ImageGalleryService : IImageGalleryService
{
    private readonly string _dbPath;
    private readonly ILogger<ImageGalleryService>? _logger;
    private readonly object _lock = new();

    public ImageGalleryService(string? dbPath = null, ILogger<ImageGalleryService>? logger = null)
    {
        _logger = logger;
        var galleryDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures", "OpenLMStudio", "Gallery");
        Directory.CreateDirectory(galleryDir);
        _dbPath = dbPath ?? Path.Combine(galleryDir, "gallery.db");
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath};Journal Mode=WAL");
        conn.Open();
        Execute(conn, @"
            CREATE TABLE IF NOT EXISTS Images (
                Id TEXT PRIMARY KEY,
                Prompt TEXT,
                ModelId TEXT,
                Width INTEGER,
                Height INTEGER,
                Seed INTEGER,
                CfgScale REAL,
                Steps INTEGER,
                Sampler TEXT,
                FilePath TEXT,
                ThumbnailPath TEXT,
                Timestamp DATETIME
            );
            CREATE INDEX IF NOT EXISTS IX_Images_Timestamp ON Images(Timestamp DESC);
            CREATE INDEX IF NOT EXISTS IX_Images_Prompt ON Images(Prompt);
        ");
    }

    private async Task WithConnectionAsync(Func<SqliteConnection, Task> action)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath};Journal Mode=WAL");
        await conn.OpenAsync();
        await action(conn);
    }

    public async Task<IReadOnlyList<ImageGalleryEntry>> GetRecentImagesAsync(int count = 50, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath};Journal Mode=WAL");
        await conn.OpenAsync(ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Images ORDER BY Timestamp DESC LIMIT @count";
        cmd.Parameters.AddWithValue("@count", count);
        var results = new List<ImageGalleryEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(MapRow(reader));
        return results;
    }

    public async Task<IReadOnlyList<ImageGalleryEntry>> SearchImagesAsync(string query, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath};Journal Mode=WAL");
        await conn.OpenAsync(ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Images WHERE Prompt LIKE @q ORDER BY Timestamp DESC";
        cmd.Parameters.AddWithValue("@q", $"%{query}%");
        var results = new List<ImageGalleryEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(MapRow(reader));
        return results;
    }

    public async Task<ImageGalleryEntry?> GetImageAsync(string id, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath};Journal Mode=WAL");
        await conn.OpenAsync(ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Images WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
            return MapRow(reader);
        return null;
    }

    public async Task DeleteImageAsync(string id, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath};Journal Mode=WAL");
        await conn.OpenAsync(ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Images WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<string> ExportGalleryAsJsonAsync(CancellationToken ct = default)
    {
        var entries = await GetRecentImagesAsync(count: 10000, ct);
        return JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task ImportGalleryFromJsonAsync(string json, CancellationToken ct = default)
    {
        var entries = JsonSerializer.Deserialize<List<ImageGalleryEntry>>(json);
        if (entries == null) return;
        await WithConnectionAsync(async conn =>
        {
            foreach (var entry in entries)
            {
                Execute(conn, @"
            INSERT OR REPLACE INTO Images (Id, Prompt, ModelId, Width, Height, Seed, CfgScale, Steps, Sampler, FilePath, ThumbnailPath, Timestamp)
            VALUES (@Id, @Prompt, @ModelId, @Width, @Height, @Seed, @CfgScale, @Steps, @Sampler, @FilePath, @ThumbnailPath, @Timestamp)",
                    new { entry.Id, entry.Prompt, entry.ModelId, entry.Width, entry.Height, entry.Seed, entry.CfgScale, entry.Steps, entry.Sampler, entry.FilePath, entry.ThumbnailPath, entry.Timestamp });
            }
        });
    }

    public void Dispose() { }

    private static ImageGalleryEntry MapRow(SqliteDataReader reader)
    {
        return new ImageGalleryEntry(
            Id: reader["Id"] as string ?? "",
            Prompt: reader["Prompt"] as string ?? "",
            ModelId: reader["ModelId"] as string ?? "",
            Width: Convert.ToInt32(reader["Width"]),
            Height: Convert.ToInt32(reader["Height"]),
            Seed: Convert.ToInt64(reader["Seed"]),
            CfgScale: Convert.ToDouble(reader["CfgScale"]),
            Steps: Convert.ToInt32(reader["Steps"]),
            Sampler: reader["Sampler"] as string ?? "",
            FilePath: reader["FilePath"] as string ?? "",
            ThumbnailPath: reader["ThumbnailPath"] as string ?? "",
            Timestamp: reader["Timestamp"] is DateTime dt ? dt : DateTime.Now);
    }

    private int Execute(SqliteConnection conn, string sql, object? parameters = null)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        if (parameters != null)
            foreach (var prop in parameters.GetType().GetProperties())
                cmd.Parameters.AddWithValue($"@{prop.Name}", prop.GetValue(parameters) ?? (object)DBNull.Value);
        return cmd.ExecuteNonQuery();
    }
}
