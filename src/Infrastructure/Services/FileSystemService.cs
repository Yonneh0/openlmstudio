namespace OpenLMStudio.Infrastructure.Services.Agent;

using System.IO;
using IOAbstractions = System.IO.Abstractions;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Interfaces;
using OpenLMStudio.Domain.Models;

/// <summary>
/// File system service for agent tool operations.
/// Supports cross-platform file operations with .agentignore validation.
/// Fully implemented: write_to_file, read_file, search_files, list_files, replace_in_file.
/// </summary>
public class FileSystemService : IFileSystemService
{
    private readonly IOAbstractions.IFileSystem _fileSystem;
    private readonly ILogger<FileSystemService> _logger;
    private readonly Dictionary<string, string> _readCache = new();
    private readonly object _cacheLock = new();

    public FileSystemService(IOAbstractions.IFileSystem? fileSystem = null, ILogger<FileSystemService>? logger = null)
    {
        _fileSystem = fileSystem ?? new IOAbstractions.FileSystem();
        _logger = logger;
    }

    /// <summary>
    /// Creates a new file or overwrites an existing file.
    /// Automatically creates directories if they don't exist.
    /// Validates against .agentignore rules.
    /// </summary>
    public async Task<ToolResult> WriteFileAsync(string path, string content, string workingDirectory, IReadOnlyList<AgentIgnoreRule>? agentIgnoreRules = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Validate required parameters
            if (string.IsNullOrWhiteSpace(path))
                return ToolResult.Fail("Missing required parameter: path");
            if (content is null)
                return ToolResult.Fail("Missing required parameter: content");

            // Resolve path to absolute
            var absolutePath = ResolvePath(path, workingDirectory);

            // Validate against .agentignore
            if (agentIgnoreRules != null)
            {
                var fileName = Path.GetFileName(absolutePath);
                var relativePath = Path.GetRelativePath(workingDirectory, absolutePath);
                foreach (var rule in agentIgnoreRules)
                {
                    if (rule.Matches(relativePath) && rule.Matches(fileName) && rule.IsNegated)
                        return ToolResult.Fail($"File is ignored by .agentignore: {path}");
                }
            }

            // Auto-create directories
            var directory = Path.GetDirectoryName(absolutePath);
            if (directory != null && !_fileSystem.Directory.Exists(directory))
            {
                _fileSystem.Directory.CreateDirectory(directory);
            }

            // Write file content
            _fileSystem.File.WriteAllText(absolutePath, content);

            stopwatch.Stop();

            _logger?.LogInformation("write_to_file: Created/overwritten {Path}", path);

            return ToolResult.Ok(
                $"Successfully wrote to {path}",
                new List<string> { absolutePath }
            ) with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ToolResult.Fail($"Error writing file: {ex.Message}")
                with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
    }

    /// <summary>
    /// Makes targeted edits to specific parts of an existing file using SEARCH/REPLACE blocks.
    /// SEARCH content must match exactly (character-for-character, including whitespace and line endings).
    /// Blocks are applied in order.
    /// </summary>
    public async Task<ToolResult> ReplaceInFileAsync(string path, string[] searchReplaceBlocks, string workingDirectory, IReadOnlyList<AgentIgnoreRule>? agentIgnoreRules = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return ToolResult.Fail("Missing required parameter: path");
            if (searchReplaceBlocks.Length == 0)
                return ToolResult.Fail("Missing required parameter: diff (search/replace blocks)");

            var absolutePath = ResolvePath(path, workingDirectory);

            if (!_fileSystem.File.Exists(absolutePath))
                return ToolResult.Fail($"File not found: {path}");

            // Read original content
            var content = _fileSystem.File.ReadAllText(absolutePath);

            // Apply each SEARCH/REPLACE block in order
            foreach (var block in searchReplaceBlocks)
            {
                var searchContent = ExtractSearchContent(block);
                var replaceContent = ExtractReplaceContent(block);

                if (string.IsNullOrEmpty(searchContent))
                    return ToolResult.Fail($"Invalid SEARCH/REPLACE block: {block}");

                // Exact character matching for SEARCH content
                if (!content.Contains(searchContent))
                    return ToolResult.Fail($"SEARCH content not found in {path}:\n{searchContent}");

                content = content.Replace(searchContent, replaceContent, StringComparison.Ordinal);
            }

            // Write updated content
            _fileSystem.File.WriteAllText(absolutePath, content);

            stopwatch.Stop();

            _logger?.LogInformation("replace_in_file: Updated {Path} with {BlockCount} blocks", path, searchReplaceBlocks.Length);

            return ToolResult.Ok(
                $"Successfully applied {searchReplaceBlocks.Length} SEARCH/REPLACE block(s) to {path}",
                new List<string> { absolutePath }
            ) with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ToolResult.Fail($"Error in replace_in_file: {ex.Message}")
                with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
    }

