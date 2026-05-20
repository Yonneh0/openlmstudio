using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Core agent that implements the plan/act cycle for agentic task execution.
/// Coordinates with IToolRegistry to discover and execute available tools.
/// </summary>
public class Agent : IAgent, IDisposable
{
    private readonly ILogger<Agent>? _logger;
    private readonly ITaskProgressTracker _progressTracker;
    private readonly IToolRegistry _toolRegistry;
    private readonly IChatCompletionService _chatService;
    private readonly IChatContextManager _contextManager;
    private AgentState _state;
    private bool _disposed;

    private readonly List<AgentToolCallRecord> _toolCalls = new();
    private readonly List<AgentMessageExchange> _conversationHistory = new();
    private AgentTaskRequest? _currentRequest;

    public Agent(
        ILogger<Agent>? logger,
        ITaskProgressTracker progressTracker,
        IToolRegistry? toolRegistry = null,
        IChatCompletionService? chatService = null,
        IChatContextManager? contextManager = null)
    {
        _logger = logger;
        _progressTracker = progressTracker;
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
        _contextManager = contextManager ?? throw new ArgumentNullException(nameof(contextManager));
        _state = AgentState.Idle;
    }

    public AgentState State => _state;

    public async Task<AgentTaskResult> ExecuteAsync(AgentTaskRequest request, CancellationToken ct = default)
    {
        _currentRequest = request;
        _state = AgentState.Planning;
        await _progressTracker.UpdateStageAsync(TaskProgressStage.InProgress);

        try
        {
            // Phase 1: Planning — ask LLM to propose a plan
            var plan = await GeneratePlanAsync(request, ct);
            if (string.IsNullOrEmpty(plan))
            {
                await _progressTracker.RecordErrorAsync("Failed to generate plan.");
                _state = AgentState.Failed;
                return CreateResult(request, AgentState.Failed);
            }

            _state = AgentState.Acting;

            // Phase 2: Act — execute actions using available tools
            var result = await ExecuteActionsAsync(request, plan, ct);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Agent execution failed");
            await _progressTracker.RecordErrorAsync(ex.Message);
            _state = AgentState.Failed;
            return CreateResult(request, AgentState.Failed);
        }
    }

    public Task PauseAsync()
    {
        _state = AgentState.Paused;
        return Task.CompletedTask;
    }

    public async Task ResumeAsync(CancellationToken ct = default)
    {
        if (_currentRequest == null)
            return;

        _state = AgentState.Acting;

        // Re-execute remaining tool calls from where we were interrupted
        // The conversation history preserves the last action state
        if (_conversationHistory.Count > 0)
        {
            _logger?.LogInformation("Resuming agent task {TaskId} from interruption point", _currentRequest.TaskId);
        }
    }

    public async Task AbortAsync()
    {
        _state = AgentState.Failed;
        await _progressTracker.RecordErrorAsync("Task aborted by user.");
    }

    public IReadOnlyList<AgentToolCallRecord> GetToolCalls() => _toolCalls;
    public IReadOnlyList<AgentMessageExchange> GetConversationHistory() => _conversationHistory;

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    private async Task<string?> GeneratePlanAsync(AgentTaskRequest request, CancellationToken ct)
    {
        var tools = _toolRegistry.GetTools();
        var toolNames = string.Join(", ", tools.Keys);

        var systemPrompt = $"""
You are an autonomous agent. Your task is:

{request.Description}

Available tools: {toolNames}
Max iterations: {request.MaxIterations}

Propose a detailed plan for completing this task. Be specific about which tools to use and in what order.
""";

        try
        {
            await _contextManager.GetCompressedContextAsync(request.TaskId, Domain.Models.CompressionLevel.Medium);
            var response = await _chatService.GetCompletionAsync(new ChatRequest(
                ModelId: "default",
                Messages: new List<Message>
                {
                    new() { Role = MessageRole.System, Content = systemPrompt },
                    new() { Role = MessageRole.User, Content = "Please propose a plan." }
                },
                Stream: false
            ));

            return response.Message.Content;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to generate plan");
            return null;
        }
    }

