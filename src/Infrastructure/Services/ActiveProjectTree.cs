using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Represents a node in the project tree (file or directory).
/// </summary>
public class ProjectTreeNode
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public DateTime LastModified { get; set; }
    public long SizeBytes { get; set; }
    public List<ProjectTreeNode> Children { get; set; } = new();
    public bool HasChildren { get; set; }
    public bool IsExpanded { get; set; }
}

/// <summary>
/// Event arguments for project tree changes.
/// </summary>
public class ProjectTreeChangedEventArgs : EventArgs
{
    public ProjectTreeChangeType ChangeType { get; }
    public string Path { get; }
    public string? OldPath { get; }

    public ProjectTreeChangedEventArgs(ProjectTreeChangeType changeType, string path, string? oldPath = null)
    {
        ChangeType = changeType;
        Path = path;
        OldPath = oldPath;
    }
}

public enum ProjectTreeChangeType
{
    Created,
    Deleted,
    Changed,
    Renamed
}

/// <summary>
/// File preview service for the active project tree.
/// Implements IFilePreviewService for the agent sandbox.
/// </summary>
public class FilePreviewService : IFilePreviewService
{
    private readonly ILogger<FilePreviewService>? _logger;

    public FilePreviewService(ILogger<FilePreviewService>? logger = null)
    {
        _logger = logger;
    }

    public async Task<string?> GetPreviewAsync(string filePath, int maxLines = 100, CancellationToken ct = default)
    {
        if (!File.Exists(filePath)) return null;

        try
        {
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > 1024 * 1024)
                return null;

            if (IsBinaryFile(filePath))
                return null;

            using var reader = new StreamReader(filePath);
            var lines = new StringBuilder();
            var lineCount = 0;

            while (lineCount < maxLines && (await reader.ReadLineAsync(ct)) is string line)
            {
                if (lines.Length > 0)
                    lines.AppendLine();
                lines.Append(line);
                lineCount++;
            }

            return lineCount > 0 ? lines.ToString() : null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to preview file: {Path}", filePath);
            return null;
        }
    }

    private static bool IsBinaryFile(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            var buffer = new byte[8192];
            var bytesRead = fs.Read(buffer, 0, buffer.Length);
            for (var i = 0; i < bytesRead; i++)
            {
                if (buffer[i] == 0)
                    return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}
