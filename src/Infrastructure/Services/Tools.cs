using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Shared helpers for tool implementations.
/// </summary>
internal static class ToolHelpers
{
    public static string? TryGetString(Dictionary<string, object> dict, string key) =>
        dict.TryGetValue(key, out var value) ? Convert.ToString(value) : null;

    public static int? TryGetInt(Dictionary<string, object> dict, string key)
    {
        if (!dict.TryGetValue(key, out var value)) return null;
        return value switch
        {
            int i => i,
            long l => Convert.ToInt32(l),
            _ => null
        };
    }
}

// ============================================================================
// CodeDefinitionExtractorTool (~202 lines)
// ============================================================================

/// <summary>
/// Tool that extracts code definitions (classes, functions, methods) from a project directory.
/// Supports C#, Python, TypeScript/JavaScript, and other common languages.
/// </summary>
public class CodeDefinitionExtractorTool : ITool
{
    private readonly IGitRepositoryService _gitService;

    public CodeDefinitionExtractorTool(IGitRepositoryService gitService)
    {
        _gitService = gitService;
    }

    public string Name => "code_definitions";

    public string Description => "Extracts code definitions (classes, functions, methods) from source files in a project directory.";

    public async Task<bool> ExecuteAsync(Dictionary<string, object> args)
    {
        var repoPath = args["repo_path"] as string ?? ".";
        var maxDepth = args["max_depth"] is int d && d > 0 ? d : 4;
        var languageFilter = args["language"] as string;

        var definitions = ExtractDefinitions(repoPath, maxDepth, languageFilter);

        System.Console.WriteLine($"## Code Definitions (language: {languageFilter ?? "all"}, max_depth: {maxDepth}):");
        foreach (var def in definitions)
        {
            System.Console.WriteLine($"  {def}");
        }

        return true;
    }

    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new()
    {
        ["repo_path"] = new ToolParameterSchema("string", false),
        ["max_depth"] = new ToolParameterSchema("number", false),
        ["language"] = new ToolParameterSchema("string", false)
    };

    public void Dispose() { }

    private static List<string> ExtractDefinitions(string path, int maxDepth, string? languageFilter)
    {
        var definitions = new List<string>();
        var exts = languageFilter?.ToLowerInvariant() switch
        {
            "csharp" or "cs" => new[] { ".cs" },
            "python" or "py" => new[] { ".py" },
            "typescript" or "ts" => new[] { ".ts", ".tsx" },
            "javascript" or "js" => new[] { ".js", ".jsx" },
            _ => new[] { ".cs", ".py", ".ts", ".tsx", ".js", ".jsx", ".java", ".go", ".rb", ".rs" }
        };

        foreach (var ext in exts)
        {
            var pattern = $"*{ext}";
            foreach (var file in Directory.GetFiles(path, pattern, SearchOption.AllDirectories)
                .Where(f => f.Replace('\\', '/').Length - path.Replace('\\', '/').Length <= maxDepth * 3))
            {
                try
                {
                    var relativePath = Path.GetRelativePath(path, file);
                    var content = File.ReadAllText(file);

                    if (ext == ".cs")
                    {
                        ExtractCSharpDefinitions(content, relativePath, definitions);
                    }
                    else if (ext == ".py")
                    {
                        ExtractPythonDefinitions(content, relativePath, definitions);
                    }
                    else if (ext is ".ts" or ".tsx" or ".js" or ".jsx")
                    {
                        ExtractJavaScriptDefinitions(content, relativePath, definitions);
                    }
                }
                catch
                {
                    // Skip files we can't read
                }
            }
        }

        return definitions;
    }

