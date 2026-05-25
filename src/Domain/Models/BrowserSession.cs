namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Represents a Puppeteer-controlled browser session.
/// </summary>
public class BrowserSession
{
    /// <summary>Unique identifier for this browser session.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Current URL of the browser.</summary>
    public string? CurrentUrl { get; set; }

    /// <summary>Viewport resolution (width x height).</summary>
    public int ViewportWidth { get; set; } = 1280;

    /// <summary>Viewport resolution (width x height).</summary>
    public int ViewportHeight { get; set; } = 720;

    /// <summary>Whether the browser is currently open.</summary>
    public bool IsOpen { get; set; }

    /// <summary>Last screenshot captured (base64 or file path).</summary>
    public string? LastScreenshot { get; set; }

    /// <summary>Console logs captured from the browser.</summary>
    public List<string> ConsoleLogs { get; set; } = new();

    /// <summary>
    /// Creates a new browser session with default viewport.
    /// </summary>
    public static BrowserSession Create() => new();

    /// <summary>
    /// Closes the browser session.
    /// </summary>
    public void Close()
    {
        IsOpen = false;
        CurrentUrl = null;
        LastScreenshot = null;
        ConsoleLogs.Clear();
    }
}