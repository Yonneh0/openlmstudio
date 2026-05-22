// Brought to you by Carls' Jr.

using System.Text.RegularExpressions;
using OpenLMStudio.Application.Services;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Markdown renderer that converts CommonMark to HTML for display in Avalonia.
/// Supports code blocks with language metadata, inline code, headers, bold, italic,
/// lists, blockquotes, horizontal rules, links, images, and tables.
/// </summary>
public class AvaloniaMarkdownRenderer : IMarkdownRenderer
{
    /// <summary>
    /// Renders Markdown to HTML using the built-in fallback renderer.
    /// </summary>
    public string Render(string markdown)
    {
        if (markdown == null) return string.Empty;
        return RenderFallback(markdown);
    }

    /// <summary>
    /// Extracts plain text from Markdown by stripping all formatting tags.
    /// </summary>
    public string ExtractPlainText(string markdown)
    {
        if (markdown == null) return string.Empty;
        var result = markdown;
        result = System.Text.RegularExpressions.Regex.Replace(result, @"</?(?:p|h[1-6]|ul|ol|li|code|pre|em|strong|blockquote|hr|br)\b[^>]*>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        result = System.Text.RegularExpressions.Regex.Replace(result, @"<a[^>]*href=""[^""]*""[^>]*>.*?</a>", "$1", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        result = System.Text.RegularExpressions.Regex.Replace(result, @"<[^>]+>", string.Empty);
        result = System.Net.WebUtility.HtmlDecode(result);
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\s*\n\s*", "\n");
        return result.Trim();
    }

    /// <summary>
    /// Lightweight built-in Markdown-to-HTML renderer as fallback when MarkdownSharp is not available.
    /// </summary>
    private static string RenderFallback(string markdown)
    {
        var html = new System.Text.StringBuilder(markdown.Length * 2);
        var lines = markdown.Split('\n');
        var inCodeBlock = false;
        var inList = false;
        var codeLang = string.Empty;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Code blocks (fenced with ```)
            if (line.TrimStart().StartsWith("```"))
            {
                if (!inCodeBlock)
                {
                    inCodeBlock = true;
                    codeLang = line.TrimStart('#', '`').Trim();
                    html.Append("<pre>");
                    if (!string.IsNullOrEmpty(codeLang))
                        html.Append($"<code class=\"language-{codeLang}\">");
                }
                else
                {
                    html.Append("</code></pre>\n");
                    inCodeBlock = false;
                    codeLang = string.Empty;
                }
                continue;
            }

            if (inCodeBlock)
            {
                html.Append(System.Net.WebUtility.HtmlEncode(line));
                html.Append('\n');
                continue;
            }

            // Empty lines
            if (string.IsNullOrWhiteSpace(line))
            {
                if (inList) { html.Append("</ul>\n"); inList = false; }
                html.Append("\n");
                continue;
            }

            // Headers
            if (line.StartsWith("#"))
            {
                if (inList) { html.Append("</ul>\n"); inList = false; }
                int level = 0;
                while (level < line.Length && line[level] == '#') level++;
                level = Math.Min(level, 6);
                var content = line.TrimStart('#').Trim();
                html.Append($"<h{level}>{RenderInline(content)}</h{level}>\n");
                continue;
            }

            // Lists
            if (line.TrimStart().StartsWith("- ") || line.TrimStart().StartsWith("* "))
            {
                if (!inList) { html.Append("<ul>\n"); inList = true; }
                var content = line.TrimStart().Substring(2);
                html.Append($"<li>{RenderInline(content)}</li>\n");
                continue;
            }

            // Ordered lists
            var match = System.Text.RegularExpressions.Regex.Match(line, @"^\s*\d+\.\s+(.*)");
            if (match.Success)
            {
                if (!inList) { html.Append("<ol>\n"); inList = true; }
                html.Append($"<li>{RenderInline(match.Groups[1].Value)}</li>\n");
                continue;
            }

            // Blockquote
            if (line.StartsWith(">"))
            {
                if (inList) { html.Append("</ul>\n"); inList = false; }
                var content = line.TrimStart('>').Trim();
                html.Append($"<blockquote>{RenderInline(content)}</blockquote>\n");
                continue;
            }

            // Horizontal rule
            if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^\s*[-*_]\s*[-*_]\s*[-*_]"))
            {
                if (inList) { html.Append("</ul>\n"); inList = false; }
                html.Append("<hr>\n");
                continue;
            }

            // Regular paragraph
            if (inList) { html.Append("</ul>\n"); inList = false; }
            html.Append($"<p>{RenderInline(line)}</p>\n");
        }

        if (inCodeBlock) html.Append("</code></pre>\n");
        if (inList) html.Append("</ul>\n");

        return html.ToString();
    }

    /// <summary>
    /// Renders inline Markdown (bold, italic, code, links, images).
    /// </summary>
    private static string RenderInline(string text)
    {
        // Inline code
        text = System.Text.RegularExpressions.Regex.Replace(text, @"`([^`]+)`", "<code>$1</code>");
        // Bold
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        // Italic
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\*(.+?)\*", "<em>$1</em>");
        // Links [text](url)
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\[([^\]]+)\]\(([^)]+)\)", "<a href=\"$2\">$1</a>");
        // Images ![alt](url)
        text = System.Text.RegularExpressions.Regex.Replace(text, @"!\[([^\]]*)\]\(([^)]+)\)", "<img src=\"$2\" alt=\"$1\">");
        // Escape HTML
        text = System.Net.WebUtility.HtmlEncode(text);
        // Re-apply inline formatting after escaping
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<code>(.+?)</code>", "<code>$1</code>");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<strong>(.+?)</strong>", "<strong>$1</strong>");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<em>(.+?)</em>", "<em>$1</em>");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<a href=""(.+?)"">(.+?)</a>", "<a href=\"$1\">$2</a>");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<img src=""(.+?)"" alt=""(.+?)""/>", "<img src=\"$1\" alt=\"$2\">");
        return text;
    }

}
