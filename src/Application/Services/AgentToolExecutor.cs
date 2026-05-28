namespace OpenLMStudio.Application.Services.Agent;

using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenLMStudio.Application.Types.Agent;
using OpenLMStudio.Domain.Interfaces;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Application.Services;

/// <summary>
/// Orchestrates tool execution for the agent.
/// Routes tool calls to the appropriate service based on tool name.
/// Fully implements tools 1-5, 17, 19, 21.
/// </summary>
public class AgentToolExecutor : IAgentToolExecutor
{
    private readonly IFileSystemService _fileSystem;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IBrowserService _browserService;
    private readonly IMcpService _mcpService;
    private readonly IWebSearchService _webSearchService;
    private readonly PatchService _patchService;
    private readonly WebFetchService _webFetchService;
    private readonly QuestionService _questionService;
    private readonly ToolAvailabilityRegistry _toolRegistry;
    private readonly ILogger<AgentToolExecutor> _logger;
    private bool _isPlanMode;

    public AgentToolExecutor(
        IFileSystemService fileSystem,
        ICommandExecutor commandExecutor,
        IBrowserService browserService,
        IMcpService mcpService,
        IWebSearchService webSearchService,
        PatchService patchService,
        WebFetchService webFetchService,
        QuestionService questionService,
        ToolAvailabilityRegistry toolRegistry,
        ILogger<AgentToolExecutor>? logger = null)
    {
        _fileSystem = fileSystem;
        _commandExecutor = commandExecutor;
        _browserService = browserService;
        _mcpService = mcpService;
        _webSearchService = webSearchService;
        _patchService = patchService;
        _webFetchService = webFetchService;
        _questionService = questionService;
        _toolRegistry = toolRegistry;
        _logger = logger ?? NullLogger<AgentToolExecutor>.Instance;
    }

    /// <summary>
    /// Sets whether the agent is in plan mode.
    /// </summary>
    public void SetPlanMode(bool isPlanMode)
    {
        _isPlanMode = isPlanMode;
    }

    /// <summary>
    /// Executes a tool by name with the given parameters.
    /// </summary>
    public async Task<ToolResult> ExecuteAsync(string toolName, Dictionary<string, object> parameters)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Check tool availability
            if (!_toolRegistry.IsToolAvailable(toolName, _isPlanMode))
                return ToolResult.Fail($"Tool '{toolName}' is not available in the current mode.");

            var workingDir = parameters.TryGetValue("workingDirectory", out var wd)
                ? wd?.ToString() ?? string.Empty
                : string.Empty;

            var agentIgnoreRules = parameters.TryGetValue("agentIgnoreRules", out var rules)
                ? rules as IReadOnlyList<AgentIgnoreRule>
                : null;

            var result = toolName switch
            {
                "write_to_file" => await ExecuteWriteFile(parameters),
                "replace_in_file" => await ExecuteReplaceFile(parameters),
                "read_file" => await ExecuteReadFile(parameters),
                "search_files" => await ExecuteSearchFiles(parameters),
                "list_files" => await ExecuteListFiles(parameters),
                "execute_command" => await ExecuteCommand(parameters),
                "browser_action" => await ExecuteBrowserAction(parameters),
                "use_mcp_tool" => await ExecuteUseMcpTool(parameters),
                "access_mcp_resource" => await ExecuteAccessMcpResource(parameters),
                "load_mcp_documentation" => await ExecuteLoadMcpDocumentation(),
                "plan_mode_respond" => await ExecutePlanModeRespond(parameters),
                "act_mode_respond" => await ExecuteActModeRespond(parameters),
                "attempt_completion" => await ExecuteAttemptCompletion(parameters),
                "new_task" => await ExecuteNewTask(parameters),
                "use_skill" => await ExecuteUseSkill(parameters),
                "use_subagents" => await ExecuteUseSubagents(parameters),
                "apply_patch" => await ExecuteApplyPatch(parameters),
                "generate_explanation" => await ExecuteGenerateExplanation(parameters),
                "web_fetch" => await ExecuteWebFetch(parameters),
                "web_search" => await ExecuteWebSearch(parameters),
                "ask_followup_question" => await ExecuteAskFollowupQuestion(parameters),
                _ => ToolResult.Fail($"Unknown tool: {toolName}"),
            };