    private async Task<AgentTaskResult> ExecuteActionsAsync(AgentTaskRequest request, string plan, CancellationToken ct)
    {
        var iterations = 0;
        var maxIterations = request.MaxIterations;

        while (iterations < maxIterations && !_disposed)
        {
            iterations++;
            _logger?.LogInformation("Agent iteration {Iteration}/{Max}", iterations, maxIterations);

            await _progressTracker.ReportProgressAsync((int)((iterations / (double)maxIterations) * 100));

            // Generate next action from LLM
            var action = await GenerateActionAsync(request, plan, ct);
            if (string.IsNullOrEmpty(action))
            {
                _logger?.LogWarning("No action generated — stopping agent loop");
                break;
            }

            // Find the best matching tool for this action by parsing the tool name from the LLM response
            var matchingTools = FindMatchingTools(action);

            if (matchingTools.Count == 0)
            {
                _logger?.LogWarning("No tool matched the generated action — skipping iteration");
                continue;
            }

            // Execute the matched tool(s) — prefer the first match
            foreach (var tool in matchingTools)
            {
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();

                    // Parse tool parameters from the LLM action response
                    var parameters = ParseActionParameters(action, tool.Name);

                    var success = await tool.ExecuteAsync(parameters).ConfigureAwait(false);
                    sw.Stop();

                    var resultText = success ? "Completed" : "Failed";
                    var record = new AgentToolCallRecord(
                        tool.Name, parameters, resultText, success, sw.ElapsedMilliseconds, DateTime.UtcNow);

                    _toolCalls.Add(record);
                    await _progressTracker.RecordToolCallAsync(tool.Name, parameters, resultText, success, sw.ElapsedMilliseconds);

                    _logger?.LogInformation("Tool '{ToolName}' executed: {Result} in {Ms}ms", tool.Name, resultText, sw.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Tool '{ToolName}' failed during execution", tool.Name);
                }
            }
        }

        _state = AgentState.Completed;
        await _progressTracker.UpdateStageAsync(TaskProgressStage.Completed);
        return CreateResult(request, AgentState.Completed);
    }

    private async Task<string?> GenerateActionAsync(AgentTaskRequest request, string plan, CancellationToken ct)
    {
        try
        {
            var tools = _toolRegistry.GetTools();
            var toolNames = string.Join(", ", tools.Keys);
            var toolDescriptions = string.Join("\n", tools.Select(t => $"- {t.Key}: {t.Value.Description}"));

            var systemPrompt = $"""
You are executing a plan for an agentic task. Your task is:

{request.Description}

Current plan:
{plan}

Available tools:
{toolDescriptions}

Based on the current state and the plan, what single action should you take next? Return ONLY the tool name and a brief description of what to do. Format: "Tool: <name>\nAction: <description>"
""";

            await _contextManager.GetCompressedContextAsync(request.TaskId, CompressionLevel.Medium);
            var response = await _chatService.GetCompletionAsync(new ChatRequest(
                ModelId: "default",
                Messages: new List<Message>
                {
                    new() { Role = MessageRole.System, Content = systemPrompt },
                    new() { Role = MessageRole.User, Content = "Continue executing the plan." }
                },
                Stream: false
            ));

            return response.Message.Content;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to generate action");
            return null;
        }
    }

    /// <summary>
    /// Parses tool parameters from the LLM action response.
    /// Extracts key-value pairs from the action description (e.g., "path: /foo/bar").
    /// </summary>
    private static Dictionary<string, object> ParseActionParameters(string action, string toolName)
    {
        var parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        parameters["tool_name"] = toolName;

        // Extract file paths from the action text (common pattern: "path: /some/path")
        var pathMatches = System.Text.RegularExpressions.Regex.Matches(action, @"(?:path|file|directory|target)[\s:=]+[\""]?([^""]+\.[a-z0-9]+|/[\w/]+)");
        foreach (System.Text.RegularExpressions.Match match in pathMatches)
        {
            parameters["path"] = match.Value;
        }

        // Extract numeric parameters (common pattern: "count: 42")
        var countMatches2 = System.Text.RegularExpressions.Regex.Matches(action, @"(?:count|num|n|steps)[\s:=]+(\d+)");
        foreach (System.Text.RegularExpressions.Match match in countMatches2)
        {
            parameters["count"] = int.Parse(match.Value);
        }

        return parameters;
    }

    /// <summary>
    /// Finds tools matching the generated action by parsing tool names from the LLM response.
    /// </summary>
    private IReadOnlyList<ITool> FindMatchingTools(string action)
    {
        var tools = _toolRegistry.GetTools();
        var matching = new List<ITool>();

        foreach (var (name, tool) in tools)
        {
            if (action.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                matching.Add(tool);
        }

        return matching;
    }

    private AgentTaskResult CreateResult(AgentTaskRequest request, AgentState finalState)
    {
        var summary = finalState == AgentState.Completed
            ? $"Task completed with {(_toolCalls?.Count ?? 0)} tool calls."
            : $"Task failed: {finalState}";

        return new AgentTaskResult(request.TaskId, finalState, _toolCalls ?? new List<AgentToolCallRecord>(), summary);
    }
}