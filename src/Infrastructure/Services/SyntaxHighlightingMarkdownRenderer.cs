// Brought to you by Carls' Jr.

using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using OpenLMStudio.Application.Services;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Markdown renderer that produces HTML with basic syntax highlighting for code blocks.
/// Detects language from info tokens and wraps code in semantic HTML elements.
/// </summary>
public class SyntaxHighlightingMarkdownRenderer : IMarkdownRenderer
{
    private static readonly MarkdownPipeline _pipeline;
    private static readonly Regex _codeBlockRegex = new(
        @"```(\w+)?\s*\n(.*?)\n```",
        RegexOptions.Singleline | RegexOptions.Compiled);

    static SyntaxHighlightingMarkdownRenderer()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    /// <summary>
    /// Renders Markdown to HTML with basic syntax highlighting for code blocks.
    /// </summary>
    public string Render(string markdown)
    {
        if (markdown == null) return string.Empty;

        // First, convert basic markdown to HTML using Markdig
        var html = Markdig.Markdown.ToHtml(markdown, _pipeline);

        // Then, enhance code blocks with syntax highlighting
        return EnhanceCodeBlocks(html);
    }

    /// <summary>
    /// Extracts plain text from Markdown (strips all formatting).
    /// </summary>
    public string ExtractPlainText(string markdown)
    {
        if (markdown == null) return string.Empty;

        // Use a simple pipeline without extensions, then strip HTML tags
        var plainPipeline = new MarkdownPipelineBuilder().Build();
        var raw = Markdig.Markdown.ToHtml(markdown, plainPipeline);
        return StripHtmlTags(raw);
    }

    private static string EnhanceCodeBlocks(string html)
    {
        // Find <pre><code> blocks and add language class for external highlighters
        // This allows CSS-based syntax highlighting to work
        var result = new StringBuilder(html.Length);

        using var reader = new StringReader(html);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            // Add language class to code blocks
            var match = Regex.Match(line, @"<code[^>]*class=""language-(?<lang>\w+)""");
            if (match.Success)
            {
                result.AppendLine(line);
            }
            else if (line.TrimStart().StartsWith("<code>", StringComparison.Ordinal))
            {
                // Add generic language-unknown class if no language detected
                result.AppendLine(line.Replace("<code>", "<code class=\"language-unknown\">", StringComparison.Ordinal));
            }
            else
            {
                result.AppendLine(line);
            }
        }

        return result.ToString();
    }

    private static string StripHtmlTags(string html)
    {
        if (html == null) return string.Empty;
        var result = new StringBuilder(html.Length);
        var inTag = false;
        for (int i = 0; i < html.Length; i++)
        {
            char c = html[i];
            if (c == '<') { inTag = true; continue; }
            if (c == '>') { inTag = false; continue; }
            if (!inTag) result.Append(c);
        }
        return result.ToString().Trim();
    }
}