using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Manages keyboard shortcuts with action callbacks.
/// </summary>
public class KeyboardShortcutsService : IKeyboardShortcuts
{
    private readonly ILogger<KeyboardShortcutsService>? _logger;
    private readonly Dictionary<string, KeyCombination> _shortcuts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Action> _handlers = new(StringComparer.OrdinalIgnoreCase);

    public KeyboardShortcutsService(ILogger<KeyboardShortcutsService>? logger = null)
    {
        _logger = logger;
        foreach (var kvp in IKeyboardShortcuts.DefaultShortcuts)
        {
            _shortcuts[kvp.Key] = kvp.Value;
        }
    }

    public void RegisterShortcut(KeyCombination combination, string action)
    {
        _shortcuts[action] = combination;
        _logger?.LogDebug("Registered shortcut: {Action} -> {Key}+{Modifiers}", action, combination.KeyName, combination.Modifiers);
    }

    public void UnregisterShortcut(string action)
    {
        _shortcuts.Remove(action);
        _handlers.Remove(action);
        _logger?.LogDebug("Unregistered shortcut: {Action}", action);
    }

    public IReadOnlyDictionary<string, KeyCombination> GetShortcuts() => new Dictionary<string, KeyCombination>(_shortcuts);

    public void RegisterHandler(string action, Action handler)
    {
        _handlers[action] = handler;
    }

    public bool TryInvoke(string action)
    {
        if (_handlers.TryGetValue(action, out var handler))
        {
            handler();
            return true;
        }
        return false;
    }

    public void UnregisterHandler(string action)
    {
        _handlers.Remove(action);
    }
}