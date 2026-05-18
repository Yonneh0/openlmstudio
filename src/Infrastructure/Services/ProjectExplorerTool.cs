using System;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

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
    public async Task<bool> ExecuteAsync(System.Collections.Generic.Dictionary<string, object> parameters)
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
    public System.Collections.Generic.Dictionary<string, ToolParameterSchema> GetParameterSchema() => new Dictionary<string, ToolParameterSchema>
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