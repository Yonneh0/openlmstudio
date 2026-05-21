using System;
using System.Collections.Generic;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Persisted window state for the main application window.
/// </summary>
public class WindowStateSettings
{
    /// <summary>Window width at last close.</summary>
    public double Width { get; set; } = 1400;

    /// <summary>Window height at last close.</summary>
    public double Height { get; set; } = 750;

    /// <summary>Window X position at last close (screen coordinates).</summary>
    public double Left { get; set; } = 100;

    /// <summary>Window Y position at last close (screen coordinates).</summary>
    public double Top { get; set; } = 100;

    /// <summary>Active tab name at last close.</summary>
    public string ActiveTab { get; set; } = "Chat";

    /// <summary>Left sidebar width at last close.</summary>
    public double LeftSidebarWidth { get; set; } = 280;

    /// <summary>Right sidebar width at last close.</summary>
    public double RightSidebarWidth { get; set; } = 320;

    /// <summary>Selected chat ID at last close.</summary>
    public Guid? SelectedChatId { get; set; }
}

/// <summary>
/// Interface for persisting and restoring window state.
/// </summary>
public interface IWindowSettings
{
    /// <summary>
    /// Saves the current window state to disk.
    /// </summary>
    Task SaveAsync(WindowStateSettings settings);

    /// <summary>
    /// Loads the previously saved window state, or returns default if none exists.
    /// </summary>
    Task<WindowStateSettings> LoadAsync();
}