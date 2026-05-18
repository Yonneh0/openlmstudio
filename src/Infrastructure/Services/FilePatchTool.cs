using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

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
            var pathValue = TryGetString(parameters, "FilePath");
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

    private static string? TryGetString(Dictionary<string, object> dict, string key) =>
        dict.TryGetValue(key, out var value) ? Convert.ToString(value) : null;
}