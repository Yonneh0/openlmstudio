using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Concrete implementation of IFileOperationsService for file read/write operations within the agent sandbox.
/// </summary>
public class FileOperationsService : IFileOperationsService, IDisposable
{
    private readonly ILogger<FileOperationsService>? _logger;
    private bool _disposed;

    /// <summary>
    /// Creates a new FileOperationsService instance.
    /// </summary>
    public FileOperationsService(ILogger<FileOperationsService>? logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<FileReadResult> ReadFileAsync(FileReadRequest request, CancellationToken ct = default)
    {
        if (request == null || string.IsNullOrEmpty(request.FilePath))
            throw new ArgumentException("FilePath is required.", nameof(request));

        try
        {
            var fileInfo = new FileInfo(request.FilePath);
            long totalSizeBytes = fileInfo.Exists ? fileInfo.Length : 0;

            if (!File.Exists(request.FilePath))
                return new FileReadResult(string.Empty, totalSizeBytes, false);

            // Read file with optional line limit
            string content;
            bool truncated = false;

            if (request.MaxLines != null && request.MaxLines > 0)
            {
                using var reader = new StreamReader(request.FilePath);
                var lines = new List<string>();

                while (!reader.EndOfStream && lines.Count < request.MaxLines.Value)
                {
                    string? line = await reader.ReadLineAsync();
                    if (line != null)
                        lines.Add(line);
                }

                content = string.Join("\n", lines);
                truncated = !reader.EndOfStream;
            }
            else
            {
                content = await File.ReadAllTextAsync(request.FilePath, ct);
            }

            return new FileReadResult(content, totalSizeBytes, truncated);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to read file: {FilePath}", request.FilePath);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<FileWriteResult> WriteFileAsync(FileWriteRequest request, CancellationToken ct = default)
    {
        if (request == null || string.IsNullOrEmpty(request.FilePath))
            throw new ArgumentException("FilePath is required.", nameof(request));

        try
        {
            bool overwritesExisting = File.Exists(request.FilePath);

            // Ensure parent directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(request.FilePath)!);

            if (request.Append)
            {
                await File.AppendAllTextAsync(request.FilePath, request.Content, ct);
                return new FileWriteResult(
                    Convert.ToInt64(System.Text.Encoding.UTF8.GetByteCount(request.Content)),
                    overwritesExisting);
            }
            else
            {
                await File.WriteAllTextAsync(request.FilePath, request.Content, ct);
                return new FileWriteResult(
                    System.Text.Encoding.UTF8.GetByteCount(request.Content),
                    overwritesExisting);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to write file: {FilePath}", request.FilePath);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<FilePatchResult> PatchFileAsync(FilePatchRequest request, CancellationToken ct = default)
    {
        if (request == null || string.IsNullOrEmpty(request.FilePath))
            throw new ArgumentException("FilePath is required.", nameof(request));

        try
        {
            if (!File.Exists(request.FilePath))
                return new FilePatchResult(0, 0, false);

            var lines = new List<string>(await File.ReadAllLinesAsync(request.FilePath, ct));

            // Remove lines first (in reverse order to preserve indices)
            foreach (var lineNum in request.LinesToRemove.OrderByDescending(l => l))
            {
                if (lineNum > 0 && lineNum <= lines.Count)
                    lines.RemoveAt(lineNum - 1);
            }

            // Add lines at the end
            int insertIndex = lines.Count;
            foreach (var addLine in request.LinesToAdd)
            {
                lines.Insert(insertIndex, addLine);
                insertIndex++;
            }

            await File.WriteAllLinesAsync(request.FilePath, lines, ct);

            return new FilePatchResult(
                Convert.ToInt64(request.LinesToAdd.Count),
                request.LinesToRemove.Count,
                true);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to patch file: {FilePath}", request.FilePath);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchFileResult>> SearchFilesAsync(SearchFilesRequest request, CancellationToken ct = default)
    {
        if (request == null || string.IsNullOrEmpty(request.Pattern))
            throw new ArgumentException("Pattern is required.", nameof(request));

        var results = new List<SearchFileResult>();
        string? searchRoot = request.RootPath ?? GetActiveProjectRootPath();
        try
        {
            if (!Directory.Exists(searchRoot))
                return results;

            // Search all files recursively for regex pattern matches
            foreach (var filePath in Directory.EnumerateFiles(searchRoot, "*", new EnumerationOptions { RecurseSubdirectories = true }))
            {
                if (ct.IsCancellationRequested) throw new OperationCanceledException(ct);

                try
                {
                    string content = await File.ReadAllTextAsync(filePath);
                    var lines = content.Split('\n');

                    for (int i = 0; i < lines.Length && results.Count < request.MaxResults; i++)
                    {
                        if (Regex.IsMatch(lines[i], request.Pattern, RegexOptions.Compiled))
                            results.Add(new SearchFileResult(filePath, i + 1, lines[i].Trim(), results.Count + 1));
                    }
                }
                catch (IOException)
                {
                    // Skip files that can't be read (permissions, etc.)
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SearchFiles failed in directory: {RootPath}", searchRoot ?? "unknown");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ProjectExplorerResult> ExploreProjectAsync(ProjectExplorerRequest? request = null, CancellationToken ct = default)
    {
        var nodes = new List<ProjectNode>();

        string rootPath = request?.RootPath ?? GetActiveProjectRootPath() ?? Directory.GetCurrentDirectory();
        try
        {
            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
                return new ProjectExplorerResult(Array.Empty<ProjectNode>(), 0, 0);

            // Build directory tree recursively
            var directories = Directory.GetDirectories(rootPath!, "*", new EnumerationOptions { RecurseSubdirectories = true });
            var files = await GetFilesRecursiveAsync(rootPath, request?.IncludeHiddenFiles ?? false);

            foreach (var dir in directories)
                nodes.Add(CreateDirectoryNode(dir));

            foreach (var filePath in files)
                nodes.Add(CreateFileNode(filePath));

            return new ProjectExplorerResult(
                nodes.AsReadOnly(),
                Convert.ToInt64(directories.Length),
                Convert.ToInt64(files.Count));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to explore project directory: {RootPath}", rootPath);
            throw;
        }
    }

    /// <inheritdoc />
    public string? GetActiveProjectRootPath() => null;  // No active project set — could be configured via TaskContextSnapshot

    private async Task<IReadOnlyList<string>> GetFilesRecursiveAsync(string directory, bool includeHidden)
    {
        var files = new List<string>();

        foreach (var file in Directory.GetFiles(directory))
        {
            if (!includeHidden && Path.GetFileName(file)[0] == '.') continue;
            files.Add(file);
        }

        if (includeHidden) return files;  // Already included hidden dirs in recursion

        return files;
    }

    private ProjectNode CreateDirectoryNode(string directoryPath) => new(
        directoryPath,
        true,
        0,
        Directory.GetLastWriteTime(directoryPath),
        null);

    private ProjectNode CreateFileNode(string filePath) => new(
        filePath,
        false,
        new FileInfo(filePath).Length,
        File.GetLastWriteTime(filePath),
        null);

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            // No unmanaged resources to dispose.
        }
    }
}