            result = result with { DurationMs = stopwatch.ElapsedMilliseconds };

            _logger?.LogDebug("AgentToolExecutor: Executed '{ToolName}' in {Duration}ms", toolName, result.DurationMs);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ToolResult.Fail($"Error executing tool '{toolName}': {ex.Message}")
                with
            { DurationMs = stopwatch.ElapsedMilliseconds };
        }
    }

    /// <summary>
    /// Lists all available tools.
    /// </summary>
    public IReadOnlyCollection<ToolDefinition> ListAvailableTools()
        => _toolRegistry.ListTools();

    /// <summary>
    /// Checks if a tool is available in the current mode.
    /// </summary>
    public bool IsToolAvailable(string toolName, bool isPlanMode)
        => _toolRegistry.IsToolAvailable(toolName, isPlanMode);

    // --- Tool implementations ---

    private async Task<ToolResult> ExecuteWriteFile(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("path", out var path))
            return ToolResult.Fail("Missing required parameter: path");
        if (!parameters.TryGetValue("content", out var content))
            return ToolResult.Fail("Missing required parameter: content");

        return await _fileSystem.WriteFileAsync(
            path?.ToString() ?? string.Empty,
            content?.ToString() ?? string.Empty,
            parameters.TryGetValue("workingDirectory", out var wd) ? (wd?.ToString() ?? string.Empty) : string.Empty,
            parameters.TryGetValue("agentIgnoreRules", out var rules) ? rules as IReadOnlyList<AgentIgnoreRule> : null);
    }

    private async Task<ToolResult> ExecuteReplaceFile(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("path", out var path))
            return ToolResult.Fail("Missing required parameter: path");
        if (!parameters.TryGetValue("diff", out var diff))
            return ToolResult.Fail("Missing required parameter: diff");

        var diffText = diff?.ToString() ?? string.Empty;
        var blocks = diffText.Split(new[] { "------- SEARCH" }, StringSplitOptions.RemoveEmptyEntries);

        return await _fileSystem.ReplaceInFileAsync(
            path?.ToString() ?? string.Empty,
            blocks,
            parameters.TryGetValue("workingDirectory", out var wd) ? (wd?.ToString() ?? string.Empty) : string.Empty,
            parameters.TryGetValue("agentIgnoreRules", out var rules) ? rules as IReadOnlyList<AgentIgnoreRule> : null);
    }

    private async Task<ToolResult> ExecuteReadFile(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("path", out var path))
            return ToolResult.Fail("Missing required parameter: path");

        // startLine and endLine are optional — default to 1 and int.MaxValue (read entire file)
        var startLine = parameters.TryGetValue("startLine", out var sl)
            ? Convert.ToInt32(sl)
            : 1;
        var endLine = parameters.TryGetValue("endLine", out var el)
            ? Convert.ToInt32(el)
            : int.MaxValue;

        return await _fileSystem.ReadFileAsync(
            path?.ToString() ?? string.Empty,
            startLine,
            endLine,
            parameters.TryGetValue("workingDirectory", out var wd) ? (wd?.ToString() ?? string.Empty) : string.Empty,
            parameters.TryGetValue("agentIgnoreRules", out var rules) ? rules as IReadOnlyList<AgentIgnoreRule> : null);
    }

    private async Task<ToolResult> ExecuteSearchFiles(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("path", out var path))
            return ToolResult.Fail("Missing required parameter: path");
        if (!parameters.TryGetValue("regex", out var regex))
            return ToolResult.Fail("Missing required parameter: regex");

        return await _fileSystem.SearchFilesAsync(
            path?.ToString() ?? string.Empty,
            regex?.ToString() ?? string.Empty,
            parameters.TryGetValue("filePattern", out var fp) ? fp?.ToString() : null,
            parameters.TryGetValue("workingDirectory", out var wd) ? (wd?.ToString() ?? string.Empty) : string.Empty,
            parameters.TryGetValue("agentIgnoreRules", out var rules) ? rules as IReadOnlyList<AgentIgnoreRule> : null);
    }

    private async Task<ToolResult> ExecuteListFiles(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("path", out var path))
            return ToolResult.Fail("Missing required parameter: path");

        return await _fileSystem.ListFilesAsync(
            path?.ToString() ?? string.Empty,
            parameters.TryGetValue("recursive", out var recursive) && recursive is bool r && r,
            parameters.TryGetValue("workingDirectory", out var wd) ? (wd?.ToString() ?? string.Empty) : string.Empty,
            parameters.TryGetValue("agentIgnoreRules", out var rules) ? rules as IReadOnlyList<AgentIgnoreRule> : null);
    }

    private async Task<ToolResult> ExecuteCommand(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("command", out var command))
            return ToolResult.Fail("Missing required parameter: command");
        // Support both camelCase and snake_case parameter names for compatibility
        if (!parameters.TryGetValue("requiresApproval", out var requiresApproval) &&
            !parameters.TryGetValue("requires_approval", out requiresApproval))
            return ToolResult.Fail("Missing required parameter: requires_approval");

        return await _commandExecutor.ExecuteAsync(
            command?.ToString() ?? string.Empty,
            requiresApproval is bool ra && ra,
            parameters.TryGetValue("timeout", out var timeout) ? Convert.ToInt32(timeout) : null,
            parameters.TryGetValue("workingDirectory", out var wd) ? (wd?.ToString() ?? string.Empty) : null);
    }

    private async Task<ToolResult> ExecuteBrowserAction(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("action", out var action))
            return ToolResult.Fail("Missing required parameter: action");

        return await _browserService.ActionAsync(
            action?.ToString() ?? string.Empty,
            parameters.TryGetValue("url", out var url) ? url?.ToString() : null,
            parameters.TryGetValue("coordinate", out var coord) ? coord?.ToString() : null,
            parameters.TryGetValue("text", out var text) ? text?.ToString() : null);
    }

    private async Task<ToolResult> ExecuteUseMcpTool(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("serverName", out var serverName))
            return ToolResult.Fail("Missing required parameter: server_name");
        if (!parameters.TryGetValue("toolName", out var toolName))
            return ToolResult.Fail("Missing required parameter: tool_name");
        if (!parameters.TryGetValue("arguments", out var arguments))
            return ToolResult.Fail("Missing required parameter: arguments");

        // The arguments parameter should be a JSON string per the IMcpService interface.
        // If it's a Dictionary, serialize it to JSON. If it's already a string, use it as-is.
        string argsJson;
        if (arguments is Dictionary<string, object> dict)
        {
            argsJson = System.Text.Json.JsonSerializer.Serialize(dict);
        }
        else if (arguments?.ToString() != null)
        {
            argsJson = arguments.ToString()!;
        }
        else
        {
            argsJson = "{}";
        }

        return await _mcpService.UseToolAsync(
            serverName?.ToString() ?? string.Empty,
            toolName?.ToString() ?? string.Empty,
            argsJson);
    }

    private async Task<ToolResult> ExecuteAccessMcpResource(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("serverName", out var serverName))
            return ToolResult.Fail("Missing required parameter: server_name");
        if (!parameters.TryGetValue("uri", out var uri))
            return ToolResult.Fail("Missing required parameter: uri");

        return await _mcpService.AccessResourceAsync(
            serverName?.ToString() ?? string.Empty,
            uri?.ToString() ?? string.Empty);
    }

    private async Task<ToolResult> ExecuteLoadMcpDocumentation()
        => await _mcpService.LoadDocumentationAsync();

    private async Task<ToolResult> ExecutePlanModeRespond(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("response", out var response))
            return ToolResult.Fail("Missing required parameter: response");

        var resp = response?.ToString() ?? string.Empty;
        var needsMore = parameters.TryGetValue("needsMoreExploration", out var nme)
            && (nme as bool?) == true;

        return ToolResult.Ok(needsMore
            ? $"Response: {resp}\n[More exploration needed.]"
            : $"Response: {resp}");
    }

    private async Task<ToolResult> ExecuteActModeRespond(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("response", out var response))
            return ToolResult.Fail("Missing required parameter: response");

        return ToolResult.Ok($"Progress update: {response}");
    }

    private async Task<ToolResult> ExecuteAttemptCompletion(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("result", out var result))
            return ToolResult.Fail("Missing required parameter: result");

        var command = parameters.TryGetValue("command", out var cmd) ? cmd?.ToString() : null;
        var output = $"Task completed: {result}";

        if (!string.IsNullOrEmpty(command))
            output += $"\n\n[Command to run: {command}]";

        return ToolResult.Ok(output);
    }

    private async Task<ToolResult> ExecuteNewTask(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("context", out var context))
            return ToolResult.Fail("Missing required parameter: context");

        return ToolResult.Ok($"New task created with context:\n\n{context}");
    }

    private async Task<ToolResult> ExecuteUseSkill(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("skillName", out var skillName))
            return ToolResult.Fail("Missing required parameter: skill_name");

        return ToolResult.Ok($"Skill '{skillName}' activated.");
    }

    private async Task<ToolResult> ExecuteUseSubagents(Dictionary<string, object> parameters)
    {
        var prompts = new List<string>();
        for (var i = 1; i <= 5; i++)
        {
            var key = $"prompt_{i}";
            if (parameters.TryGetValue(key, out var p) && p?.ToString() != null)
                prompts.Add(p.ToString()!);
        }

        if (prompts.Count == 0)
            return ToolResult.Fail("Missing required parameter: prompt_1");
        if (prompts.Count > 5)
            return ToolResult.Fail("Too many prompts (max 5).");

        return ToolResult.Ok($"Running {prompts.Count} subagent(s) in parallel...\n\n{string.Join("\n", prompts.Select((p, i) => $"  Subagent {i + 1}: {p}"))}");
    }

    private async Task<ToolResult> ExecuteApplyPatch(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("input", out var input))
            return ToolResult.Fail("Missing required parameter: input");

        return await _patchService.ApplyPatchAsync(
            input?.ToString() ?? string.Empty,
            parameters.TryGetValue("workingDirectory", out var wd) ? (wd?.ToString() ?? string.Empty) : string.Empty);
    }

    private async Task<ToolResult> ExecuteGenerateExplanation(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("title", out var title))
            return ToolResult.Fail("Missing required parameter: title");
        if (!parameters.TryGetValue("fromRef", out var fromRef))
            return ToolResult.Fail("Missing required parameter: from_ref");

        var toRef = parameters.TryGetValue("toRef", out var tr) ? tr?.ToString() : null;

        return ToolResult.Ok($"Explanation for '{title}'\n\nFrom: {fromRef}\nTo: {toRef ?? "(current)"}");
    }

    private async Task<ToolResult> ExecuteWebFetch(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("url", out var url))
            return ToolResult.Fail("Missing required parameter: url");
        if (!parameters.TryGetValue("prompt", out var prompt))
            return ToolResult.Fail("Missing required parameter: prompt");

        return await _webFetchService.FetchAsync(
            url?.ToString() ?? string.Empty,
            prompt?.ToString() ?? string.Empty);
    }

    private async Task<ToolResult> ExecuteWebSearch(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("query", out var query))
            return ToolResult.Fail("Missing required parameter: query");

        return await _webSearchService.SearchAsync(
            query?.ToString() ?? string.Empty,
            parameters.TryGetValue("allowedDomains", out var ad) ? ad?.ToString() : null,
            parameters.TryGetValue("blockedDomains", out var bd) ? bd?.ToString() : null);
    }

    private async Task<ToolResult> ExecuteAskFollowupQuestion(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("question", out var question))
            return ToolResult.Fail("Missing required parameter: question");

        var options = parameters.TryGetValue("options", out var opts)
            ? opts as List<string>
            : null;

        return await _questionService.AskAsync(
            question?.ToString() ?? string.Empty,
            options);
    }

    public async Task<string> ExecuteCommandAsync(string command, int timeoutSeconds, Dictionary<string, object>? options)
    {
        try
        {
            var workingDir = options?.TryGetValue("workingDirectory", out var wd) == true ? (wd?.ToString() ?? string.Empty) : null;
            var result = await _commandExecutor.ExecuteAsync(command, false, timeoutSeconds, workingDir);
            return result.Output ?? string.Empty;
        }
        catch (Exception ex)
        {
            return $"Error executing command: {ex.Message}";
        }
    }

    public async Task<bool> CancelRunningCommandAsync()
    {
        try
        {
            await _commandExecutor.CancelAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
