using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

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
            var pathValue = TryGetString(parameters, "FilePath");
            if (string.IsNullOrEmpty(pathValue))
            {
                _logger?.LogWarning("FileWrite tool called without FilePath parameter.");
                return false;
            }

            var contentValue = TryGetString(parameters, "Content");
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

    private static string? TryGetString(Dictionary<string, object> dict, string key) =>
        dict.TryGetValue(key, out var value) ? Convert.ToString(value) : null;
}