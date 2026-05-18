using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Represents a git commit with its metadata.
/// </summary>
public class GitCommitInfo
{
    public string Sha { get; set; } = string.Empty;
    public string ShortSha => Sha.Length > 7 ? Sha.Substring(0, 7) : Sha;
    public string Message { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorEmail { get; set; } = string.Empty;
    public DateTimeOffset CommittedAt { get; set; }
}

/// <summary>
/// Represents a git diff between two refs.
/// </summary>
public class GitDiffInfo
{
    /// <summary>The number of lines added.</summary>
    public int LinesAdded { get; set; }

    /// <summary>The number of lines deleted.</summary>
    public int LinesDeleted { get; set; }

    /// <summary>The unified diff text (if requested).</summary>
    public string? UnifiedDiff { get; set; }

    /// <summary>File-level diff stats for each changed file.</summary>
    public IReadOnlyList<GitDiffFileInfo>? Files => _files.AsReadOnly();
    private readonly List<GitDiffFileInfo> _files = new();

    public void AddFile(GitDiffFileInfo file) => _files.Add(file);

    public override string ToString() => $"+{LinesAdded} -{LinesDeleted}";
}

/// <summary>A single file-level diff in a GitDiffInfo.</summary>
public class GitDiffFileInfo
{
    public string Path { get; set; } = string.Empty;
    public int LinesAdded { get; set; }
    public int LinesDeleted { get; set; }
    public bool IsRename => NewPath != Path;
    public string? OldPath { get; set; } // For renames
    public string? NewPath { get; set; } // For renames

    public override string ToString() => $"{Path} ({LinesAdded:+0;-0} / -{LinesDeleted})";
}

/// <summary>Represents a git branch.</summary>
public class GitBranchInfo
{
    /// <summary>The full ref name (e.g., "refs/heads/main").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The short name (e.g., "main", "feature/foo").</summary>
    public string ShortName => Name.StartsWith("refs/") ? Name.Substring(Name.LastIndexOf('/') + 1) : Name;

    /// <summary>The latest commit SHA for this branch.</summary>
    public string? TipSha { get; set; }

    /// <summary>Whether this is the currently checked-out branch.</summary>
    public bool IsCurrentBranch { get; set; }
}

/// <summary>Represents a git tag reference.</summary>
public class GitTagInfo
{
    /// <summary>The full ref name (e.g., "refs/tags/v1.0").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The short name (e.g., "v1.0").</summary>
    public string ShortName => Name.StartsWith("refs/") ? Name.Substring(Name.LastIndexOf('/') + 1) : Name;

    /// <summary>The commit SHA the tag points to.</summary>
    public string? TargetSha { get; set; }

    /// <summary>Whether this is an annotated tag (vs lightweight).</summary>
    public bool IsAnnotatedTag { get; set; }

    /// <summary>The tagger name for annotated tags.</summary>
    public string? TaggerName { get; set; }

    /// <summary>The date of the annotated tag creation.</summary>
    public DateTimeOffset? TaggedAt { get; set; }
}

/// <summary>Represents a git remote reference.</summary>
public class GitRemoteInfo
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool IsFetchOnly { get; set; }
}

/// <summary>Represents a git status entry for a single file.</summary>
public class GitStatusEntry
{
    /// <summary>The relative path of the file from the working directory root.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Working tree status (e.g., "M" for modified, "?" for untracked).</summary>
    public char WorkingTreeStatus => _workingTreeStatus[0];
    private readonly string _workingTreeStatus = "?"; // Default to unknown

    /// <summary>Index/status in staging area.</summary>
    public char IndexStatus => _indexStatus[0];
    private readonly string _indexStatus = "?"; // Default to unknown

    /// <summary>The full status strings from Git (e.g., "M" and "A").</summary>
    public IReadOnlyDictionary<string, char> Statuses => _statuses.AsReadOnly();
    private readonly Dictionary<string, char> _statuses = new();

    /// <summary>Add a status code-letter pair. E.g., ("index", 'M') or ("workingTree", "?").</summary>
    public void AddStatus(string scope, char letter) => _statuses[scope] = letter;

    /// <summary>The short display of this entry's statuses.</summary>
    public override string ToString() => $"[{_indexStatus[0]}{_workingTreeStatus[0]}] {Path}";
}

/// <summary>
/// Interface for deep git repository operations.
/// </summary>
public interface IGitRepositoryService : IDisposable
{
    /// <summary>The path to the detected root of the git repository.</summary>
    string? RepositoryRoot { get; }

    /// <summary>Whether a valid git repository was found at or above the given working directory.</summary>
    bool IsRepositoryAvailable { get; }

    /// <summary>
    /// Detects whether there is a git repository at or above the specified path.
    /// </summary>
    Task<bool> InitializeFromPathAsync(string workingDirectory);

    /// <summary>Lists branches in the repository.</summary>
    Task<IReadOnlyList<GitBranchInfo>> ListBranchesAsync();

    /// <summary>Lists tags in the repository.</summary>
    Task<IReadOnlyList<GitTagInfo>> ListTagsAsync();

    /// <summary>Lists remotes configured for the repository.</summary>
    Task<IReadOnlyList<GitRemoteInfo>> ListRemotesAsync();

    /// <summary>Gets the currently checked-out branch name.</summary>
    Task<string?> GetCurrentBranchNameAsync();

    /// <summary>Gets the latest commit SHA on a given branch (or HEAD if null).</summary>
    Task<string?> GetCommitShaForRefAsync(string? refName = null);

    /// <summary>Lists recent commits, optionally filtered by a path.</summary>
    Task<IReadOnlyList<GitCommitInfo>> ListCommitsAsync(int maxCount = 30, string? pathFilter = null);

    /// <summary>Gets the diff between two refs (or HEAD vs working tree if both are null).</summary>
    Task<GitDiffInfo?> GetDiffBetweenRefsAsync(string? fromRef = null, string? toRef = null, bool includeUnifiedDiff = false);

    /// <summary>Gets the file-level git status for all tracked files.</summary>
    Task<IReadOnlyList<GitStatusEntry>> GetFileStatusAsync();

    /// <summary>Lists untracked files in the repository (respecting .gitignore).</summary>
    Task<IReadOnlyList<string>> ListUntrackedFilesAsync(string? directoryFilter = null);

    /// <summary>Gets blame annotation for a file — line-by-line attribution of who wrote each line.</summary>
    Task<IReadOnlyDictionary<int, string?>> GetBlameForFileAsync(string filePath);

    /// <summary>Checks out the working tree to match the specified ref (or HEAD).</summary>
    Task CheckoutRefAsync(string refName);
}