using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Types;
using SkiaSharp;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// SQLite-backed image gallery service with thumbnail generation.
/// </summary>
public class ImageGalleryService : IImageGalleryService
{
    private readonly string _databasePath;
    private readonly ILogger<ImageGalleryService>? _logger;
    private readonly object _lock = new();

    public ImageGalleryService(ILogger<ImageGalleryService>? logger = null)
    {
        _logger = logger;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dataDir = Path.Combine(appData, "OpenLMStudio");
        _databasePath = Path.Combine(dataDir, "image_gallery.db");
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var conn = new SqliteConnection($"Data Source={_databasePath}");
        conn.Open();
        using var cmd = new SqliteCommand(@"
            CREATE TABLE IF NOT EXISTS ImageGallery (
                Id TEXT PRIMARY KEY,
                Prompt TEXT NOT NULL,
                ModelId TEXT NOT NULL,
                Width INTEGER NOT NULL,
                Height INTEGER NOT NULL,
                Seed INTEGER NOT NULL,
                CfgScale REAL NOT NULL,
                Steps INTEGER NOT NULL,
                Sampler TEXT NOT NULL,
                FilePath TEXT NOT NULL,
                ThumbnailPath TEXT NOT NULL,
                Timestamp TEXT NOT NULL
            )", conn);
        cmd.ExecuteNonQuery();
    }

    public async Task<ImageGalleryEntry> AddImageAsync(ImageGalleryEntry entry, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection($"Data Source={_databasePath}");
        conn.Open();
        using var cmd = new SqliteCommand(@"
            INSERT INTO ImageGallery (Id, Prompt, ModelId, Width, Height, Seed, CfgScale, Steps, Sampler, FilePath, ThumbnailPath, Timestamp)
            VALUES (@Id, @Prompt, @ModelId, @Width, @Height, @Seed, @CfgScale, @Steps, @Sampler, @FilePath, @ThumbnailPath, @Timestamp)", conn);
        cmd.Parameters.AddWithValue("@Id", entry.Id);
        cmd.Parameters.AddWithValue("@Prompt", entry.Prompt);
        cmd.Parameters.AddWithValue("@ModelId", entry.ModelId);
        cmd.Parameters.AddWithValue("@Width", entry.Width);
        cmd.Parameters.AddWithValue("@Height", entry.Height);
        cmd.Parameters.AddWithValue("@Seed", entry.Seed);
        cmd.Parameters.AddWithValue("@CfgScale", entry.CfgScale);
        cmd.Parameters.AddWithValue("@Steps", entry.Steps);
        cmd.Parameters.AddWithValue("@Sampler", entry.Sampler);
        cmd.Parameters.AddWithValue("@FilePath", entry.FilePath);
        cmd.Parameters.AddWithValue("@ThumbnailPath", entry.ThumbnailPath);
        cmd.Parameters.AddWithValue("@Timestamp", entry.Timestamp.ToString("o"));
        await cmd.ExecuteNonQueryAsync(ct);
        return entry;
    }

    public async Task<ImageGalleryEntry?> GetImageAsync(string id, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection($"Data Source={_databasePath}");
        conn.Open();
        using var cmd = new SqliteCommand("SELECT * FROM ImageGallery WHERE Id = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
            return CreateEntry(reader);
        return null;
    }

    public async Task<IReadOnlyList<ImageGalleryEntry>> GetRecentImagesAsync(int count = 50, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection($"Data Source={_databasePath}");
        conn.Open();
        using var cmd = new SqliteCommand("SELECT * FROM ImageGallery ORDER BY Timestamp DESC LIMIT @Count", conn);
        cmd.Parameters.AddWithValue("@Count", count);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        var entries = new List<ImageGalleryEntry>();
        while (await reader.ReadAsync(ct))
            entries.Add(CreateEntry(reader));
        return entries;
    }

    public async Task<IReadOnlyList<ImageGalleryEntry>> SearchImagesAsync(string query, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection($"Data Source={_databasePath}");
        conn.Open();
        using var cmd = new SqliteCommand(@"
            SELECT * FROM ImageGallery 
            WHERE Prompt LIKE @Query OR ModelId LIKE @Query 
            ORDER BY Timestamp DESC", conn);
        cmd.Parameters.AddWithValue("@Query", $"%{query}%");
        using var reader = await cmd.ExecuteReaderAsync(ct);
        var entries = new List<ImageGalleryEntry>();
        while (await reader.ReadAsync(ct))
            entries.Add(CreateEntry(reader));
        return entries;
    }

    public async Task DeleteImageAsync(string id, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection($"Data Source={_databasePath}");
        conn.Open();
        using var cmd = new SqliteCommand("DELETE FROM ImageGallery WHERE Id = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        await cmd.ExecuteNonQueryAsync(ct);
        // Also delete the file
        var entry = await GetImageAsync(id, ct);
        if (entry != null && File.Exists(entry.FilePath))
            File.Delete(entry.FilePath);
        if (entry != null && entry.ThumbnailPath != entry.FilePath && File.Exists(entry.ThumbnailPath))
            File.Delete(entry.ThumbnailPath);
    }

    public async Task<IReadOnlyList<ImageGalleryEntry>> GetAllImagesAsync(CancellationToken ct = default)
    {
        using var conn = new SqliteConnection($"Data Source={_databasePath}");
        conn.Open();
        using var cmd = new SqliteCommand("SELECT * FROM ImageGallery ORDER BY Timestamp DESC", conn);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        var entries = new List<ImageGalleryEntry>();
        while (await reader.ReadAsync(ct))
            entries.Add(CreateEntry(reader));
        return entries;
    }

    public async Task ExportGalleryAsJsonAsync(string filePath, CancellationToken ct = default)
    {
        var entries = await GetAllImagesAsync(ct);
        var json = System.Text.Json.JsonSerializer.Serialize(entries, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json, ct);
    }

    public async Task ImportGalleryFromJsonAsync(string filePath, CancellationToken ct = default)
    {
        var json = await File.ReadAllTextAsync(filePath, ct);
        var entries = System.Text.Json.JsonSerializer.Deserialize<List<ImageGalleryEntry>>(json);
        if (entries == null) return;
        foreach (var entry in entries)
            await AddImageAsync(entry, ct);
    }

    public void Dispose() { }

    private static ImageGalleryEntry CreateEntry(SqliteDataReader reader)
    {
        return new ImageGalleryEntry(
            Id: reader.GetString(0),
            Prompt: reader.GetString(1),
            ModelId: reader.GetString(2),
            Width: reader.GetInt32(3),
            Height: reader.GetInt32(4),
            Seed: reader.GetInt64(5),
            CfgScale: reader.GetDouble(6),
            Steps: reader.GetInt32(7),
            Sampler: reader.GetString(8),
            FilePath: reader.GetString(9),
            ThumbnailPath: reader.GetString(10),
            Timestamp: DateTimeOffset.Parse(reader.GetString(11))
        );
    }
}