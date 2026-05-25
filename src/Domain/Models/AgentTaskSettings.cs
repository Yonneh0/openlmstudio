namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Configuration for an agent task.
/// </summary>
public class AgentTaskSettings
{
    /// <summary>Current working directory.</summary>
    public string Cwd { get; set; } = string.Empty;

    /// <summary>Task ID.</summary>
    public Guid TaskId { get; set; }

    /// <summary>Unique task identifier.</summary>
    public string Ulid { get; set; } = string.Empty;

    /// <summary>Current mode (Plan or Act).</summary>
    public string Mode { get; set; } = "Act";

    /// <summary>Whether strict plan mode is enabled.</summary>
    public bool StrictPlanModeEnabled { get; set; }

    /// <summary>Whether YOLO mode is toggled.</summary>
    public bool YoloModeToggled { get; set; }

    /// <summary>Whether double-check completion is enabled.</summary>
    public bool DoubleCheckCompletionEnabled { get; set; }

    /// <summary>VSCode terminal execution mode.</summary>
    public string VscodeTerminalExecutionMode { get; set; } = "default";

    /// <summary>Whether parallel tool calling is enabled.</summary>
    public bool EnableParallelToolCalling { get; set; }

    /// <summary>Whether this is a subagent execution.</summary>
    public bool IsSubagentExecution { get; set; }

    /// <summary>Auto-approval settings.</summary>
    public AgentAutoApprovalSettings AutoApprovalSettings { get; set; } = new();

    /// <summary>Browser settings.</summary>
    public AgentBrowserSettings BrowserSettings { get; set; } = new();

    /// <summary>Focus chain settings.</summary>
    public AgentFocusChainSettings FocusChainSettings { get; set; } = new();

    /// <summary>
    /// Creates default settings for a new task.
    /// </summary>
    public static AgentTaskSettings CreateDefault(string cwd = "")
    {
        return new AgentTaskSettings
        {
            Cwd = cwd,
            TaskId = Guid.NewGuid(),
            Ulid = GenerateUlid(),
            Mode = "Act",
            StrictPlanModeEnabled = false,
            YoloModeToggled = false,
            DoubleCheckCompletionEnabled = true,
            VscodeTerminalExecutionMode = "default",
            EnableParallelToolCalling = true,
            IsSubagentExecution = false,
        };
    }

    private static string GenerateUlid()
    {
        var bytes = Guid.NewGuid().ToByteArray();
        return Convert.ToBase64String(bytes).Substring(0, 26);
    }
}

/// <summary>
/// Auto-approval settings for the agent.
/// </summary>
public class AgentAutoApprovalSettings
{
    /// <summary>Whether auto-approval is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Default timeout for commands (in seconds).</summary>
    public int DefaultTimeout { get; set; } = 30;

    /// <summary>Whether to auto-approve file operations.</summary>
    public bool AutoApproveFileOperations { get; set; } = true;

    /// <summary>Whether to auto-approve command execution.</summary>
    public bool AutoApproveCommandExecution { get; set; } = true;

    /// <summary>Whether to auto-approve browser actions.</summary>
    public bool AutoApproveBrowserActions { get; set; } = true;
}

/// <summary>
/// Browser settings for the agent.
/// </summary>
public class AgentBrowserSettings
{
    /// <summary>Browser width.</summary>
    public int Width { get; set; } = 1280;

    /// <summary>Browser height.</summary>
    public int Height { get; set; } = 720;

    /// <summary>Whether screenshots are enabled.</summary>
    public bool EnableScreenshots { get; set; } = true;
}

/// <summary>
/// Focus chain settings for the agent.
/// </summary>
public class AgentFocusChainSettings
{
    /// <summary>Maximum number of focus chain items.</summary>
    public int MaxItems { get; set; } = 10;

    /// <summary>Whether to prioritize recent items.</summary>
    public bool PrioritizeRecent { get; set; } = true;
}