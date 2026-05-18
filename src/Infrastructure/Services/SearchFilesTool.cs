using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

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
            var patternValue = TryGetString(parameters, "Pattern");
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

    private static string? TryGetString(Dictionary<string, object> dict, string key) =>
        dict.TryGetValue(key, out var value) ? Convert.ToString(value) : null;
}