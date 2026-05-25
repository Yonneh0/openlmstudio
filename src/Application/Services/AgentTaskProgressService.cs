namespace OpenLMStudio.Application.Services;

using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

/// <summary>
/// Manages agent task progress tracking with file-based storage.
/// </summary>
public class AgentTaskProgressService : IAgentTaskProgressService
{
    private readonly IFileSystem _fileSystem;
    private readonly string _progressDirectory;
    private readonly ConcurrentDictionary<Guid, AgentTaskProgress> _inMemoryProgress;
    private readonly ILogger<AgentTaskProgressService> _logger;
    private readonly object _lock = new();

    public AgentTaskProgressService(
        IFileSystem? fileSystem = null,
        string? progressDirectory = null,
        ILogger<AgentTaskProgressService>? logger = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
        _progressDirectory = progressDirectory ?? Path.Combine(Path.GetTempPath(), "OpenLMStudio", "progress");
        _inMemoryProgress = new ConcurrentDictionary<Guid, AgentTaskProgress>();
        _logger = logger ?? NullLogger<AgentTaskProgressService>.Instance;
    }

    public void Dispose()
    {
        _inMemoryProgress.Clear();
    }

    public async Task<AgentTaskProgress?> GetProgressAsync(Guid taskId, CancellationToken ct = default)
    {
        try
        {
            if (_inMemoryProgress.TryGetValue(taskId, out var progress))
                return progress;

            var progressPath = GetProgressFilePath(taskId);
            if (!_fileSystem.File.Exists(progressPath))
                return new AgentTaskProgress();

            var json = await _fileSystem.File.ReadAllTextAsync(progressPath, ct);
            var loadedProgress = JsonSerializer.Deserialize<AgentTaskProgress>(json);

            if (loadedProgress != null)
            {
                _inMemoryProgress[taskId] = loadedProgress;
            }

            return loadedProgress;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting progress for task {TaskId}", taskId);
            return new AgentTaskProgress();
        }
    }

    public async Task UpdateProgressAsync(Guid taskId, int percentage, CancellationToken ct = default)
    {
        var progress = await GetProgressAsync(taskId, ct);
        if (progress != null)
        {
            progress.Percentage = Math.Max(0, Math.Min(100, percentage));
            await SaveProgressAsync(taskId, progress, ct);
        }
    }

    public async Task AddChecklistItemAsync(Guid taskId, string description, bool isCompleted = false, CancellationToken ct = default)
    {
        var progress = await GetProgressAsync(taskId, ct);
        if (progress != null)
        {
            progress.AddChecklistItem(description, isCompleted);
            await SaveProgressAsync(taskId, progress, ct);
        }
    }

    public async Task CompleteChecklistItemAsync(Guid taskId, string description, CancellationToken ct = default)
    {
        var progress = await GetProgressAsync(taskId, ct);
        if (progress != null)
        {
            progress.CompleteChecklistItem(description);
            await SaveProgressAsync(taskId, progress, ct);
        }
    }

    public async Task UpdateCurrentStepAsync(Guid taskId, string step, CancellationToken ct = default)
    {
        var progress = await GetProgressAsync(taskId, ct);
        if (progress != null)
        {
            progress.UpdateCurrentStep(step);
            await SaveProgressAsync(taskId, progress, ct);
        }
    }

    public async Task AddReminderAsync(Guid taskId, string reminder, CancellationToken ct = default)
    {
        var progress = await GetProgressAsync(taskId, ct);
        if (progress != null)
        {
            progress.AddReminder(reminder);
            await SaveProgressAsync(taskId, progress, ct);
        }
    }

    public async Task<int> GetChecklistPercentageAsync(Guid taskId, CancellationToken ct = default)
    {
        var progress = await GetProgressAsync(taskId, ct);
        return progress?.CalculateChecklistPercentage() ?? 0;
    }

    public async Task<bool> ShouldSendReminderAsync(Guid taskId, CancellationToken ct = default)
    {
        var progress = await GetProgressAsync(taskId, ct);
        if (progress == null || progress.Reminders.Count == 0)
            return false;

        var elapsed = DateTime.UtcNow - progress.LastReminderTime;
        return elapsed.TotalMinutes >= progress.ReminderIntervalMinutes;
    }

    private async Task SaveProgressAsync(Guid taskId, AgentTaskProgress progress, CancellationToken ct)
    {
        try
        {
            var progressPath = GetProgressFilePath(taskId);
            var dir = _fileSystem.Path.GetDirectoryName(progressPath);
            if (!string.IsNullOrEmpty(dir))
                _fileSystem.Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(progress, new JsonSerializerOptions { WriteIndented = true });
            await _fileSystem.File.WriteAllTextAsync(progressPath, json, ct);

            lock (_lock)
            {
                _inMemoryProgress[taskId] = progress;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving progress for task {TaskId}", taskId);
            throw;
        }
    }

    private string GetProgressFilePath(Guid taskId)
    {
        return Path.Combine(_progressDirectory, $"{taskId:N}.json");
    }
}