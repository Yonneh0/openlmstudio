namespace OpenLMStudio.Application.Types.Agent;

/// <summary>
/// Request for browser_action tool.
/// </summary>
public record BrowserActionRequest
{
    /// <summary>Action to perform (launch, click, type, scroll_down, scroll_up, close).</summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>URL to launch (for launch action).</summary>
    public string? Url { get; init; }

    /// <summary>x,y coordinates for click action.</summary>
    public string? Coordinate { get; init; }

    /// <summary>Text to type (for type action).</summary>
    public string? Text { get; init; }

    /// <summary>Task progress checklist (optional).</summary>
    public string? TaskProgress { get; init; }
}