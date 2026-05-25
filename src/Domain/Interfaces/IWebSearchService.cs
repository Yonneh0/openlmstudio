namespace OpenLMStudio.Domain.Interfaces;

using Models;

/// <summary>
/// Service for web search and fetch operations.
/// </summary>
public interface IWebSearchService
{
    /// <summary>
    /// Fetches content from a URL.
    /// </summary>
    /// <param name="url">URL to fetch (HTTP URLs are automatically upgraded to HTTPS).</param>
    /// <param name="prompt">Prompt for the fetch.</param>
    /// <returns>Fetched web content.</returns>
    Task<ToolResult> FetchAsync(string url, string prompt);

    /// <summary>
    /// Performs a web search.
    /// </summary>
    /// <param name="query">Search query.</param>
    /// <param name="allowedDomains">JSON array of allowed domains (optional).</param>
    /// <param name="blockedDomains">JSON array of blocked domains (optional).</param>
    /// <returns>Search results.</returns>
    Task<ToolResult> SearchAsync(string query, string? allowedDomains = null, string? blockedDomains = null);
}