    private static void ExtractCSharpDefinitions(string content, string filePath, List<string> definitions)
    {
        var lines = content.Split('\n');
        var inClass = false;
        var className = "";

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (line.StartsWith("public class") || line.StartsWith("internal class") ||
                line.StartsWith("private class") || line.StartsWith("sealed class") ||
                line.StartsWith("abstract class"))
            {
                inClass = true;
                className = line.Split(' ', 2).LastOrDefault()?.TrimEnd('{', ',') ?? "Unknown";
                definitions.Add($"[{filePath}] class {className} (line {i + 1})");
            }
            else if (inClass && (line.StartsWith("public ") || line.StartsWith("public async ")))
            {
                var methodMatch = Regex.Match(line, @"(public|private|protected|internal)\s+(async\s+)?(Task|void|\w+)\s+(\w+)\s*\([^)]*\)");
                if (methodMatch.Success)
                {
                    var methodName = methodMatch.Groups[4].Value;
                    definitions.Add($"  [{filePath}] {methodName}() (line {i + 1})");
                }
            }
            else if (inClass && (line.StartsWith("private ") || line.StartsWith("protected ") || line.StartsWith("internal ")))
            {
                var methodMatch = Regex.Match(line, @"(public|private|protected|internal)\s+(async\s+)?(Task|void|\w+)\s+(\w+)\s*\([^)]*\)");
                if (methodMatch.Success)
                {
                    var methodName = methodMatch.Groups[4].Value;
                    definitions.Add($"    [{filePath}] {methodName}() (line {i + 1})");
                }
            }
            else if (line.Contains("interface") && line.Contains("class"))
            {
                definitions.Add($"[{filePath}] interface {className}");
            }

            if (line.StartsWith("public ") && line.Contains("get") && line.Contains("set"))
            {
                var propMatch = Regex.Match(line, @"(public|private|protected)\s+\w+\s+(\w+)\s*{");
                if (propMatch.Success)
                {
                    definitions.Add($"    [{filePath}] .{propMatch.Groups[2].Value}");
                }
            }
        }
    }

    private static void ExtractPythonDefinitions(string content, string filePath, List<string> definitions)
    {
        var lines = content.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimStart();

            if (line.StartsWith("class ") && line.Split('(')[0].Contains("class"))
            {
                var className = line.Split(':')[0].Replace("class ", "").Trim();
                definitions.Add($"[{filePath}] class {className} (line {i + 1})");
            }
            else if (line.StartsWith("def ") || line.StartsWith("async def "))
            {
                var funcName = Regex.Match(line, @"def\s+(\w+)").Groups[1].Value;
                if (!string.IsNullOrEmpty(funcName) && funcName != "__init__")
                {
                    definitions.Add($"    [{filePath}] def {funcName}() (line {i + 1})");
                }
            }
        }
    }

    private static void ExtractJavaScriptDefinitions(string content, string filePath, List<string> definitions)
    {
        var lines = content.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (Regex.Match(line, @"class\s+\w+").Success)
            {
                var className = Regex.Match(line, @"class\s+(\w+)").Groups[1].Value;
                definitions.Add($"[{filePath}] class {className} (line {i + 1})");
            }
            else if (Regex.Match(line, @"(export\s+)?(async\s+)?function\s+\w+|const\s+\w+\s*=\s*(async\s+)?\(?\(?").Success)
            {
                var funcMatch = Regex.Match(line, @"(export\s+)?(async\s+)?function\s+(\w+)|const\s+(\w+)\s*=\s*(async\s+)?\(?");
                if (funcMatch.Success)
                {
                    var funcName = funcMatch.Groups[3].Value ?? funcMatch.Groups[4].Value;
                    if (!string.IsNullOrEmpty(funcName))
                    {
                        definitions.Add($"    [{filePath}] function {funcName}() (line {i + 1})");
                    }
                }
            }
        }
    }
}

// ============================================================================
// CommandExecuteTool (~93 lines)
// ============================================================================

/// <summary>
/// CommandExecute tool implementation for running shell commands within the agent sandbox.
/// </summary>
public class CommandExecuteTool : ITool, IDisposable
{
    private readonly ILogger<CommandExecuteTool>? _logger;
    private readonly ICommandExecutionService _commandExecutor;
    private bool _disposed;

    public string Name => "CommandExecute";
    public string Description => "Executes a shell command within the sandboxed environment.";

    /// <summary>
    /// Creates a new CommandExecute tool instance.
    /// </summary>
    public CommandExecuteTool(ILogger<CommandExecuteTool>? logger, ICommandExecutionService commandExecutor)
    {
        _logger = logger;
        _commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
    }

    /// <inheritdoc />
    public async Task<bool> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (parameters == null || _disposed) return false;

