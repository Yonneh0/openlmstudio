namespace OpenLMStudio.Application.Types.Agent;

/// <summary>
/// Request for list_files tool.
/// </summary>
public record ListFilesRequest
{
    /// <summary>Directory path to list contents for.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Whether to list files recursively (default: false).</summary>
    public bool Recursive { get; init; }

    /// <summary>Task progress checklist (optional).</summary>
    public string? TaskProgress { get; init; }
}

/// <summary>
/// Request for write_to_file tool.
/// </summary>
public record WriteFileRequest
{
    /// <summary>Relative path to the file (e.g., "src/index.ts").</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>The complete content to write.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>Task progress checklist (optional).</summary>
    public string? TaskProgress { get; init; }
}

/// <summary>
/// Request for read_file tool.
/// </summary>
public record ReadFileRequest
{
    /// <summary>Relative path to the file.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>1-based line number to start reading from (default: 1).</summary>
    public int StartLine { get; init; } = 1;

    /// <summary>1-based line number to stop reading at (default: startLine + 1000).</summary>
    public int EndLine { get; init; } = 1001;

    /// <summary>Task progress checklist (optional).</summary>
    public string? TaskProgress { get; init; }
}

/// <summary>
/// Request for replace_in_file tool.
/// </summary>
public record ReplaceFileRequest
{
    /// <summary>Relative path to the file.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>One or more SEARCH/REPLACE blocks.</summary>
    public string Diff { get; init; } = string.Empty;

    /// <summary>Task progress checklist (optional).</summary>
    public string? TaskProgress { get; init; }
}

/// <summary>
/// Request for search_files tool.
/// </summary>
public record SearchFilesRequest
{
    /// <summary>Directory path to search in.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Regular expression pattern (uses Rust regex syntax).</summary>
    public string Regex { get; init; } = string.Empty;

    /// <summary>Glob pattern to filter files (e.g., "*.ts").</summary>
    public string? FilePattern { get; init; }

    /// <summary>Task progress checklist (optional).</summary>
    public string? TaskProgress { get; init; }
}

/// <summary>
/// Request for execute_command tool.
/// </summary>
public record ExecuteCommandRequest
{
    /// <summary>The CLI command to execute.</summary>
    public string Command { get; init; } = string.Empty;

    /// <summary>Whether the command requires explicit user approval.</summary>
    public bool RequiresApproval { get; init; }

    /// <summary>Timeout in seconds (optional).</summary>
    public int? Timeout { get; init; }

    /// <summary>Task progress checklist (optional).</summary>
    public string? TaskProgress { get; init; }
}

/// <summary>
/// Request for use_mcp_tool tool.
/// </summary>
public record UseMcpToolRequest
{
    /// <summary>Name of the MCP server.</summary>
    public string ServerName { get; init; } = string.Empty;

    /// <summary>Name of the tool to execute.</summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>JSON object containing the tool's input parameters.</summary>
    public string Arguments { get; init; } = string.Empty;

    /// <summary>Task progress checklist (optional).</summary>
    public string? TaskProgress { get; init; }
}

/// <summary>
/// Request for access_mcp_resource tool.
/// </summary>
public record AccessMcpResourceRequest
{
    /// <summary>Name of the MCP server.</summary>
    public string ServerName { get; init; } = string.Empty;

    /// <summary>URI identifying the resource.</summary>
    public string Uri { get; init; } = string.Empty;

    /// <summary>Task progress checklist (optional).</summary>
    public string? TaskProgress { get; init; }
}

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