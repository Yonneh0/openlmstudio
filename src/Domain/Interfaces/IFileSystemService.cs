namespace OpenLMStudio.Domain.Interfaces;

using Models;

/// <summary>
/// File system service for agent tool operations.
/// Supports cross-platform file operations with .agentignore validation.
/// </summary>
public interface IFileSystemService
{
    /// <summary>
    /// Creates a new file or overwrites an existing file.
    /// Automatically creates directories if they don't exist.
    /// </summary>
    /// <param name="path">Relative path to the file (e.g., "src/index.ts").</param>
    /// <param name="content">The complete content to write.</param>
    /// <param name="workingDirectory">Base working directory for path resolution.</param>
    /// <param name="agentIgnoreRules">Rules from .agentignore for validation.</param>
    /// <returns>Tool result with output and modified files.</returns>
    Task<ToolResult> WriteFileAsync(string path, string content, string workingDirectory, IReadOnlyList<AgentIgnoreRule>? agentIgnoreRules = null);

    /// <summary>
    /// Makes targeted edits to specific parts of an existing file using SEARCH/REPLACE blocks.
    /// </summary>
    /// <param name="path">Relative path to the file.</param>
    /// <param name="searchReplaceBlocks">One or more SEARCH/REPLACE blocks.</param>
    /// <param name="workingDirectory">Base working directory for path resolution.</param>
    /// <param name="agentIgnoreRules">Rules from .agentignore for validation.</param>
    /// <returns>Tool result with output and modified files.</returns>
    Task<ToolResult> ReplaceInFileAsync(string path, string[] searchReplaceBlocks, string workingDirectory, IReadOnlyList<AgentIgnoreRule>? agentIgnoreRules = null);

    /// <summary>
    /// Reads the contents of a file with line numbers.
    /// </summary>
    /// <param name="path">Relative path to the file.</param>
    /// <param name="startLine">1-based line number to start reading from (default: 1).</param>
    /// <param name="endLine">1-based line number to stop reading at (default: startLine + 1000).</param>
    /// <param name="workingDirectory">Base working directory for path resolution.</param>
    /// <param name="agentIgnoreRules">Rules from .agentignore for validation.</param>
    /// <returns>Tool result with file content and line numbers.</returns>
    Task<ToolResult> ReadFileAsync(string path, int startLine, int endLine, string workingDirectory, IReadOnlyList<AgentIgnoreRule>? agentIgnoreRules = null);

    /// <summary>
    /// Performs a regex search across files in a directory.
    /// </summary>
    /// <param name="directoryPath">Directory path to search in.</param>
    /// <param name="regex">Regular expression pattern (uses Rust regex syntax).</param>
    /// <param name="filePattern">Glob pattern to filter files (e.g., "*.ts").</param>
    /// <param name="workingDirectory">Base working directory for path resolution.</param>
    /// <param name="agentIgnoreRules">Rules from .agentignore for validation.</param>
    /// <returns>Tool result with context-rich search results.</returns>
    Task<ToolResult> SearchFilesAsync(string directoryPath, string regex, string? filePattern, string workingDirectory, IReadOnlyList<AgentIgnoreRule>? agentIgnoreRules = null);

    /// <summary>
    /// Lists files and directories within a specified directory.
    /// </summary>
    /// <param name="directoryPath">Directory path to list contents for.</param>
    /// <param name="recursive">Whether to list files recursively.</param>
    /// <param name="workingDirectory">Base working directory for path resolution.</param>
    /// <param name="agentIgnoreRules">Rules from .agentignore for validation.</param>
    /// <returns>Tool result with file listing.</returns>
    Task<ToolResult> ListFilesAsync(string directoryPath, bool recursive, string workingDirectory, IReadOnlyList<AgentIgnoreRule>? agentIgnoreRules = null);
}