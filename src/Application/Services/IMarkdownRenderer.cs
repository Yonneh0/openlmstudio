using System;

namespace OpenLMStudio.Application.Services;

/// <summary>
/// Converts Markdown text into formatted UI content for display in Avalonia controls.
/// </summary>
public interface IMarkdownRenderer
{
    /// <summary>
    /// Renders markdown text into a formatted block suitable for display in a chat message.
    /// </summary>
    /// <param name="markdown">The markdown string to render.</param>
    /// <returns>A string of formatted content (HTML-like or Avalonia markup).</returns>
    string Render(string markdown);

    /// <summary>
    /// Extracts plain text from a markdown string (strips formatting).
    /// </summary>
    string ExtractPlainText(string markdown);
}