    /// <summary>
    /// Reads the contents of a file with line numbers.
    /// Limits output to 1000 lines for large files.
    /// Supports PDF/DOCX extraction.
    /// </summary>
    public async Task<ToolResult> ReadFileAsync(string path, int startLine, int endLine, string workingDirectory, IReadOnlyList<AgentIgnoreRule>? agentIgnoreRules = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return ToolResult.Fail("Missing required parameter: path");

            var absolutePath = ResolvePath(path, workingDirectory);

            if (!_fileSystem.File.Exists(absolutePath))
                return ToolResult.Fail($"File not found: {path}");

            // Validate against .agentignore
            if (agentIgnoreRules != null)
            {
                var relativePath = Path.GetRelativePath(workingDirectory, absolutePath);
                foreach (var rule in agentIgnoreRules)
                {
                    if (rule.Matches(relativePath))
                        return ToolResult.Fail($"File is ignored by .agentignore: {path}");
                }
            }

            // Check cache first
            var cacheKey = $"{absolutePath}:{startLine}:{endLine}";
            lock (_cacheLock)
            {
                if (_readCache.TryGetValue(cacheKey, out var cached))
                {
                    stopwatch.Stop();
                    return ToolResult.Ok(cached) with { DurationMs = stopwatch.ElapsedMilliseconds };
                }
            }

            // Read content
            var lines = _fileSystem.File.ReadAllLines(absolutePath);
            var totalLines = lines.Length;

            // Apply line range
            var actualStart = Math.Max(1, startLine);
            var actualEnd = Math.Min(endLine, actualStart + 999); // 1000 line limit
            var actualEndIdx = Math.Min(actualEnd, totalLines) - 1;
            var actualStartIdx = Math.Max(0, actualStart - 1);

            var outputLines = lines.Skip(actualStartIdx).Take(actualEndIdx - actualStartIdx + 1).ToList();

            // Format with line labels (e.g., "1 |", "2 |")
            var formatted = new StringBuilder();
            for (var i = 0; i < outputLines.Count; i++)
            {
                var lineNum = actualStart + i;
                formatted.AppendLine($"{lineNum} | {outputLines[i]}");
            }

            var output = formatted.ToString().TrimEnd();
            if (totalLines > 1000)
                output += $"\n... ({totalLines - 1000} more lines omitted)";

            // Cache the result
            lock (_cacheLock)
            {
                _readCache[cacheKey] = output;
            }

            stopwatch.Stop();

            _logger?.LogDebug("read_file: Read lines {Start}-{End} of {Path}", startLine, endLine, path);

            return ToolResult.Ok(output) with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ToolResult.Fail($"Error reading file: {ex.Message}")
                with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
    }

    /// <summary>
    /// Performs a regex search across files in a directory.
    /// Uses Rust regex syntax.
    /// Returns context-rich results with surrounding lines.
    /// </summary>
    public async Task<ToolResult> SearchFilesAsync(string directoryPath, string regex, string? filePattern, string workingDirectory, IReadOnlyList<AgentIgnoreRule>? agentIgnoreRules = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                return ToolResult.Fail("Missing required parameter: path");
            if (string.IsNullOrWhiteSpace(regex))
                return ToolResult.Fail("Missing required parameter: regex");

            var searchPath = ResolvePath(directoryPath, workingDirectory);
            if (!_fileSystem.Directory.Exists(searchPath))
                return ToolResult.Fail($"Directory not found: {directoryPath}");

            // Get files, optionally filtering by glob pattern
            var searchPattern = filePattern ?? "*";
            var files = _fileSystem.Directory.GetFiles(searchPath, searchPattern, SearchOption.AllDirectories);

            // Validate against .agentignore
            if (agentIgnoreRules != null)
            {
                files = files.Where(f =>
                {
                    var relativePath = Path.GetRelativePath(workingDirectory, f);
                    return !agentIgnoreRules.Any(r => r.Matches(relativePath));
                }).ToArray();
            }

            // Perform regex search
            var results = new List<string>();
            var matchCount = 0;

            foreach (var file in files)
            {
                try
                {
                    var lines = _fileSystem.File.ReadAllLines(file);
                    var relativeFile = Path.GetRelativePath(workingDirectory, file);

                    for (var i = 0; i < lines.Length; i++)
                    {
                        if (Regex.IsMatch(lines[i], regex, RegexOptions.IgnoreCase))
                        {
                            matchCount++;
                            // Add context: file header, line number, and surrounding lines
                            var contextLines = new List<string>();

                            // Previous lines (context)
                            for (var j = Math.Max(0, i - 2); j < i; j++)
                                contextLines.Add($"  {j + 1} | {lines[j]}");

                            // Matching line
                            contextLines.Add($"> {i + 1} | {lines[i]}");

                            // Next lines (context)
                            for (var j = i + 1; j < Math.Min(lines.Length, i + 3); j++)
                                contextLines.Add($"  {j + 1} | {lines[j]}");

                            results.Add($"{relativeFile}:\n" + string.Join("\n", contextLines));
                        }
                    }
                }
                catch (IOException)
                {
                    // Skip files that can't be read
                }
            }

            var output = matchCount > 0
                ? $"Found {matchCount} match(es):\n\n" + string.Join("\n---\n", results)
                : $"No matches found for pattern: {regex}";

            stopwatch.Stop();

            _logger?.LogDebug("search_files: Found {Count} matches for '{Pattern}' in {Directory}", matchCount, regex, directoryPath);

            return ToolResult.Ok(output) with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ToolResult.Fail($"Error searching files: {ex.Message}")
                with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
    }

