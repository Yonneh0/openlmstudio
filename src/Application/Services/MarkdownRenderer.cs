using Markdig;
using OpenLMStudio.Application.Services;

namespace OpenLMStudio.Application.Services;

/// <summary>
/// Markdown renderer backed by Markdig, producing HTML suitable for display in Avalonia.
/// Supports code blocks with language detection, headers, bold, italic, lists, links, and tables.
/// </summary>
public class MarkdownRenderer : IMarkdownRenderer
{
    private readonly Markdig.MarkdownPipeline _pipeline;
    private readonly Markdig.MarkdownPipeline _plainPipeline;

    public MarkdownRenderer()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseAbbreviations()
            .UseMathematics()
            .UseYamlFrontMatter()
            .Build();

        _plainPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    public string Render(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;

        return Markdig.Markdown.ToHtml(markdown, _pipeline);
    }

    public string ExtractPlainText(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;

        // Use a simple pipeline and then strip HTML tags to get plain text
        var html = Markdig.Markdown.ToHtml(markdown, _plainPipeline);
        return StripHtmlTags(html);
    }

    private static string StripHtmlTags(string html)
    {
        var sb = new System.Text.StringBuilder(html.Length);
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
        var result = sb.ToString().Trim();
        // Collapse whitespace
        var cleaned = new System.Text.StringBuilder(result.Length);
        var prevSpace = false;
        foreach (var c in result)
        {
            if (c == ' ' || c == '\n' || c == '\r')
            {
                if (!prevSpace)
                    cleaned.Append(' ');
                prevSpace = true;
            }
            else
            {
                cleaned.Append(c);
                prevSpace = false;
            }
        }
        return cleaned.ToString().Trim();
    }
}