using System.Collections.Generic;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Represents a file read operation request.
/// </summary>
public record FileReadRequest(
    string FilePath,
    int? MaxLines = null);

/// <summary>
/// Result of a file read operation.
/// </summary>
public record FileReadResult(
    string Content,
    long TotalSizeBytes,
    bool Truncated);

/// <summary>
/// Represents a file write operation request.
/// </summary>
public record FileWriteRequest(
    string FilePath,
    string Content,
    bool Append = false);

/// <summary>
/// Result of a file write operation.
/// </summary>
public record FileWriteResult(
    long BytesWritten,
    bool OverwrittenExistingFile);

/// <summary>
/// Represents a file patch (modify) operation request.
/// </summary>
public record FilePatchRequest(
    string FilePath,
    IReadOnlyList<string> LinesToAdd,
    IReadOnlyList<int> LinesToRemove);

/// <summary>
/// Result of a file patch operation.
/// </summary>
public record FilePatchResult(
    long LinesAdded,
    long LinesRemoved,
    bool Success);

/// <summary>
/// Represents a command execution request in the agent sandbox.
/// </summary>
public record CommandExecuteRequest(
    string Command,
    IReadOnlyDictionary<string, string>? EnvironmentVariables = null,
    int TimeoutSeconds = 120);

/// <summary>
/// Result of a command execution.
/// </summary>
public record CommandExecuteResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    double DurationMs);

/// <summary>
/// Represents a project file search request (regex across all files in active project).
/// </summary>
public record SearchFilesRequest(
    string Pattern,
    string? RootPath = null,
    int MaxResults = 100);

/// <summary>
/// Result of a file search operation.
/// </summary>
public record SearchFileResult(
    string FilePath,
    long LineNumber,
    string MatchedLine,
    long TotalMatches);

/// <summary>
/// Represents a git diff request comparing two refs (or working directory vs HEAD).
/// </summary>
public record GitDiffRequest(
    string? FromRef = null,  // If null, compares to HEAD or working directory
    string? ToRef = "HEAD",   // If null, compares working directory
    bool? ShowUnifiedDiff = true);

/// <summary>
/// Result of a git diff operation.
/// </summary>
public record GitDiffResult(
    IReadOnlyList<string> UnifiedDiffLines,
    long TotalAdditions,
    long TotalDeletions);

/// <summary>
/// Represents a request to list directory contents recursively.
/// </summary>
public record ProjectExplorerRequest(
    string? RootPath = null,  // If null, uses the active project path from TaskContextSnapshot
    bool IncludeHiddenFiles = false);

/// <summary>
/// Result of a project explorer operation.
/// </summary>
public record ProjectExplorerResult(
    IReadOnlyList<ProjectNode> Nodes,
    long TotalDirectories,
    long TotalFiles);

/// <summary>
/// Represents a node in the active project tree (file or directory).
/// </summary>
public record ProjectNode(
    string Path,
    bool IsDirectory,
    long SizeBytes,
    DateTime LastModified,
    IReadOnlyList<ProjectNode>? Children = null);

/// <summary>
/// Represents a request to list git commit history.
/// </summary>
public record GitHistoryRequest(
    int? Count = 20,
    string? Path = null);

/// <summary>
/// Result of a git history query.
/// </summary>
public record GitHistoryResult(
    IReadOnlyList<GitCommitEntry> Commits);

/// <summary>
/// Represents a single commit in the git history result.
/// </summary>
public record GitCommitEntry(
    string Hash,
    string ShortHash,
    string AuthorName,
    DateTime CommittedAt,
    string Message,
    IReadOnlyList<GitDiffSummary>? DiffSummaries = null);

/// <summary>
/// Represents a diff summary for a single file within a commit.
/// </summary>
public record GitDiffSummary(
    string FilePath,
    string? OldPath, // For renames
    int Additions,
    int Deletions);

/// <summary>
/// Service for reading and writing files safely in the agent sandbox.
/// </summary>
public interface IFileOperationsService : IDisposable
{
    /// <summary>
    /// Reads a file with optional line limit.
    /// </summary>
    Task<FileReadResult> ReadFileAsync(FileReadRequest request, CancellationToken ct = default);

    /// <summary>
    /// Writes content to a file (creates or overwrites).
    /// </summary>
    Task<FileWriteResult> WriteFileAsync(FileWriteRequest request, CancellationToken ct = default);

    /// <summary>
    /// Applies a patch (add/remove lines) to an existing file.
    /// </summary>
    Task<FilePatchResult> PatchFileAsync(FilePatchRequest request, CancellationToken ct = default);

    /// <summary>
    /// Searches files in the project root for regex matches.
    /// </summary>
    Task<IReadOnlyList<SearchFileResult>> SearchFilesAsync(SearchFilesRequest request, CancellationToken ct = default);

    /// <summary>
    /// Lists directory contents recursively as a tree structure.
    /// </summary>
    Task<ProjectExplorerResult> ExploreProjectAsync(ProjectExplorerRequest? request = null, CancellationToken ct = default);

    /// <summary>
    /// Gets the current project root path (from TaskContextSnapshot or config).
    /// Returns null if no active project is set.
    /// </summary>
    string? GetActiveProjectRootPath();
}

/// <summary>
/// Service for executing shell commands in a sandboxed environment.
/// </summary>
public interface ICommandExecutionService : IDisposable
{
    /// <summary>
    /// Executes a command with the given parameters and timeout.
    /// </summary>
    Task<CommandExecuteResult> ExecuteAsync(CommandExecuteRequest request, CancellationToken ct = default);

    /// <summary>
    /// Cancels an in-progress sandboxed process by ID.
    /// </summary>
    Task CancelAsync(int processId);

    /// <summary>
    /// Gets the current list of active sandboxed processes and their status.
    /// </summary>
    IReadOnlyList<SandboxProcessInfo> GetActiveProcesses();

    /// <summary>
    /// Gets a summary of resource usage for an active sandboxed process.
    /// </summary>
    Task<SandboxResourceUsage> GetResourceUsageAsync(int processId);
}

/// <summary>
/// Information about an active sandboxed process.
/// </summary>
public record SandboxProcessInfo(
    int ProcessId,
    string Command,
    DateTime StartedAt,
    bool IsRunning,
    double ElapsedMs);

/// <summary>
/// Resource usage for a sandboxed process (CPU time, memory).
/// </summary>
public record SandboxResourceUsage(
    TimeSpan CpuTimeUsed,
    long PeakWorkingSetBytes,
    long CurrentWorkingSetBytes);