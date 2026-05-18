using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

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
            var pathValue = TryGetString(parameters, "FilePath");
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

    private static string? TryGetString(Dictionary<string, object> dict, string key) =>
        dict.TryGetValue(key, out var value) ? Convert.ToString(value) : null;
}