        try
        {
            var commandValue = ToolHelpers.TryGetString(parameters, "Command");
            if (string.IsNullOrEmpty(commandValue))
            {
                _logger?.LogWarning("CommandExecute tool called without Command parameter.");
                return false;
            }

            int timeoutSeconds = 120; // Default timeout
            if (parameters.TryGetValue("Timeout", out var timeoutValue) && timeoutValue is long t)
                timeoutSeconds = Convert.ToInt32(t);

            Dictionary<string, string>? envVars = null;
            if (parameters.TryGetValue("EnvironmentVariables", out var envVarValue))
            {
                // Parse environment variables from a dictionary parameter
                envVars = new();
                if (envVarValue is IEnumerable<KeyValuePair<string, object>> pairs)
                {
                    foreach (var pair in pairs)
                        envVars.Add(pair.Key, Convert.ToString(pair.Value) ?? string.Empty);
                }
            }

            // Execute through the command executor with sandbox isolation
            var request = new CommandExecuteRequest(commandValue, envVars, timeoutSeconds);
            await _commandExecutor.ExecuteAsync(request);

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "CommandExecute tool failed to execute command.");
            return false;
        }
    }

    /// <inheritdoc />
    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new Dictionary<string, ToolParameterSchema>
    {
        ["Command"] = new ToolParameterSchema("string", true),  // Required
        ["EnvironmentVariables"] = new ToolParameterSchema("object[]", false),  // Optional — dictionary of key-value pairs
        ["Timeout"] = new ToolParameterSchema("number", false)  // Optional — timeout in seconds (default: 120)
    };

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _commandExecutor.Dispose();
        }
    }
}

// ============================================================================
// FilePatchTool (~92 lines)
// ============================================================================

/// <summary>
/// FilePatch tool implementation for safely patching files within the agent sandbox.
/// </summary>
public class FilePatchTool : ITool, IDisposable
{
    private readonly ILogger<FilePatchTool>? _logger;
    private readonly IFileOperationsService _fileOperations;
    private bool _disposed;

    public string Name => "FilePatch";
    public string Description => "Applies a safe patch (add/remove lines) to an existing file at the specified path.";

    /// <summary>
    /// Creates a new FilePatch tool instance.
    /// </summary>
    public FilePatchTool(ILogger<FilePatchTool>? logger, IFileOperationsService fileOperations)
    {
        _logger = logger;
        _fileOperations = fileOperations ?? throw new ArgumentNullException(nameof(fileOperations));
    }

    /// <inheritdoc />
    public async Task<bool> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (parameters == null || _disposed) return false;

        try
        {
            var pathValue = ToolHelpers.TryGetString(parameters, "FilePath");
            if (string.IsNullOrEmpty(pathValue))
            {
                _logger?.LogWarning("FilePatch tool called without FilePath parameter.");
                return false;
            }

            // Parse LinesToAdd and LinesToRemove from the parameters dictionary
            List<string> linesToAdd = new();
            if (parameters.TryGetValue("LinesToAdd", out var addValue) && addValue is IEnumerable<object> addList)
            {
                foreach (var item in addList)
                    linesToAdd.Add(Convert.ToString(item) ?? string.Empty);
            }

            List<int> linesToRemove = new();
            if (parameters.TryGetValue("LinesToRemove", out var removeValue) && removeValue is IEnumerable<long> removeList)
            {
                foreach (var val in removeList)
                    linesToRemove.Add((int)val);
            }

            // Execute through the file operations service with sandbox isolation
            var request = new FilePatchRequest(pathValue, linesToAdd, linesToRemove);
            await _fileOperations.PatchFileAsync(request);

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "FilePatch tool failed to patch file.");
            return false;
        }
    }

    /// <inheritdoc />
    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new Dictionary<string, ToolParameterSchema>
    {
        ["FilePath"] = new ToolParameterSchema("string", true),   // Required
        ["LinesToAdd"] = new ToolParameterSchema("string[]", false),  // Optional
        ["LinesToRemove"] = new ToolParameterSchema("number[]", false)  // Optional
    };

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

// ============================================================================
// FileReadTool (~82 lines)
// ============================================================================

/// <summary>
/// FileRead tool implementation for reading file contents safely within the agent sandbox.
/// </summary>
public class FileReadTool : ITool, IDisposable
{
    private readonly ILogger<FileReadTool>? _logger;
    private readonly IFileOperationsService _fileOperations;
    private bool _disposed;

    public string Name => "FileRead";
    public string Description => "Reads the contents of a file at the specified path. Optionally limits to first N lines.";

    /// <summary>
    /// Creates a new FileRead tool instance.
    /// </summary>
    public FileReadTool(ILogger<FileReadTool>? logger, IFileOperationsService fileOperations)
    {
        _logger = logger;
        _fileOperations = fileOperations ?? throw new ArgumentNullException(nameof(fileOperations));
    }

