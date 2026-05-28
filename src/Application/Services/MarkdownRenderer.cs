namespace OpenLMStudio.Application.Services;

using System.IO;
using Markdig;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using Markdig.Extensions.Yaml;
using Markdig.Renderers;
using Markdig.Syntax;
using OpenLMStudio.Domain.Models;

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

/// <summary>
/// IMarkdownRenderer implementation using Markdig with AdvancedExtensions, Abbreviations,
/// Mathematics, YamlFrontMatter.
/// </summary>
public class MarkdownRenderer : IMarkdownRenderer
{
    private readonly MarkdownPipeline _pipeline;
    private readonly MarkdownPipeline _plainPipeline;

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

        try
        {
            return Markdig.Markdown.ToHtml(markdown, _pipeline);
        }
        catch
        {
            return markdown;
        }
    }

    public string ExtractPlainText(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;

        try
        {
            var html = Markdig.Markdown.ToHtml(markdown, _plainPipeline);
            var result = html.Replace("<p>", System.String.Empty)
                .Replace("</p>", System.String.Empty)
                .Replace("<br />", "\n")
                .Replace("<br/>", "\n")
                .Replace("<br>", "\n");
            // Decode HTML entities
            result = System.Net.WebUtility.HtmlDecode(result);
            return result.Trim();
        }
        catch
        {
            return markdown;
        }
    }
}