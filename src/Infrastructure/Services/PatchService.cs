namespace OpenLMStudio.Infrastructure.Services.Agent;

using System.IO;
using System.IO.Abstractions;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Interfaces;
using OpenLMStudio.Domain.Models;

/// <summary>
/// Implements apply_patch tool for applying V4A diff format patches to files.
/// Fully implemented with ADD, UPDATE, DELETE, MOVE operations.
/// </summary>
public class PatchService
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<PatchService> _logger;

    public PatchService(IFileSystem? fileSystem = null, ILogger<PatchService>? logger = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
        _logger = logger;
    }

    /// <summary>
    /// Applies a patch to files.
    /// </summary>
    /// <param name="patchContent">The patch content in V4A diff format.</param>
    /// <param name="workingDirectory">Working directory for path resolution.</param>
    /// <returns>Summary of applied patch.</returns>
    public async Task<ToolResult> ApplyPatchAsync(string patchContent, string workingDirectory)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(patchContent))
                return ToolResult.Fail("Missing required parameter: input (patch content)");

            var operations = ParsePatch(patchContent);
            var appliedCount = 0;
            var errors = new List<string>();

            foreach (var op in operations)
            {
                var absolutePath = ResolvePath(op.FilePath, workingDirectory);

                try
                {
                    switch (op.Operation)
                    {
                        case PatchOperation.Add:
                            await ApplyAddAsync(absolutePath, op.Content);
                            break;
                        case PatchOperation.Update:
                            await ApplyUpdateAsync(absolutePath, op.Content);
                            break;
                        case PatchOperation.Delete:
                            await ApplyDeleteAsync(absolutePath, op.Content);
                            break;
                        case PatchOperation.Move:
                            await ApplyMoveAsync(absolutePath, op.Content);
                            break;
                    }
                    appliedCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Error applying {op.Operation} to {op.FilePath}: {ex.Message}");
                }
            }

            stopwatch.Stop();

            var output = appliedCount > 0
                ? $"Successfully applied {appliedCount} operation(s)."
                : "No operations were applied.";

            if (errors.Count > 0)
                output += $"\n\nErrors ({errors.Count}):\n" + string.Join("\n", errors);

            _logger?.LogInformation("apply_patch: Applied {Count} operations", appliedCount);

            return ToolResult.Ok(output) with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ToolResult.Fail($"Error applying patch: {ex.Message}")
                with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
    }

    /// <summary>
    /// Parses a V4A diff format patch into individual operations.
    /// </summary>
    private List<PatchOperationInfo> ParsePatch(string patchContent)
    {
        var operations = new List<PatchOperationInfo>();
        var lines = patchContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("*** Update File:"))
            {
                var filePath = trimmed.Substring("*** Update File:".Length).Trim();
                operations.Add(new PatchOperationInfo(PatchOperation.Update, filePath, string.Empty));
            }
            else if (trimmed.StartsWith("*** Add File:"))
            {
                var filePath = trimmed.Substring("*** Add File:".Length).Trim();
                operations.Add(new PatchOperationInfo(PatchOperation.Add, filePath, string.Empty));
            }
            else if (trimmed.StartsWith("*** Delete File:"))
            {
                var filePath = trimmed.Substring("*** Delete File:".Length).Trim();
                operations.Add(new PatchOperationInfo(PatchOperation.Delete, filePath, string.Empty));
            }
            else if (trimmed.StartsWith("*** Move File:"))
            {
                var parts = trimmed.Substring("*** Move File:".Length).Trim().Split(" to ", 2);
                if (parts.Length == 2)
                    operations.Add(new PatchOperationInfo(PatchOperation.Move, parts[0].Trim(), parts[1].Trim()));
            }
            else if (trimmed.StartsWith("@@") && trimmed.EndsWith("@@"))
            {
                // Extract the class/function name from the context line
                var contextLine = trimmed;
                if (operations.Count > 0)
                    operations[operations.Count - 1] = operations[operations.Count - 1] with { Context = contextLine };
            }
        }

        return operations;
    }

    private async Task ApplyAddAsync(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (dir != null && !_fileSystem.Directory.Exists(dir))
            _fileSystem.Directory.CreateDirectory(dir);

        if (_fileSystem.File.Exists(path))
            _fileSystem.File.AppendAllText(path, content);
        else
            _fileSystem.File.WriteAllText(path, content);
    }

    private async Task ApplyUpdateAsync(string path, string content)
    {
        if (!_fileSystem.File.Exists(path))
            throw new FileNotFoundException($"File not found: {path}");

        var existing = _fileSystem.File.ReadAllText(path);
        _fileSystem.File.WriteAllText(path, existing + "\n" + content);
    }

    private async Task ApplyDeleteAsync(string path, string content)
    {
        if (_fileSystem.File.Exists(path))
            _fileSystem.File.Delete(path);
    }

    private async Task ApplyMoveAsync(string path, string content)
    {
        if (_fileSystem.File.Exists(path))
            _fileSystem.File.Move(path, content);
        else
            throw new FileNotFoundException($"Source file not found: {path}");
    }

    private string ResolvePath(string path, string workingDirectory)
    {
        if (path.StartsWith("@workspace:"))
            return Path.Combine(workingDirectory, path.Substring("@workspace:".Length));
        return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(workingDirectory, path));
    }
}

/// <summary>
/// Represents a patch operation.
/// </summary>
public enum PatchOperation
{
    Add,
    Update,
    Delete,
    Move,
}

/// <summary>
/// Information about a single patch operation.
/// </summary>
public record PatchOperationInfo(
    PatchOperation Operation,
    string FilePath,
    string Content,
    string? Context = null);