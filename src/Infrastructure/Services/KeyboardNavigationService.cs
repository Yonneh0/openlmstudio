using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Provides keyboard navigation helpers for accessibility.
/// </summary>
public class KeyboardNavigationService : IKeyboardNavigationService
{
    private readonly ILogger<KeyboardNavigationService>? _logger;
    private readonly List<string> _focusableElements = new();
    private int _currentIndex = -1;

    public KeyboardNavigationService(ILogger<KeyboardNavigationService>? logger = null)
    {
        _logger = logger;
    }

    public void RegisterFocusableElement(string elementName)
    {
        if (!_focusableElements.Contains(elementName))
        {
            _focusableElements.Add(elementName);
            _logger?.LogDebug("Registered focusable element: {Element}", elementName);
        }
    }

    public void UnregisterFocusableElement(string elementName)
    {
        _focusableElements.Remove(elementName);
        if (_currentIndex >= _focusableElements.Count)
        {
            _currentIndex = Math.Max(0, _focusableElements.Count - 1);
        }
        _logger?.LogDebug("Unregistered focusable element: {Element}", elementName);
    }

    public void MoveFocusForward()
    {
        if (_focusableElements.Count == 0)
        {
            _logger?.LogWarning("No focusable elements registered");
            return;
        }

        _currentIndex = (_currentIndex + 1) % _focusableElements.Count;
        _logger?.LogDebug("Focus moved forward to: {Element}", _focusableElements[_currentIndex]);
    }

    public void MoveFocusBackward()
    {
        if (_focusableElements.Count == 0)
        {
            _logger?.LogWarning("No focusable elements registered");
            return;
        }

        _currentIndex = (_currentIndex - 1 + _focusableElements.Count) % _focusableElements.Count;
        _logger?.LogDebug("Focus moved backward to: {Element}", _focusableElements[_currentIndex]);
    }

    public void FocusByName(string elementName)
    {
        var index = _focusableElements.IndexOf(elementName);
        if (index >= 0)
        {
            _currentIndex = index;
            _logger?.LogDebug("Focus moved to: {Element}", elementName);
        }
        else
        {
            _logger?.LogWarning("Element not found: {Element}", elementName);
        }
    }

    public IReadOnlyList<string> GetFocusableElements() => _focusableElements.AsReadOnly();

    public string? GetCurrentFocusedElement() => _currentIndex >= 0 && _currentIndex < _focusableElements.Count
        ? _focusableElements[_currentIndex]
        : null;
}