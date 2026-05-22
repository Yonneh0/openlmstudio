using System;
using System.Collections.Generic;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Defines keyboard shortcut mappings and actions.
/// </summary>
public interface IKeyboardShortcuts
{
    /// <summary>
    /// Registers a keyboard shortcut for a given key combination and action.
    /// </summary>
    void RegisterShortcut(KeyCombination combination, string action);

    /// <summary>
    /// Unregisters a keyboard shortcut.
    /// </summary>
    void UnregisterShortcut(string action);

    /// <summary>
    /// Gets all registered shortcuts.
    /// </summary>
    IReadOnlyDictionary<string, KeyCombination> GetShortcuts();

    /// <summary>
    /// Gets the default keyboard shortcut configuration.
    /// Keys are action names, values are string representations suitable for serialization.
    /// </summary>
    static IReadOnlyDictionary<string, KeyCombination> DefaultShortcuts => new Dictionary<string, KeyCombination>
    {
        { "NewChat", new KeyCombination("N", KeyModifier.Control | KeyModifier.Shift) },
        { "Send", new KeyCombination("Enter", KeyModifier.Control) },
        { "SendNewLine", new KeyCombination("Enter", KeyModifier.None) },
        { "StopAgent", new KeyCombination("Escape", KeyModifier.Control) },
        { "ToggleServer", new KeyCombination("F5", KeyModifier.None) },
        { "ToggleDarkMode", new KeyCombination("F11", KeyModifier.None) },
        { "FocusInput", new KeyCombination("G", KeyModifier.Control) },
        { "SearchChats", new KeyCombination("F", KeyModifier.Control) },
        { "ToggleSidebar", new KeyCombination("B", KeyModifier.Control) },
        { "NewTask", new KeyCombination("M", KeyModifier.Control) },
        { "AgentStart", new KeyCombination("K", KeyModifier.Control) },
        { "AgentStop", new KeyCombination("L", KeyModifier.Control) },
    };
}

/// <summary>
/// Represents a keyboard shortcut with key name and modifier.
/// Uses string key names (e.g. "N", "Escape", "F5") for cross-layer compatibility.
/// The Desktop layer converts these to Avalonia.Input.Key values.
/// </summary>
public record KeyCombination(string KeyName, KeyModifier Modifiers);

/// <summary>
/// Represents modifier keys.
/// </summary>
[Flags]
public enum KeyModifier
{
    None = 0,
    Control = 1,
    Shift = 2,
    Alt = 4,
}