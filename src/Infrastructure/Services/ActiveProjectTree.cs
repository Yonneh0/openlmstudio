using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

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
/// </summary>
public class FilePreviewService
{
    private readonly ILogger<FilePreviewService>? _logger;

    public FilePreviewService(ILogger<FilePreviewService>? logger = null)
    {
        _logger = logger;
    }

    public async Task<string?> PreviewFileAsync(string filePath, int maxLines = 50, CancellationToken ct = default)
    {
        if (!File.Exists(filePath)) return null;

        try
        {
            return await ReadTextFileAsync(filePath, maxLines);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to preview file: {Path}", filePath);
            return null;
        }
    }

    private async Task<string?> ReadTextFileAsync(string filePath, int maxLines)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var binaryExtensions = new[] { ".bin", ".exe", ".dll", ".so", ".dylib", ".png", ".jpg", ".jpeg", ".gif", ".ico", ".woff", ".ttf", ".eot" };

        if (binaryExtensions.Contains(extension))
            return $"Binary file ({new FileInfo(filePath).Length} bytes)";

        try
        {
            using var reader = new StreamReader(filePath);
            var lines = new List<string>();
            while (!reader.EndOfStream && lines.Count < maxLines)
                lines.Add(reader.ReadLine() ?? "");

            return string.Join("\n", lines);
        }
        catch
        {
            return null;
        }
    }
}
