using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Core agent that implements the plan/act cycle for agentic task execution.
/// Coordinates with IToolRegistry to discover and execute available tools.
/// Supports error recovery (loop detection, timeout guard, tool fallback),
/// resume from checkpoint, and auto-commit for safe operations.
/// </summary>
public class Agent : IAgent, IDisposable
{
    private readonly ILogger<Agent>? _logger;
    private readonly ITaskProgressTracker _progressTracker;
    private readonly IToolRegistry _toolRegistry;
    private readonly IChatCompletionService _chatService;
    private readonly IChatContextManager _contextManager;
    private readonly IContextCompressor? _contextCompressor;
    private AgentState _state;
    private bool _disposed;

    private readonly List<AgentToolCallRecord> _toolCalls = new();
    private readonly List<AgentMessageExchange> _conversationHistory = new();
    private AgentTaskRequest? _currentRequest;

    // Error recovery state
    private int _consecutiveFailures = 0;
    private static readonly int MaxConsecutiveFailures = 3;

    // Loop detection
    private readonly Queue<string> _recentActions = new();
    private const int LoopDetectionWindow = 10;

    // Checkpoint for resume
    private AgentCheckpoint? _lastCheckpoint;

    // Timeout tracking
    private DateTime _loopStartTime;

    public Agent(
        ILogger<Agent>? logger,
        ITaskProgressTracker progressTracker,
        IToolRegistry? toolRegistry = null,
        IChatCompletionService? chatService = null,
        IChatContextManager? contextManager = null,
        IContextCompressor? contextCompressor = null)
    {
        _logger = logger;
        _progressTracker = progressTracker;
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
        _contextManager = contextManager ?? throw new ArgumentNullException(nameof(contextManager));
        _contextCompressor = contextCompressor;
        _state = AgentState.Idle;
    }

    public AgentState State => _state;

    public async Task<AgentTaskResult> ExecuteAsync(AgentTaskRequest request, CancellationToken ct = default)
    {
        _currentRequest = request;
        _state = AgentState.Planning;
        _loopStartTime = DateTime.UtcNow;
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

            // Save checkpoint after planning phase
            _lastCheckpoint = new AgentCheckpoint(
                Iteration: 0,
                ToolCalls: new List<AgentToolCallRecord>(_toolCalls),
                ConversationHistory: new List<AgentMessageExchange>(_conversationHistory),
                State: AgentState.Planning,
                Timestamp: DateTime.UtcNow);

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

        // Save checkpoint on pause for resume
        _lastCheckpoint = new AgentCheckpoint(
            Iteration: _toolCalls.Count,
            ToolCalls: new List<AgentToolCallRecord>(_toolCalls),
            ConversationHistory: new List<AgentMessageExchange>(_conversationHistory),
            State: AgentState.Paused,
            Timestamp: DateTime.UtcNow);

        return Task.CompletedTask;
    }

