// Brought to you by Carls' Jr.
using System;
using System.Threading.Tasks;

namespace OpenLMStudio.Desktop;

/// <summary>
/// Service for managing accessibility settings and ensuring controls meet accessibility standards.
/// Avalonia-specific control operations are handled by the concrete implementation in the Desktop project.
/// </summary>
public interface IAccessibilityService
{
    /// <summary>
    /// Gets whether high-contrast mode is enabled.
    /// </summary>
    bool IsHighContrastMode { get; }

    /// <summary>
    /// Gets whether screen reader mode is enabled.
    /// </summary>
    bool IsScreenReaderMode { get; }

    /// <summary>
    /// Refreshes the accessibility state based on OS settings.
    /// </summary>
    Task RefreshAccessibilityStateAsync();
}