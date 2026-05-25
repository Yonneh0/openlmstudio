using System;
using System.Text;
using OpenLMStudio.Application.Services;

namespace OpenLMStudio.Desktop.Services;

/// <summary>
/// Avalonia-specific markdown renderer that converts markdown to RichTextBlock-compatible markup.
/// Uses Markdig for parsing and produces a simple markup language that can be rendered in Avalonia.
/// </summary>
public class AvaloniaMarkdownRenderer : IMarkdownRenderer
{
    private readonly Markdig.MarkdownPipeline _pipeline;

    public AvaloniaMarkdownRenderer()
    {
        _pipeline = new Markdig.MarkdownPipelineBuilder().Build();
    }

    public string Render(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;

        var html = Markdig.Markdown.ToHtml(markdown, _pipeline);
        return ConvertHtmlToAvaloniaMarkup(html);
    }

    public string ExtractPlainText(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;

        var html = Markdig.Markdown.ToHtml(markdown, _pipeline);
        return StripHtmlTags(html);
    }

    private static string ConvertHtmlToAvaloniaMarkup(string html)
    {
        var sb = new StringBuilder(html.Length);
        var i = 0;

        while (i < html.Length)
        {
            // Handle pre/code blocks
            if (html[i] == '<' && html.Substring(i).StartsWith("<pre"))
            {
                var closePre = html.IndexOf("</pre>", i);
                if (closePre > 0)
                {
                    var preContent = html.Substring(i + 5, closePre - i - 5);
                    // Strip inner tags for pre content
                    var cleanContent = StripHtmlTags(preContent);
                    sb.AppendLine();
                    sb.Append("```\n");
                    sb.Append(cleanContent);
                    sb.AppendLine();
                    sb.Append("```");
                    sb.AppendLine();
                    i = closePre + 6;
                    continue;
                }
            }

            // Handle code blocks
            if (html[i] == '<' && html.Substring(i).StartsWith("<code"))
            {
                var closeCode = html.IndexOf("</code>", i);
                if (closeCode > 0)
                {
                    var codeContent = html.Substring(i + 5, closeCode - i - 5);
                    var cleanCode = StripHtmlTags(codeContent).Replace("<", "<").Replace(">", ">").Replace("&", "&");
                    sb.Append("`").Append(cleanCode).Append("`");
                    i = closeCode + 7;
                    continue;
                }
            }

            // Handle bold
            if (html[i] == '<' && html.Substring(i).StartsWith("<strong") || html.Substring(i).StartsWith("<b"))
            {
                var closeTag = html.IndexOf("</", i + 1);
                if (closeTag > 0)
                {
                    var tagEnd = html.IndexOf(">", closeTag);
                    var content = html.Substring(closeTag + 2, tagEnd - closeTag - 2);
                    sb.Append("**").Append(StripHtmlTags(content)).Append("**");
                    i = tagEnd + 1;
                    continue;
                }
            }

            // Handle italic
            if (html[i] == '<' && html.Substring(i).StartsWith("<em") || html.Substring(i).StartsWith("<i"))
            {
                var closeTag = html.IndexOf("</", i + 1);
                if (closeTag > 0)
                {
                    var tagEnd = html.IndexOf(">", closeTag);
                    var content = html.Substring(closeTag + 2, tagEnd - closeTag - 2);
                    sb.Append("*").Append(StripHtmlTags(content)).Append("*");
                    i = tagEnd + 1;
                    continue;
                }
            }

            // Handle links
            if (html[i] == '<' && html.Substring(i).StartsWith("<a "))
            {
                var closeTag = html.IndexOf("</a>", i);
                if (closeTag > 0)
                {
                    var hrefStart = html.IndexOf("href=", i);
                    var hrefEnd = html.IndexOf('"', hrefStart + 6);
                    var href = html.Substring(hrefStart + 6, hrefEnd - hrefStart - 6);
                    var tagEnd = html.IndexOf(">", closeTag);
                    var content = html.Substring(closeTag + 4, tagEnd - closeTag - 4);
                    sb.Append("[").Append(StripHtmlTags(content)).Append("](").Append(href).Append(")");
                    i = tagEnd + 1;
                    continue;
                }
            }

            // Handle headers
            if (html[i] == '<' && html[i + 1] == 'h' && html[i + 2] >= '1' && html[i + 2] <= '6')
            {
                var level = html[i + 2] - '0';
                var tagEnd = html.IndexOf(">", i);
                var closeTag = html.IndexOf("</h", i);
                if (closeTag > 0 && tagEnd < closeTag)
                {
                    tagEnd = html.IndexOf(">", closeTag);
                    var content = html.Substring(tagEnd + 1, closeTag - tagEnd - 1);
                    for (int h = 0; h < level; h++) sb.Append('#');
                    sb.Append(' ').Append(StripHtmlTags(content)).AppendLine();
                    i = tagEnd + 1;
                    continue;
                }
            }

            // Handle lists
            if (html[i] == '<' && html.Substring(i).StartsWith("<li>"))
            {
                sb.Append("- ");
                i += 4;
                continue;
            }

            // Skip all HTML tags
            if (html[i] == '<')
            {
                var tagEnd = html.IndexOf('>', i);
                if (tagEnd > 0)
                {
                    i = tagEnd + 1;
                    continue;
                }
            }

            // Handle HTML entities
            if (html[i] == '&')
            {
                var entityEnd = html.IndexOf(';', i);
                if (entityEnd > 0 && entityEnd - i < 10)
                {
                    var entity = html.Substring(i + 1, entityEnd - i - 1);
                    switch (entity)
                    {
                        case "lt": sb.Append('<'); break;
                        case "gt": sb.Append('>'); break;
                        case "amp": sb.Append('&'); break;
                        case "quot": sb.Append('"'); break;
                        case "apos": sb.Append('\''); break;
                        case "nbsp": sb.Append(' '); break;
                        default: sb.Append('&').Append(entity).Append(';'); break;
                    }
                    i = entityEnd + 1;
                    continue;
                }
            }

            sb.Append(html[i]);
            i++;
        }

        return sb.ToString();
    }

    private static string StripHtmlTags(string html)
    {
        var sb = new StringBuilder(html.Length);
        var inTag = false;
        foreach (var c in html)
        {
            if (c == '<')
            {
                inTag = true;
                continue;
            }
            if (c == '>')
            {
                inTag = false;
                sb.Append(' ');
                continue;
            }
            if (!inTag)
                sb.Append(c);
        }
        return sb.ToString().Trim();
    }
}