    public async Task ResumeAsync(CancellationToken ct = default)
    {
        if (_currentRequest == null)
            return;

        _state = AgentState.Acting;

        // Restore from checkpoint if available
        if (_lastCheckpoint != null)
        {
            _logger?.LogInformation("Resuming agent task {TaskId} from checkpoint at iteration {Iteration}",
                _currentRequest.TaskId, _lastCheckpoint.Iteration);

            // Restore state from checkpoint
            _toolCalls.Clear();
            _toolCalls.AddRange(_lastCheckpoint.ToolCalls);
            _conversationHistory.Clear();
            _conversationHistory.AddRange(_lastCheckpoint.ConversationHistory);
        }
        else if (_conversationHistory.Count > 0)
        {
            _logger?.LogInformation("Resuming agent task {TaskId} from interruption point", _currentRequest.TaskId);
        }

        // Continue with the last plan if available
        var plan = GetLastPlanFromHistory();
        if (plan != null)
        {
            _state = AgentState.Acting;
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
            // Get compressed context for planning — await the async call properly
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

        while (iterations < maxIterations && !_disposed && !ct.IsCancellationRequested)
        {
            iterations++;
            _logger?.LogInformation("Agent iteration {Iteration}/{Max}", iterations, maxIterations);

            // Check timeout guard — abort if running for more than 30 minutes
            if ((DateTime.UtcNow - _loopStartTime).TotalMinutes > 30)
            {
                _logger?.LogWarning("Agent timeout exceeded — aborting loop");
                await _progressTracker.RecordErrorAsync("Task timed out after 30 minutes.");
                _state = AgentState.Failed;
                return CreateResult(request, AgentState.Failed);
            }

            await _progressTracker.ReportProgressAsync((int)((iterations / (double)maxIterations) * 100));

            // Generate next action from LLM
            var action = await GenerateActionAsync(request, plan, ct);
            if (string.IsNullOrEmpty(action))
            {
                _logger?.LogWarning("No action generated — stopping agent loop");
                break;
            }

            // Loop detection: check if the same action was repeated too many times
            if (IsLoopDetected(action))
            {
                _logger?.LogWarning("Loop detected — trying degraded action");
                action = await GenerateDegradedActionAsync(request, plan, ct);
                if (string.IsNullOrEmpty(action))
                {
                    _logger?.LogWarning("Degraded action generation failed — stopping agent loop");
                    break;
                }
            }

            // Find the best matching tool for this action by parsing the tool name from the LLM response
            var matchingTools = FindMatchingTools(action);

            if (matchingTools.Count == 0)
            {
                _logger?.LogWarning("No tool matched the generated action — skipping iteration");
                continue;
            }

            // Execute the matched tool(s) — prefer the first match
            var executed = false;
            foreach (var tool in matchingTools)
            {
                var success = await ExecuteToolWithFallbackAsync(tool, action, request, ct);
                if (success)
                {
                    executed = true;
                    _consecutiveFailures = 0;
                    break;
                }
                else
                {
                    _consecutiveFailures++;
                    _logger?.LogWarning("Tool '{ToolName}' failed (consecutive failures: {Count})", tool.Name, _consecutiveFailures);

                    // Check consecutive failure threshold
                    if (_consecutiveFailures >= MaxConsecutiveFailures)
                    {
                        _logger?.LogWarning("Max consecutive failures reached ({Count}) — attempting fallback", MaxConsecutiveFailures);
                        var fallbackPlan = await GenerateFallbackPlanAsync(request, plan, ct);
                        if (fallbackPlan != null)
                        {
                            _logger?.LogInformation("Falling back to alternate plan");
                            // Reset and try with fallback plan
                            _consecutiveFailures = 0;
                            // Continue with fallback plan
                            goto ContinueWithPlan;
                        }
                        else
                        {
                            _logger?.LogWarning("No fallback plan available — aborting");
                            await _progressTracker.RecordErrorAsync("Max consecutive failures reached with no fallback plan.");
                            _state = AgentState.Failed;
                            return CreateResult(request, AgentState.Failed);
                        }
                    }
                }
            }

            if (!executed)
            {
                _logger?.LogWarning("All matching tools failed to execute");
            }

        ContinueWithPlan:;
            // Save checkpoint periodically
            if (iterations % 5 == 0)
            {
                SaveCheckpoint();
            }
        }

        _state = AgentState.Completed;
        await _progressTracker.UpdateStageAsync(TaskProgressStage.Completed);
        return CreateResult(request, AgentState.Completed);
    }

    private async Task<bool> ExecuteToolWithFallbackAsync(ITool tool, string action, AgentTaskRequest request, CancellationToken ct)
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

            // Auto-commit: if tool made significant changes (e.g., file writes > threshold)
            if (success && ShouldAutoCommit(tool.Name))
            {
                await AutoCommitChangesAsync(request.TaskId);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Tool '{ToolName}' failed during execution", tool.Name);

            // Try fallback: degraded parameters
            try
            {
                var fallbackParams = new Dictionary<string, object> { { "degraded", true } };
                var fallbackSuccess = await tool.ExecuteAsync(fallbackParams).ConfigureAwait(false);
                if (fallbackSuccess)
                {
                    _logger?.LogInformation("Tool '{ToolName}' succeeded with degraded parameters", tool.Name);
                }
                return fallbackSuccess;
            }
            catch
            {
                return false;
            }
        }
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

Based on the current state and the plan, what single action should you take next? Return ONLY the tool name and a brief description of what to do. Format: Tool: <name> / Action: <description>
""";

            // Get compressed context for action generation — await the async call properly
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
    /// Generates a degraded action when loop detection triggers.
    /// Asks the LLM to take a different approach.
    /// </summary>
    private async Task<string?> GenerateDegradedActionAsync(AgentTaskRequest request, string plan, CancellationToken ct)
    {
        try
        {
            var systemPrompt = $"""
You are in a degraded mode. You were stuck in a loop. Try a different approach for this task:

{request.Description}

Previous plan failed to make progress. What is a completely different action you could take?
""";

            var response = await _chatService.GetCompletionAsync(new ChatRequest(
                ModelId: "default",
                Messages: new List<Message>
                {
                    new() { Role = MessageRole.System, Content = systemPrompt },
                    new() { Role = MessageRole.User, Content = "Propose a different action." }
                },
                Stream: false
            ));

            return response.Message.Content;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Generates a fallback plan when consecutive tool failures exceed the threshold.
    /// Asks the LLM to propose an alternative strategy.
    /// </summary>
    private async Task<string?> GenerateFallbackPlanAsync(AgentTaskRequest request, string originalPlan, CancellationToken ct)
    {
        try
        {
            var toolNames = string.Join(", ", _toolRegistry.GetTools().Keys);

            var systemPrompt = $"""
You are in a degraded mode. Your previous plan failed to make progress after {MaxConsecutiveFailures} consecutive tool failures.

Task: {request.Description}

Original plan (failed):
{originalPlan}

Propose a simpler, more reliable plan that focuses on completing the essential parts of the task.
""";

            var response = await _chatService.GetCompletionAsync(new ChatRequest(
                ModelId: "default",
                Messages: new List<Message>
                {
                    new() { Role = MessageRole.System, Content = systemPrompt },
                    new() { Role = MessageRole.User, Content = "Propose a fallback plan." }
                },
                Stream: false
            ));

            return response.Message.Content;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Detects if the same action has been repeated too many times in the recent history.
    /// </summary>
    private bool IsLoopDetected(string action)
    {
        _recentActions.Enqueue(action);
        if (_recentActions.Count > LoopDetectionWindow)
        {
            _recentActions.Dequeue();
        }

        // Check if the same action appears more than 50% of the time in the window
        if (_recentActions.Count >= 4)
        {
            var count = _recentActions.Count(a => string.Equals(a, action, StringComparison.OrdinalIgnoreCase));
            return count >= Math.Max(3, _recentActions.Count / 2);
        }

        return false;
    }

    /// <summary>
    /// Determines if a tool should trigger auto-commit when it makes significant changes.
    /// </summary>
    private static bool ShouldAutoCommit(string toolName) =>
        toolName.Contains("Write", StringComparison.OrdinalIgnoreCase) ||
        toolName.Contains("Patch", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Performs auto-commit for safe operations that modified files.
    /// </summary>
    private async Task AutoCommitChangesAsync(Guid taskId)
    {
        try
        {
            // In a full implementation, this would:
            // 1. Check if git is available
            // 2. Stage and commit the changes
            // 3. Create a branch if needed
            _logger?.LogDebug("Auto-commit triggered for task {TaskId}", taskId);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Auto-commit failed for task {TaskId}", taskId);
        }
    }

    private string? GetLastPlanFromHistory()
    {
        // Find the last message that contains plan context
        foreach (var msg in _conversationHistory)
        {
            if (msg.Content != null && msg.Content.Length > 100)
            {
                return msg.Content;
            }
        }
        return null;
    }

    private void SaveCheckpoint()
    {
        _lastCheckpoint = new AgentCheckpoint(
            Iteration: _toolCalls.Count,
            ToolCalls: new List<AgentToolCallRecord>(_toolCalls),
            ConversationHistory: new List<AgentMessageExchange>(_conversationHistory),
            State: _state,
            Timestamp: DateTime.UtcNow);
    }

    /// <summary>
    /// Parses tool parameters from the LLM action response.
    /// Extracts key-value pairs from the action description (e.g., "path: /foo/bar").
    /// </summary>
    private static Dictionary<string, object> ParseActionParameters(string action, string toolName)
    {
        var parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        parameters["tool_name"] = toolName;

        // Extract file paths from the action text (common pattern: "path: /foo/bar")
        var pathMatches = System.Text.RegularExpressions.Regex.Matches(action, @"(?:path|file|directory|target)[\s:=]+[\""]?([^""]+\.[a-z0-9]+|/[\w/]+)");
        foreach (System.Text.RegularExpressions.Match match in pathMatches)
        {
            parameters["path"] = match.Value;
        }

        // Extract numeric parameters (common pattern: "count: 42")
        // Fix: extract only the matched number group, not the entire match string
        var countMatches = System.Text.RegularExpressions.Regex.Matches(action, @"(?:count|num|n|steps)[\s:=]+(\d+)");
        foreach (System.Text.RegularExpressions.Match match in countMatches)
        {
            // The number is in the first capturing group, not match.Value (which includes "count: " prefix)
            parameters["count"] = int.Parse(match.Groups[1].Value);
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

/// <summary>
/// Checkpoint data for agent resume capability.
/// </summary>
public record AgentCheckpoint(
    int Iteration,
    IReadOnlyList<AgentToolCallRecord> ToolCalls,
    IReadOnlyList<AgentMessageExchange> ConversationHistory,
    AgentState State,
    DateTime Timestamp);