    /// <inheritdoc />
    public async Task<bool> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (parameters == null || _disposed) return false;

        try
        {
            var pathValue = ToolHelpers.TryGetString(parameters, "FilePath");
            if (string.IsNullOrEmpty(pathValue))
            {
                _logger?.LogWarning("FileRead tool called without FilePath parameter.");
                return false;
            }

            int? maxLines = null;
            if (parameters.TryGetValue("MaxLines", out var maxLinesValue) && maxLinesValue is long ml)
                maxLines = Convert.ToInt32(ml);

            // Execute through the file operations service with sandbox isolation
            var request = new FileReadRequest(pathValue, maxLines);
            await _fileOperations.ReadFileAsync(request);

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "FileRead tool failed to read file.");
            return false;
        }
    }

    /// <inheritdoc />
    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new Dictionary<string, ToolParameterSchema>
    {
        ["FilePath"] = new ToolParameterSchema("string", true),  // Required
        ["MaxLines"] = new ToolParameterSchema("number", false)  // Optional
    };

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            // No unmanaged resources to dispose — the file operations service is managed elsewhere.
        }
    }
}

// ============================================================================
// FileWriteTool (~88 lines)
// ============================================================================

/// <summary>
/// FileWrite tool implementation for writing/creating files within the agent sandbox.
/// </summary>
public class FileWriteTool : ITool, IDisposable
{
    private readonly ILogger<FileWriteTool>? _logger;
    private readonly IFileOperationsService _fileOperations;
    private bool _disposed;

    public string Name => "FileWrite";
    public string Description => "Writes or overwrites the contents of a file at the specified path.";

    /// <summary>
    /// Creates a new FileWrite tool instance.
    /// </summary>
    public FileWriteTool(ILogger<FileWriteTool>? logger, IFileOperationsService fileOperations)
    {
        _logger = logger;
        _fileOperations = fileOperations ?? throw new ArgumentNullException(nameof(fileOperations));
    }

    /// <inheritdoc />
    public async Task<bool> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (parameters == null || _disposed) return false;

        try
        {
            var pathValue = ToolHelpers.TryGetString(parameters, "FilePath");
            if (string.IsNullOrEmpty(pathValue))
            {
                _logger?.LogWarning("FileWrite tool called without FilePath parameter.");
                return false;
            }

            var contentValue = ToolHelpers.TryGetString(parameters, "Content");
            if (string.IsNullOrEmpty(contentValue))
            {
                _logger?.LogWarning("FileWrite tool called with empty Content parameter.");
                return false;
            }

            bool append = false;
            if (parameters.TryGetValue("Append", out var appendValue) && appendValue is bool bApp)
                append = bApp;

            // Execute through the file operations service with sandbox isolation
            var request = new FileWriteRequest(pathValue, contentValue, append);
            await _fileOperations.WriteFileAsync(request);

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "FileWrite tool failed to write file.");
            return false;
        }
    }

    /// <inheritdoc />
    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new Dictionary<string, ToolParameterSchema>
    {
        ["FilePath"] = new ToolParameterSchema("string", true),  // Required
        ["Content"] = new ToolParameterSchema("string", true),   // Required
        ["Append"] = new ToolParameterSchema("boolean", false)   // Optional
    };

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

// ============================================================================
// GitDiffTool (~46 lines)
// ============================================================================

/// <summary>
/// Tool that shows differences between two Git refs using unified diff format.
/// </summary>
public class GitDiffTool : ITool
{
    private readonly IGitRepositoryService _gitService;

    public GitDiffTool(IGitRepositoryService gitService)
    {
        _gitService = gitService;
    }

    public string Name => "git_diff";

    public string Description => "Shows differences between two Git refs (commits, branches, tags, or working directory).";

    public async Task<bool> ExecuteAsync(Dictionary<string, object> args)
    {
        var repoPath = args["repo_path"] as string ?? ".";
        var oldRef = args["old_ref"] as string ?? "HEAD";
        var newRef = args["new_ref"] as string ?? "HEAD";

        var diffInfo = await _gitService.GetDiffBetweenRefsAsync(oldRef, newRef, includeUnifiedDiff: true);
        var result = diffInfo is not null
            ? $"## Git Diff: {oldRef} → {newRef}\n{diffInfo}\nFiles changed: {diffInfo.Files?.Count ?? 0}\n" + (diffInfo.UnifiedDiff ?? "(no unified diff)")
            : "(No diff available)";

        System.Console.WriteLine(result);
        return true;
    }

    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new()
    {
        ["repo_path"] = new ToolParameterSchema("string", false),
        ["old_ref"] = new ToolParameterSchema("string", false),
        ["new_ref"] = new ToolParameterSchema("string", true)
    };

