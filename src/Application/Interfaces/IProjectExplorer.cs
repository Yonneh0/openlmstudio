using System.Collections.Generic;
using OpenLMStudio.Application.Types;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Service for exploring the project directory and building a tree of files and folders.
/// </summary>
public interface IProjectExplorer
{
    /// <summary>
    /// Builds a project tree from the given root path.
    /// </summary>
    Task<IReadOnlyList<ProjectNode>> GetProjectTreeAsync(string rootPath);

    /// <summary>
    /// Gets a file preview (first N lines) for a text file.
    /// </summary>
    Task<FilePreviewResult?> GetFilePreviewAsync(string filePath, int maxLines = 100);

    /// <summary>
    /// Determines if a file is binary and cannot be previewed.
    /// </summary>
    bool IsBinaryFile(string filePath);

    /// <summary>
    /// Gets git status for all files in the project directory (if within a git repository).
    /// </summary>
    Task<IReadOnlyDictionary<string, string?>> GetGitStatusAsync(string projectPath);
}