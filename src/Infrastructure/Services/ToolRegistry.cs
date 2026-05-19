using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Central registry for managing tool discovery and instantiation.
/// Provides tool lookup by name and discovers tools from DI container.
/// </summary>
public class ToolRegistry : IToolRegistry, IDisposable
{
    private readonly ILogger<ToolRegistry>? _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public ToolRegistry(ILogger<ToolRegistry>? logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public IReadOnlyDictionary<string, ITool> GetTools() => _tools;

    public void Register(ITool tool)
    {
        if (_disposed) return;
        _tools[tool.Name] = tool;
        _logger?.LogInformation("Registered tool: {Tool}", tool.Name);
    }

    public bool Unregister(string name)
    {
        if (_tools.TryRemove(name, out var tool))
        {
            try { tool.Dispose(); } catch { /* Ignore dispose errors */ }
            return true;
        }
        return false;
    }

    public ITool? GetTool(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (_tools.TryGetValue(name, out var tool))
            return tool;

        // Lazy-load from DI
        var type = Type.GetType(name);
        if (type != null && typeof(ITool).IsAssignableFrom(type) && !type.IsAbstract)
        {
            try
            {
                var toolInstance = (ITool)Activator.CreateInstance(type)!;
                Register(toolInstance);
                return toolInstance;
            }
            catch { return null; }
        }

        return null;
    }

    public async Task<bool> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters, CancellationToken ct = default)
    {
        var tool = GetTool(toolName);
        if (tool == null)
        {
            _logger?.LogWarning("Tool not found: {ToolName}", toolName);
            return false;
        }

        try
        {
            return await tool.ExecuteAsync(parameters).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Tool execution failed: {ToolName}", toolName);
            return false;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var tool in _tools.Values)
            {
                try { tool.Dispose(); } catch { /* Ignore dispose errors */ }
            }
            _disposed = true;
        }
    }
}
