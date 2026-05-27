namespace OpenLMStudio.Desktop.Models;

/// <summary>
/// Data class for settings serialization.
/// </summary>
public class SettingsData
{
    public int ServerPort { get; set; } = 8080;
    public bool EnableHttps { get; set; }
    public string? ApiKey { get; set; }
    public int RateLimit { get; set; } = 60;
    public int ContextLength { get; set; } = 4096;
    public int GpuOffloadLayers { get; set; } = -1;
    public double Temperature { get; set; } = 0.7;
    public double TopP { get; set; } = 1.0;
    public int MaxTokens { get; set; } = 4096;
    public string? ContextLengthOverride { get; set; }
    public int MaxAgentIterations { get; set; } = 50;
    public int AgentAutoCommitSizeKB { get; set; } = 1024;
    public bool RequirePlanApproval { get; set; }
    public bool EnableEncryption { get; set; }
    public int ModelCacheTimeoutDays { get; set; } = 30;
    public bool AutoCleanupOrphanedModels { get; set; }

    // Plugin settings
    public string? PluginRegistryUrl { get; set; }
    public int PluginUpdateIntervalMinutes { get; set; } = 60;
    public PluginSandboxPolicy PluginSandboxPolicy { get; set; } = PluginSandboxPolicy.Strict;
    public int McpTimeoutSeconds { get; set; } = 30;
    public int MaxToolsPerMcpServer { get; set; } = 50;
}

/// <summary>
/// Plugin sandbox policy levels.
/// </summary>
public enum PluginSandboxPolicy
{
    Strict,     // No filesystem access
    Restricted, // Read-only filesystem
    Full        // Unrestricted
}