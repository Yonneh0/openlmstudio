namespace OpenLMStudio.Domain.Interfaces;

using Models;

/// <summary>
/// Interacts with a Puppeteer-controlled browser.
/// </summary>
public interface IBrowserService
{
    /// <summary>
    /// Launches or interacts with the browser.
    /// </summary>
    /// <param name="action">Action to perform (launch, click, type, scroll_down, scroll_up, close).</param>
    /// <param name="url">URL to launch (for launch action).</param>
    /// <param name="coordinate">x,y coordinates for click action.</param>
    /// <param name="text">Text to type (for type action).</param>
    /// <returns>Tool result with screenshot and console logs.</returns>
    Task<ToolResult> ActionAsync(string action, string? url = null, string? coordinate = null, string? text = null);
}