    public void Dispose() { }
}

// ============================================================================
// GitHistoryTool, GitBlameTool, GitBranchesTool (~138 lines)
// ============================================================================

/// <summary>
/// Tool that lists recent git commits with metadata.
/// </summary>
public class GitHistoryTool : ITool
{
    private readonly IGitRepositoryService _gitService;

    public GitHistoryTool(IGitRepositoryService gitService)
    {
        _gitService = gitService;
    }

    public string Name => "git_history";

    public string Description => "Lists recent git commits with author, date, and message. Supports filtering by path.";

    public async Task<bool> ExecuteAsync(Dictionary<string, object> args)
    {
        var repoPath = args["repo_path"] as string ?? ".";
        var maxCount = args["max_count"] is int n && n > 0 ? n : 30;
        var pathFilter = args["path_filter"] as string;

        var commits = await _gitService.ListCommitsAsync(maxCount, pathFilter);

        System.Console.WriteLine($"## Git History (showing {commits.Count} commits):");
        foreach (var c in commits)
        {
            System.Console.WriteLine($"- {c.ShortSha} | {c.AuthorName} | {c.CommittedAt:yyyy-MM-dd HH:mm} | {c.Message}");
        }

        return true;
    }

    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new()
    {
        ["repo_path"] = new ToolParameterSchema("string", false),
        ["max_count"] = new ToolParameterSchema("number", false),
        ["path_filter"] = new ToolParameterSchema("string", false)
    };

    public void Dispose() { }
}

/// <summary>
/// Tool that shows blame annotation for a file — who wrote each line.
/// </summary>
public class GitBlameTool : ITool
{
    private readonly IGitRepositoryService _gitService;

    public GitBlameTool(IGitRepositoryService gitService)
    {
        _gitService = gitService;
    }

    public string Name => "git_blame";

    public string Description => "Shows blame annotation for a file — line-by-line attribution of who wrote each line.";

    public async Task<bool> ExecuteAsync(Dictionary<string, object> args)
    {
        var repoPath = args["repo_path"] as string ?? ".";
        var filePath = args["file_path"] as string ?? throw new ArgumentException("file_path is required.");

        var blame = await _gitService.GetBlameForFileAsync(filePath);

        System.Console.WriteLine($"## Git Blame: {filePath}");
        foreach (var kvp in blame)
        {
            var author = kvp.Value ?? "(unknown)";
            System.Console.WriteLine($"{kvp.Key}: {author}");
        }

        return true;
    }

    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new()
    {
        ["repo_path"] = new ToolParameterSchema("string", false),
        ["file_path"] = new ToolParameterSchema("string", true)
    };

    public void Dispose() { }
}

/// <summary>
/// Tool that lists git branches, tags, and remotes.
/// </summary>
public class GitBranchesTool : ITool
{
    private readonly IGitRepositoryService _gitService;

    public GitBranchesTool(IGitRepositoryService gitService)
    {
        _gitService = gitService;
    }

    public string Name => "git_branches";

    public string Description => "Lists git branches, tags, and remotes in the repository.";

    public async Task<bool> ExecuteAsync(Dictionary<string, object> args)
    {
        var repoPath = args["repo_path"] as string ?? ".";

        var branches = await _gitService.ListBranchesAsync();
        var tags = await _gitService.ListTagsAsync();
        var remotes = await _gitService.ListRemotesAsync();

        System.Console.WriteLine("## Branches:");
        foreach (var b in branches)
            System.Console.WriteLine($"  {b.ShortName} {(b.IsCurrentBranch ? "(current)" : "")} [{b.TipSha?.Substring(0, 7)}]");

        System.Console.WriteLine("## Tags:");
        foreach (var t in tags)
            System.Console.WriteLine($"  {t.ShortName} [{t.TargetSha?.Substring(0, 7)}]");

        System.Console.WriteLine("## Remotes:");
        foreach (var r in remotes)
            System.Console.WriteLine($"  {r.Name} -> {r.Url}");

        return true;
    }

    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new()
    {
        ["repo_path"] = new ToolParameterSchema("string", false)
    };

