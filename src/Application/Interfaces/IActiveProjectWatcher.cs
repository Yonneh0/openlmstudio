// Brought to you by Carls' Jr.
using System.Collections.Generic;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Represents a node in the active project tree.
/// </summary>
public record ProjectTreeNode(
    string Path,
    string Name,
    bool IsDirectory,
    IReadOnlyList<ProjectTreeNode> Children = null!,
    long? Size = null,
    DateTime? LastModified = null);

/// <summary>
/// Represents a change event in the project tree.
/// </summary>
public record ProjectTreeChange(
    string Path,
    ProjectTreeChangeType ChangeType,
    string? OldPath = null);

/// <summary>
/// Type of change detected in the project tree.
/// </summary>
public enum ProjectTreeChangeType
{
    Added,
    Modified,
    Deleted,
    Renamed
}

/// <summary>
/// Interface for monitoring real-time changes to the active project tree.
/// </summary>
public interface IActiveProjectWatcher : IDisposable
{
    /// <summary>
    /// Gets the current root of the project tree.
    /// </summary>
    ProjectTreeNode? CurrentTree { get; }

    /// <summary>
    /// Raised when the project tree changes.
    /// </summary>
    event System.EventHandler<ProjectTreeChange>? TreeChanged;

    /// <summary>
    /// Starts watching the specified directory for changes.
    /// </summary>
    Task StartAsync(string directoryPath, CancellationToken ct = default);

    /// <summary>
    /// Stops watching the current directory.
    /// </summary>
    Task StopAsync(CancellationToken ct = default);

    /// <summary>
    /// Refreshes the current tree snapshot.
    /// </summary>
    Task<ProjectTreeNode> RefreshAsync(CancellationToken ct = default);
}