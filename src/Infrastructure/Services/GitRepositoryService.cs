using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Concrete implementation of IGitRepositoryService using git CLI for deep git repository operations.
/// </summary>
public class GitRepositoryService : IGitRepositoryService, IDisposable
{
    private readonly ILogger<GitRepositoryService>? _logger;
    private string? _repoRoot;
    private bool _disposed;

    /// <inheritdoc />
    public string? RepositoryRoot => _repoRoot;

    /// <inheritdoc />
    public bool IsRepositoryAvailable => !string.IsNullOrEmpty(_repoRoot);

    /// <summary>
    /// Creates a new GitRepositoryService instance.
    /// </summary>
    public GitRepositoryService(ILogger<GitRepositoryService>? logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> InitializeFromPathAsync(string workingDirectory)
    {
        try
        {
            var result = await RunGitCommandAsync(workingDirectory, "rev-parse --show-toplevel", null);
            
            if (result.ExitCode != 0 || string.IsNullOrEmpty(result.StandardOutput))
            {
                _repoRoot = null;
                return false;
            }

            _repoRoot = result.StandardOutput.TrimEnd('\r', '\n');
            _logger?.LogDebug("Initialized GitRepositoryService for repository: {RepoPath}", _repoRoot);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize git repository from path: {WorkingDirectory}", workingDirectory);
            _repoRoot = null;
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitBranchInfo>> ListBranchesAsync()
    {
        if (_repoRoot == null) throw new InvalidOperationException("Git repository not initialized.");

        var result = await RunGitCommandAsync(_repoRoot, "for-each-ref --format='%(refname:short)|%(objectname:short)|%(isHEAD)' refs/heads/", null);
        
        if (result.ExitCode != 0) return Array.Empty<GitBranchInfo>();

        return ParseBranches(result.StandardOutput).AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitTagInfo>> ListTagsAsync()
    {
        if (_repoRoot == null) throw new InvalidOperationException("Git repository not initialized.");

        var result = await RunGitCommandAsync(_repoRoot, "for-each-ref --format='%(refname:short)|%(objectname:short)|%(type)' refs/tags/", null);
        
        if (result.ExitCode != 0) return Array.Empty<GitTagInfo>();

        return ParseTags(result.StandardOutput).AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitRemoteInfo>> ListRemotesAsync()
    {
        if (_repoRoot == null) throw new InvalidOperationException("Git repository not initialized.");

        var result = await RunGitCommandAsync(_repoRoot, "remote -v", null);
        
        if (result.ExitCode != 0) return Array.Empty<GitRemoteInfo>();

        return ParseRemotes(result.StandardOutput).AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<string?> GetCurrentBranchNameAsync()
    {
        if (_repoRoot == null) throw new InvalidOperationException("Git repository not initialized.");

        var result = await RunGitCommandAsync(_repoRoot, "name-rev --name-only HEAD", null);
        
        if (result.ExitCode != 0) return null;

        return result.StandardOutput.Trim();
    }

    /// <inheritdoc />
    public async Task<string?> GetCommitShaForRefAsync(string? refName = null)
    {
        if (_repoRoot == null) throw new InvalidOperationException("Git repository not initialized.");

        var refToUse = refName ?? "HEAD";
        var result = await RunGitCommandAsync(_repoRoot, $"rev-parse --verify {refToUse}", null);
        
        return result.ExitCode == 0 ? result.StandardOutput.Trim() : null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitCommitInfo>> ListCommitsAsync(int maxCount = 30, string? pathFilter = null)
    {
        if (_repoRoot == null) throw new InvalidOperationException("Git repository not initialized.");

        var formatString = "%H|%s|%aN|%aE|%aI";
        var logArgs = $"--max-count={maxCount} --format=format:{formatString}";
        
        if (!string.IsNullOrEmpty(pathFilter))
            logArgs += " -- ";

        // Re-add path after the double dash — git interprets everything before -- as options
        if (pathFilter != null)
            logArgs += $" {EscapeArg(pathFilter)}";
        else
            logArgs += " HEAD";

        var result = await RunGitCommandAsync(_repoRoot, logArgs, null);
        
        if (result.ExitCode != 0 || string.IsNullOrEmpty(result.StandardOutput)) return Array.Empty<GitCommitInfo>();

        return ParseCommits(result.StandardOutput).AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<GitDiffInfo?> GetDiffBetweenRefsAsync(string? fromRef = null, string? toRef = null, bool includeUnifiedDiff = false)
    {
        if (_repoRoot == null) throw new InvalidOperationException("Git repository not initialized.");

        var diffInfo = new GitDiffInfo();
        
        // Determine the refs for the diff
        string leftRef, rightRef;
        
        if (fromRef != null && toRef != null)
        {
            leftRef = fromRef;
            rightRef = toRef;
        }
        else if (toRef == null && fromRef == null)
        {
            // Compare working directory against staged changes
            var result = await RunGitCommandAsync(_repoRoot, "diff --staged --numstat", null);
            
            if (result.ExitCode != 0) return diffInfo;

            ParseNumStat(result.StandardOutput, diffInfo, includeUnifiedDiff: false);
            return diffInfo;
        }
        else if (toRef == null && fromRef != null)
        {
            // Compare HEAD vs working directory
            var result = await RunGitCommandAsync(_repoRoot, "diff --numstat", null);
            
            if (result.ExitCode != 0) return diffInfo;

            ParseNumStat(result.StandardOutput, diffInfo, includeUnifiedDiff: false);
            return diffInfo;
        }
        else
        {
            leftRef = fromRef ?? "HEAD";
            rightRef = toRef;
        }

        // Get numstat for stats
        var numstatResult = await RunGitCommandAsync(_repoRoot, $"diff --numstat {EscapeArg(leftRef)}..{rightRef}", null);
        
        if (numstatResult.ExitCode == 0 && !string.IsNullOrEmpty(numstatResult.StandardOutput))
            ParseNumStat(numstatResult.StandardOutput, diffInfo, includeUnifiedDiff: false);

        // Get unified diff if requested
        if (includeUnifiedDiff)
        {
            var unifiedResult = await RunGitCommandAsync(_repoRoot, $"diff -U0 {EscapeArg(leftRef)}..{rightRef}", null);
            
            if (unifiedResult.ExitCode == 0 && !string.IsNullOrEmpty(unifiedResult.StandardOutput))
                diffInfo.UnifiedDiff = unifiedResult.StandardOutput;
        }

        return diffInfo;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitStatusEntry>> GetFileStatusAsync()
    {
        if (_repoRoot == null) throw new InvalidOperationException("Git repository not initialized.");

        var result = await RunGitCommandAsync(_repoRoot, "status --porcelain=v1", null);
        
        if (result.ExitCode != 0 || string.IsNullOrEmpty(result.StandardOutput)) return Array.Empty<GitStatusEntry>();

        return ParseFileStatus(result.StandardOutput).AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListUntrackedFilesAsync(string? directoryFilter = null)
    {
        if (_repoRoot == null) throw new InvalidOperationException("Git repository not initialized.");

        var args = "ls-files --others --exclude-standard";
        
        if (!string.IsNullOrEmpty(directoryFilter))
            args += $" \"{EscapeArg(directoryFilter)}\"";

        var result = await RunGitCommandAsync(_repoRoot, args, null);
        
        if (result.ExitCode != 0 || string.IsNullOrEmpty(result.StandardOutput)) return Array.Empty<string>();

        return ParseUntrackedFiles(result.StandardOutput).AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<int, string?>> GetBlameForFileAsync(string filePath)
    {
        if (_repoRoot == null) throw new InvalidOperationException("Git repository not initialized.");

        var result = await RunGitCommandAsync(_repoRoot, $"blame --line-porcelain \"{EscapeArg(filePath)}\"", null);
        
        if (result.ExitCode != 0 || string.IsNullOrEmpty(result.StandardOutput)) return new Dictionary<int, string?>();

        return ParseBlame(result.StandardOutput).AsReadOnly();
    }

    /// <inheritdoc />
    public async Task CheckoutRefAsync(string refName)
    {
        if (_repoRoot == null) throw new InvalidOperationException("Git repository not initialized.");

        var result = await RunGitCommandAsync(_repoRoot, $"checkout \"{EscapeArg(refName)}\"", null);
        
        if (result.ExitCode != 0)
            _logger?.LogError("Failed to checkout ref: {RefName} - ExitCode: {ExitCode}", refName, result.ExitCode);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // No unmanaged resources — git CLI processes are cleaned up automatically
    }

    // ---- Helpers ----

    private async Task<GitProcessResult> RunGitCommandAsync(string workingDir, string args, IDictionary<string, string>? extraEnv = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = args,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        // Add extra environment variables if provided (e.g., git config overrides)
        if (extraEnv != null)
            foreach (var kvp in extraEnv)
                startInfo.EnvironmentVariables[kvp.Key] = kvp.Value;

        using var process = Process.Start(startInfo)!;
        
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new GitProcessResult(process.ExitCode, stdout, stderr);
    }

    private List<GitBranchInfo> ParseBranches(string output)
    {
        var branches = new List<GitBranchInfo>();
        
        foreach (var line in output.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('|');
            if (parts.Length < 3) continue;

            // For full ref names, ShortName is computed from Name via the property getter
            var branchInfo = new GitBranchInfo
            {
                Name = string.IsNullOrEmpty(parts[0]) ? "HEAD" : parts[0],
                TipSha = parts[1],
                IsCurrentBranch = parts[2] == "yes"
            };

            branches.Add(branchInfo);
        }

        return branches;
    }

    private List<GitTagInfo> ParseTags(string output)
    {
        var tags = new List<GitTagInfo>();
        
        foreach (var line in output.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('|');
            if (parts.Length < 3) continue;

            // Check if the tag is an annotated tag by looking at the type
            var isAnnotated = parts[2] == "tag";
            
            tags.Add(new GitTagInfo
            {
                Name = parts[0],
                TargetSha = parts[1],
                IsAnnotatedTag = isAnnotated
            });
        }

        return tags;
    }

    private List<GitRemoteInfo> ParseRemotes(string output)
    {
        var remotes = new List<GitRemoteInfo>();
        
        foreach (var line in output.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            // Format: origin  https://github.com/.../ (fetch/push)
            var parts = line.Trim().Split('\t');
            if (parts.Length < 2) continue;

            // Skip duplicates (same remote with fetch/push URLs)
            if (remotes.Any(r => r.Name == parts[0])) continue;

            remotes.Add(new GitRemoteInfo
            {
                Name = parts[0],
                Url = parts[1].Trim(),
                IsFetchOnly = false
            });
        }

        return remotes;
    }

    private List<GitCommitInfo> ParseCommits(string output)
    {
        var commits = new List<GitCommitInfo>();
        
        foreach (var line in output.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            // Format: SHA|message|authorName|authorEmail|date
            var parts = line.Split('|');
            if (parts.Length < 5) continue;

            commits.Add(new GitCommitInfo
            {
                Sha = parts[0],
                Message = parts[1],
                AuthorName = parts[2],
                AuthorEmail = parts[3],
                CommittedAt = DateTimeOffset.Parse(parts[4])
            });
        }

        return commits;
    }

    private void ParseNumStat(string output, GitDiffInfo diffInfo, bool includeUnifiedDiff)
    {
        var files = new List<GitDiffFileInfo>();
        
        foreach (var line in output.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            // Format: added\tdeleted\tfile_path (or \t for binary)
            var parts = line.Split('\t');
            if (parts.Length < 3) continue;

            var fileInfo = new GitDiffFileInfo();
            
            int.TryParse(parts[0], out var added);
            int.TryParse(parts[1], out var deleted);
            
            // \t means binary file — skip it for stats purposes but record the path
            if (parts[0] == "\\")
            {
                fileInfo.Path = parts[2];
                diffInfo.AddFile(fileInfo);
                continue;
            }

            fileInfo.Path = parts[2];
            fileInfo.LinesAdded = added;
            fileInfo.LinesDeleted = deleted;
            
            diffInfo.AddFile(fileInfo);
            diffInfo.LinesAdded += added;
            diffInfo.LinesDeleted += deleted;
        }
    }

    private List<GitStatusEntry> ParseFileStatus(string output)
    {
        var statuses = new List<GitStatusEntry>();
        
        foreach (var line in output.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            // Format: XY filename (for v1 porcelain format)
            if (line.Length < 3 || string.IsNullOrWhiteSpace(line.Substring(2))) continue;

            var indexStatus = line[0];
            var workingTreeStatus = line[1];
            var fileName = line.Substring(2).Trim();
            
            // Handle rename entries: R old_file -> new_file
            if (indexStatus == 'R' && fileName.Contains(" -> "))
            {
                var renameParts = fileName.Split(new[] { " -> " }, StringSplitOptions.None);
                fileName = renameParts[1];

                var newEntry = new GitStatusEntry { Path = fileName };
                newEntry.AddStatus("index", indexStatus);
                newEntry.AddStatus("workingTree", workingTreeStatus);
                statuses.Add(newEntry);

                // Also add the old path with renamed status
                var oldPathEntry = new GitStatusEntry();
                oldPathEntry.Path = renameParts[0];
                oldPathEntry.AddStatus("index", indexStatus);
                oldPathEntry.AddStatus("workingTree", '?');

                statuses.Add(oldPathEntry);
                continue;
            }

            var entry = new GitStatusEntry { Path = fileName };
            entry.AddStatus("index", indexStatus);
            entry.AddStatus("workingTree", workingTreeStatus);

            statuses.Add(entry);
        }

        return statuses;
    }

    private List<string> ParseUntrackedFiles(string output)
    {
        var files = new List<string>();
        
        foreach (var line in output.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            files.Add(line.Trim());

        return files;
    }

    private Dictionary<int, string?> ParseBlame(string output)
    {
        var result = new Dictionary<int, string?>();
        
        var lines = output.Split('\n', StringSplitOptions.None);
        int currentLineNum = 0;
        
        for (int i = 0; i < lines.Length; i++)
        {
            // Blame porcelain format: first line of each entry has: full_sha start_line end_line [parent_info]
            var line = lines[i];
            
            if (line.StartsWith(" ") || line.StartsWith("\t"))
                continue;

            // Parse the commit info line
            var parts = line.Split(' ');
            if (parts.Length < 3) continue;

            int.TryParse(parts[2], out currentLineNum);
            currentLineNum--; // Line numbers are 1-based in blame output
            
            if (currentLineNum <= 0) continue;

            // Get the message from the next lines or use abbreviated SHA as fallback
            var sha = parts[0];
            
            // Look ahead for the commit message (starts with tab)
            string? message = null;
            for (int j = i + 1; j < Math.Min(i + 8, lines.Length); j++)
            {
                if (lines[j].StartsWith("\t"))
                {
                    message = lines[j].Trim();
                    break;
                }
            }

            // Look for author line
            string? authorName = null;
            for (int j = i + 1; j < Math.Min(i + 8, lines.Length); j++)
            {
                if (lines[j].StartsWith("author "))
                {
                    authorName = lines[j].Substring(7).Trim();
                    break;
                }
            }

            var displayMessage = message ?? $"({sha.Substring(0, Math.Min(7, sha.Length))})";

            for (int j = 1; j <= int.Parse(parts[3]); j++) // End line is in parts[3]
            {
                result[currentLineNum + j - 1] = $"{currentLineNum + j}: {displayMessage} ({authorName ?? "unknown"})";
            }
        }

        return result;
    }

    private string EscapeArg(string arg) => arg.Contains(' ') ? $"\"{arg}\"" : arg;

    // ---- Internal result type for git CLI execution ----
    
    private readonly struct GitProcessResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public int ExitCode { get; } = ExitCode;
        public string StandardOutput { get; } = StandardOutput;
        public string StandardError { get; } = StandardError;
    }
}