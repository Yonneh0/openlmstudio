using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Manages accessibility settings across the application.
/// </summary>
public class AccessibilityService : IAccessibilityService
{
    private readonly ILogger<AccessibilityService>? _logger;
    private bool _screenReaderEnabled;
    private bool _highContrastEnabled;
    private int _preferredFontSize;
    private bool _keyboardNavigationEnabled;

    public AccessibilityService(ILogger<AccessibilityService>? logger = null)
    {
        _logger = logger;
        _preferredFontSize = 14; // Default readable size
        _keyboardNavigationEnabled = true;
    }

    public bool ScreenReaderEnabled
    {
        get => _screenReaderEnabled;
        set
        {
            if (_screenReaderEnabled != value)
            {
                _screenReaderEnabled = value;
                _logger?.LogInformation("Screen reader enabled: {Enabled}", value);
            }
        }
    }

    public bool HighContrastEnabled
    {
        get => _highContrastEnabled;
        set
        {
            if (_highContrastEnabled != value)
            {
                _highContrastEnabled = value;
                _logger?.LogInformation("High contrast enabled: {Enabled}", value);
            }
        }
    }

    public int PreferredFontSize
    {
        get => _preferredFontSize;
        set
        {
            if (_preferredFontSize != value)
            {
                _preferredFontSize = Math.Max(10, Math.Min(24, value));
                _logger?.LogDebug("Font size changed to {Size}", value);
            }
        }
    }

    public bool KeyboardNavigationEnabled
    {
        get => _keyboardNavigationEnabled;
        set
        {
            if (_keyboardNavigationEnabled != value)
            {
                _keyboardNavigationEnabled = value;
                _logger?.LogInformation("Keyboard navigation enabled: {Enabled}", value);
            }
        }
    }

    public IReadOnlyDictionary<string, object> GetSettings()
    {
        return new Dictionary<string, object>
        {
            { "ScreenReaderEnabled", _screenReaderEnabled },
            { "HighContrastEnabled", _highContrastEnabled },
            { "PreferredFontSize", _preferredFontSize },
            { "KeyboardNavigationEnabled", _keyboardNavigationEnabled }
        };
    }

    public void ApplySettings()
    {
        _logger?.LogInformation("Applying accessibility settings: ScreenReader={ScreenReader}, HighContrast={HighContrast}, FontSize={FontSize}, KeyboardNav={KeyboardNav}",
            _screenReaderEnabled, _highContrastEnabled, _preferredFontSize, _keyboardNavigationEnabled);
    }
}