    public void Dispose() { }
}

// ============================================================================
// ProjectExplorerTool (~73 lines)
// ============================================================================

/// <summary>
/// ProjectExplorer tool implementation for listing directory contents recursively within the agent sandbox.
/// </summary>
public class ProjectExplorerTool : ITool, IDisposable
{
    private readonly ILogger<ProjectExplorerTool>? _logger;
    private readonly IFileOperationsService _fileOperations;
    private bool _disposed;

    public string Name => "ProjectExplorer";
    public string Description => "Lists directory contents recursively as a tree structure.";

    /// <summary>
    /// Creates a new ProjectExplorer tool instance.
    /// </summary>
    public ProjectExplorerTool(ILogger<ProjectExplorerTool>? logger, IFileOperationsService fileOperations)
    {
        _logger = logger;
        _fileOperations = fileOperations ?? throw new ArgumentNullException(nameof(fileOperations));
    }

    /// <inheritdoc />
    public async Task<bool> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (parameters == null || _disposed) return false;

        try
        {
            string? rootPath = null;
            if (parameters.TryGetValue("RootPath", out var rootPathValue) && rootPathValue is string rp)
                rootPath = rp;

            bool includeHiddenFiles = false;
            if (parameters.TryGetValue("IncludeHiddenFiles", out var hiddenValue) && hiddenValue is bool h)
                includeHiddenFiles = h;

            // Execute through the file operations service with sandbox isolation
            var request = new ProjectExplorerRequest(rootPath, includeHiddenFiles);
            await _fileOperations.ExploreProjectAsync(request);

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ProjectExplorer tool failed to explore project.");
            return false;
        }
    }

    /// <inheritdoc />
    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new Dictionary<string, ToolParameterSchema>
    {
        ["RootPath"] = new ToolParameterSchema("string", false),   // Optional — override the project root path
        ["IncludeHiddenFiles"] = new ToolParameterSchema("boolean", false)  // Optional — include hidden files (default: false)
    };

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

// ============================================================================
// SearchFilesTool (~85 lines)
// ============================================================================

/// <summary>
/// SearchFiles tool implementation for regex search across project files within the agent sandbox.
/// </summary>
public class SearchFilesTool : ITool, IDisposable
{
    private readonly ILogger<SearchFilesTool>? _logger;
    private readonly IFileOperationsService _fileOperations;
    private bool _disposed;

    public string Name => "SearchFiles";
    public string Description => "Performs a regex search across all files in the project root.";

    /// <summary>
    /// Creates a new SearchFiles tool instance.
    /// </summary>
    public SearchFilesTool(ILogger<SearchFilesTool>? logger, IFileOperationsService fileOperations)
    {
        _logger = logger;
        _fileOperations = fileOperations ?? throw new ArgumentNullException(nameof(fileOperations));
    }

    /// <inheritdoc />
    public async Task<bool> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (parameters == null || _disposed) return false;

        try
        {
            var patternValue = ToolHelpers.TryGetString(parameters, "Pattern");
            if (string.IsNullOrEmpty(patternValue))
            {
                _logger?.LogWarning("SearchFiles tool called without Pattern parameter.");
                return false;
            }

            string? rootPath = null;
            if (parameters.TryGetValue("RootPath", out var rootPathValue) && rootPathValue is string rp)
                rootPath = rp;

            int maxResults = 100; // Default max results
            if (parameters.TryGetValue("MaxResults", out var maxResultsValue) && maxResultsValue is long mr)
                maxResults = Convert.ToInt32(mr);

            // Execute through the file operations service with sandbox isolation
            var request = new SearchFilesRequest(patternValue, rootPath, maxResults);
            await _fileOperations.SearchFilesAsync(request);

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SearchFiles tool failed to search files.");
            return false;
        }
    }

    /// <inheritdoc />
    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new Dictionary<string, ToolParameterSchema>
    {
        ["Pattern"] = new ToolParameterSchema("string", true),   // Required — regex pattern to search for
        ["RootPath"] = new ToolParameterSchema("string", false),  // Optional — override the project root path
        ["MaxResults"] = new ToolParameterSchema("number", false)  // Optional — maximum number of matches (default: 100)
    };

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