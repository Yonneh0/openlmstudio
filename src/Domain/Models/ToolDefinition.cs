namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Represents a tool that an agent can invoke.
/// </summary>
public record ToolDefinition
{
    /// <summary>Unique name of the tool (e.g., "write_to_file").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Human-readable description of what the tool does.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Required parameter names for this tool.</summary>
    public List<string> RequiredParameters { get; init; } = new();

    /// <summary>Parameter schema: name → type description.</summary>
    public Dictionary<string, string> ParameterSchema { get; init; } = new();

    /// <summary>Whether this tool is available in the current mode (PLAN vs ACT).</summary>
    public ToolAvailability Availability { get; set; } = ToolAvailability.All;
}

/// <summary>
/// Determines which modes a tool is available in.
/// </summary>
public enum ToolAvailability
{
    /// <summary>Available in all modes.</summary>
    All,
    /// <summary>Only available in PLAN MODE.</summary>
    PlanOnly,
    /// <summary>Only available in ACT MODE.</summary>
    ActOnly,
}