using System;
using System.Collections.Generic;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Service for accessibility features including keyboard navigation, screen reader support, and high contrast.
/// </summary>
public interface IAccessibilityService
{
    /// <summary>
    /// Gets or sets whether screen reader support is enabled.
    /// </summary>
    bool ScreenReaderEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether high contrast mode is enabled.
    /// </summary>
    bool HighContrastEnabled { get; set; }

    /// <summary>
    /// Gets or sets the preferred font size for accessibility.
    /// </summary>
    int PreferredFontSize { get; set; }

    /// <summary>
    /// Gets or sets whether keyboard navigation is enabled.
    /// </summary>
    bool KeyboardNavigationEnabled { get; set; }

    /// <summary>
    /// Gets all accessibility settings as a dictionary.
    /// </summary>
    IReadOnlyDictionary<string, object> GetSettings();

    /// <summary>
    /// Applies accessibility settings to the UI.
    /// </summary>
    void ApplySettings();
}

/// <summary>
/// Provides keyboard navigation helpers for accessibility.
/// </summary>
public interface IKeyboardNavigationService
{
    /// <summary>
    /// Navigates to the next focusable element.
    /// </summary>
    void MoveFocusForward();

    /// <summary>
    /// Navigates to the previous focusable element.
    /// </summary>
    void MoveFocusBackward();

    /// <summary>
    /// Moves focus to a specific element by name.
    /// </summary>
    void FocusByName(string elementName);

    /// <summary>
    /// Gets a list of all focusable elements.
    /// </summary>
    IReadOnlyList<string> GetFocusableElements();
}