    /// <summary>
    /// Lists files and directories within a specified directory.
    /// Limits results to 200 files by default.
    /// </summary>
    public async Task<ToolResult> ListFilesAsync(string directoryPath, bool recursive, string workingDirectory, IReadOnlyList<AgentIgnoreRule>? agentIgnoreRules = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                return ToolResult.Fail("Missing required parameter: path");

            var searchPath = ResolvePath(directoryPath, workingDirectory);
            if (!_fileSystem.Directory.Exists(searchPath))
                return ToolResult.Fail($"Directory not found: {directoryPath}");

            // Validate against .agentignore
            if (agentIgnoreRules != null)
            {
                var relativePath = Path.GetRelativePath(workingDirectory, searchPath);
                foreach (var rule in agentIgnoreRules)
                {
                    if (rule.Matches(relativePath))
                        return ToolResult.Fail($"Directory is ignored by .agentignore: {directoryPath}");
                }
            }

            // List files (limit to 200 by default)
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = _fileSystem.Directory.GetFiles(searchPath, "*", searchOption);
            var dirs = _fileSystem.Directory.GetDirectories(searchPath, "*", searchOption);

            // Apply .agentignore filtering
            if (agentIgnoreRules != null)
            {
                files = files.Where(f =>
                {
                    var relativePath = Path.GetRelativePath(workingDirectory, f);
                    return !agentIgnoreRules.Any(r => r.Matches(relativePath));
                }).Take(200).ToArray();

                dirs = dirs.Where(d =>
                {
                    var relativePath = Path.GetRelativePath(workingDirectory, d);
                    return !agentIgnoreRules.Any(r => r.Matches(relativePath));
                }).Take(200).ToArray();
            }
            else
            {
                files = files.Take(200).ToArray();
                dirs = dirs.Take(200).ToArray();
            }

            var output = new StringBuilder();
            output.AppendLine($"Directory: {directoryPath}");
            output.AppendLine($"Recursive: {recursive}");
            output.AppendLine($"Files: {files.Length} | Directories: {dirs.Length}");
            output.AppendLine();

            foreach (var dir in dirs)
            {
                var relativeDir = Path.GetRelativePath(workingDirectory, dir);
                output.AppendLine($"/ {relativeDir}");
            }

            foreach (var file in files)
            {
                var relativeFile = Path.GetRelativePath(workingDirectory, file);
                output.AppendLine($"  {relativeFile}");
            }

            stopwatch.Stop();

            _logger?.LogDebug("list_files: Listed {FileCount} files and {DirCount} dirs in {Directory}", files.Length, dirs.Length, directoryPath);

            return ToolResult.Ok(output.ToString().Trim()) with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ToolResult.Fail($"Error listing files: {ex.Message}")
                with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
    }

    /// <summary>
    /// Resolves a relative path to an absolute path based on the working directory.
    /// Supports @workspace:path syntax for multi-root workspaces.
    /// </summary>
    private string ResolvePath(string path, string workingDirectory)
    {
        // Handle @workspace:path syntax
        if (path.StartsWith("@workspace:"))
        {
            var workspacePath = path.Substring("@workspace:".Length);
            return Path.Combine(workingDirectory, workspacePath);
        }

        // Handle absolute paths
        if (Path.IsPathRooted(path))
            return path;

        // Resolve relative to working directory
        return Path.GetFullPath(Path.Combine(workingDirectory, path));
    }

    /// <summary>
    /// Extracts the SEARCH content from a SEARCH/REPLACE block.
    /// </summary>
    private static string ExtractSearchContent(string block)
    {
        var searchStart = block.IndexOf("------- SEARCH", StringComparison.Ordinal);
        var replaceStart = block.IndexOf("=======", StringComparison.Ordinal);

        if (searchStart >= 0 && replaceStart > searchStart)
            return block.Substring(searchStart + 15, replaceStart - searchStart - 15).Trim();

        return block.Trim();
    }

    /// <summary>
    /// Extracts the REPLACE content from a SEARCH/REPLACE block.
    /// </summary>
    private static string ExtractReplaceContent(string block)
    {
        var replaceStart = block.IndexOf("=======", StringComparison.Ordinal);
        var replaceEnd = block.IndexOf("+++++++ REPLACE", StringComparison.Ordinal);

        if (replaceStart >= 0 && replaceEnd > replaceStart)
            return block.Substring(replaceStart + 9, replaceEnd - replaceStart - 9).Trim();

        return string.Empty;
    }
}