namespace OpenLMStudio.Application.Services.Agent;

using System.Net.Http;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Interfaces;
using OpenLMStudio.Domain.Models;

/// <summary>
/// Implements web_fetch tool for fetching content from URLs.
/// Fully implemented with HTTP/HTTPS support and automatic HTTPS upgrade.
/// </summary>
public class WebFetchService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WebFetchService> _logger;

    public WebFetchService(IHttpClientFactory? httpClientFactory = null, ILogger<WebFetchService>? logger = null)
    {
        _httpClient = httpClientFactory?.CreateClient("AgentWeb") ?? new HttpClient();
        _logger = logger;
    }

    /// <summary>
    /// Fetches content from a URL.
    /// HTTP URLs are automatically upgraded to HTTPS.
    /// </summary>
    public async Task<ToolResult> FetchAsync(string url, string prompt)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(url))
                return ToolResult.Fail("Missing required parameter: url");
            if (string.IsNullOrWhiteSpace(prompt))
                return ToolResult.Fail("Missing required parameter: prompt");

            // Upgrade HTTP to HTTPS
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url.Substring("http://".Length);

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();

            // Truncate very long responses
            if (content.Length > 50000)
                content = content.Substring(0, 50000) + "\n...(truncated)";

            var output = $"Content from {url}:\n\n{content}";

            stopwatch.Stop();
            _logger?.LogInformation("web_fetch: Fetched {Url}", url);

            return ToolResult.Ok(output) with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            return ToolResult.Fail($"HTTP error fetching {url}: {ex.Message}")
                with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
        catch (TaskCanceledException ex)
        {
            stopwatch.Stop();
            return ToolResult.Fail($"Timeout fetching {url}: {ex.Message}")
                with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ToolResult.Fail($"Error fetching {url}: {ex.Message}")
                with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
    }
}
