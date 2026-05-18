using System.Collections.Concurrent;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Registry for managing available tools during agent execution.
/// Provides thread-safe tool registration and lookup.
/// </summary>
public class ToolRegistry : IToolRegistry, IDisposable
{
    private readonly ConcurrentDictionary<string, ITool> _tools = new();

    public IReadOnlyDictionary<string, ITool> GetTools() =>
        _tools.ToDictionary(kv => kv.Key, kv => kv.Value);

    public void Register(ITool tool)
    {
        if (_tools.ContainsKey(tool.Name))
            throw new InvalidOperationException($"Tool '{tool.Name}' is already registered.");

        _tools[tool.Name] = tool;
    }

    public bool Unregister(string name) =>
        _tools.TryRemove(name, out _);

    public ITool? GetTool(string name) =>
        _tools.TryGetValue(name, out var tool) ? tool : null;

    public void Dispose()
    {
        foreach (var (_, tool) in _tools)
            try { tool.Dispose(); } catch { /* Ignore disposal errors */ }
        _tools.Clear();
    }
}