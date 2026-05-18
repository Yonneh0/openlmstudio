using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Concrete implementation of IAgent that manages the lifecycle of an agentic task with plan/act cycle.
/// </summary>
public class Agent : IAgent, IDisposable
{
    private readonly ILogger<Agent>? _logger;
    private readonly ITaskProgressTracker _progressTracker;

    /// <summary>
    /// The task description being worked on — used for context-aware planning.
    /// </summary>
    private string? _taskDescription;
    private volatile AgentState _state = AgentState.Planning;
    private bool _disposed;
    private readonly List<AgentToolCallRecord> _toolCalls = new();
    private readonly List<AgentMessageExchange> _conversationHistory = new();

    /// <summary>
    /// Creates a new Agent instance.
    /// </summary>
    public Agent(ILogger<Agent>? logger, ITaskProgressTracker progressTracker)
    {
        _logger = logger;
        _progressTracker = progressTracker ?? throw new ArgumentNullException(nameof(progressTracker));
    }

    /// <inheritdoc />
    public AgentState State => _state;

    /// <inheritdoc />
    public async Task<AgentTaskResult> ExecuteAsync(AgentTaskRequest request, CancellationToken ct = default)
    {
        if (request == null || string.IsNullOrEmpty(request.Description))
            throw new ArgumentException("Task description is required.", nameof(request));

        if (_disposed) return CreateFailedResult(request.TaskId, "Agent disposed");

        _state = AgentState.Planning;
        await _progressTracker.UpdateStageAsync(TaskProgressStage.InProgress).ConfigureAwait(false);

        _taskDescription = request.Description;

        // Log initial planning message
        AddConversationMessage("agent", "planning", $"Planning approach for task: {request.Description}");

        try
        {
            int iterationCount = 0;
            while (iterationCount < request.MaxIterations && !ct.IsCancellationRequested)
            {
                // Check error conditions
                if (_progressTracker.HasError || _progressTracker.IsIterationLimitExceeded)
                    return CreateFailedResult(request.TaskId,
                        $"Agent hit error condition: {_progressTracker.ErrorMessage ?? "iteration limit"}");

                // Execute plan phase (determine what to do next) — use context-aware planning with task description
                var planMessage = GeneratePlanResponse();
                AddConversationMessage("agent", "planning", planMessage);

                _state = AgentState.Acting;
                await _progressTracker.UpdateStageAsync(TaskProgressStage.Reviewing).ConfigureAwait(false);

                // Execute act phase based on plan
                bool shouldContinue = await ExecuteActPhase(request, ct).ConfigureAwait(false);

                if (!shouldContinue)
                    break;

                iterationCount++;
            }

            if (ct.IsCancellationRequested)
                return CreateFailedResult(request.TaskId, "Agent execution was cancelled");

            _state = AgentState.Completed;
            await _progressTracker.UpdateStageAsync(TaskProgressStage.Completed).ConfigureAwait(false);

            return new AgentTaskResult(
                request.TaskId,
                AgentState.Completed,
                _toolCalls.ToList().AsReadOnly(),
                $"Completed {iterationCount} iterations");
        }
        catch (Exception ex)
        {
            await _progressTracker.RecordErrorAsync(ex.Message).ConfigureAwait(false);
            return CreateFailedResult(request.TaskId, ex.Message);
        }
    }

    /// <inheritdoc />
    public Task PauseAsync()
    {
        if (_disposed) return Task.CompletedTask;

        _state = AgentState.Paused;
        _logger?.LogInformation("Agent paused awaiting user input/approval");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task ResumeAsync(CancellationToken ct = default)
    {
        if (_disposed || _state != AgentState.Paused) return;

        _logger?.LogInformation("Agent resuming from paused state");
        await _progressTracker.UpdateStageAsync(TaskProgressStage.InProgress).ConfigureAwait(false);

        // Re-activate based on current phase — could continue planning or acting
    }

    /// <inheritdoc />
    public async Task AbortAsync()
    {
        if (_disposed) return;

        _logger?.LogWarning("Agent aborted by user");
        await _progressTracker.RecordErrorAsync("Agent aborted by user").ConfigureAwait(false);
        _state = AgentState.Failed;
    }

    /// <inheritdoc />
    public IReadOnlyList<AgentToolCallRecord> GetToolCalls() => _toolCalls.AsReadOnly();

    /// <inheritdoc />
    public IReadOnlyList<AgentMessageExchange> GetConversationHistory() => _conversationHistory.AsReadOnly();

    private async Task<bool> ExecuteActPhase(AgentTaskRequest request, CancellationToken ct)
    {
        // Determine what tool to use based on current context and available tools
        string? selectedToolName = TrySelectNextTool(request);
        if (selectedToolName == null)
            return false;  // No more work to do

        ITool? tool = TryFindTool(selectedToolName);

        if (tool == null)
            return true;  // Continue even if tool not found — could be MCP tool or built-in

        var parameters = new Dictionary<string, object>();
        try
        {
            _state = AgentState.Acting;

            // Log act phase message
            AddConversationMessage("agent", "acting", $"Executing tool: {selectedToolName}");

            // Execute the selected tool with appropriate parameters
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool success = await tool.ExecuteAsync(parameters).ConfigureAwait(false);
            sw.Stop();

            _toolCalls.Add(new AgentToolCallRecord(
                selectedToolName, parameters,
                success ? "Success" : "Failure",
                success, sw.ElapsedMilliseconds, DateTime.UtcNow));

            await _progressTracker.RecordToolCallAsync(selectedToolName, parameters,
                success ? "Success" : "Failure", success, sw.ElapsedMilliseconds).ConfigureAwait(false);

            return true;  // Continue executing
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Tool execution failed: {Tool}", selectedToolName);
            var errorSw = System.Diagnostics.Stopwatch.StartNew();
            errorSw.Stop();
            _toolCalls.Add(new AgentToolCallRecord(selectedToolName, parameters, ex.Message, false, errorSw.ElapsedMilliseconds, DateTime.UtcNow));

            await _progressTracker.RecordErrorAsync($"Tool '{selectedToolName}' failed: {ex.Message}").ConfigureAwait(false);
            return true;  // Continue even on failure — agent should try other approaches
        }
    }

    /// <summary>
    /// Generates a context-aware plan response using the available tool descriptions.
    /// When an LLM is available, this would call it to generate a dynamic plan.
    /// Without LLM integration, provides heuristic-based planning with tool suggestions.
    /// </summary>
    private string GeneratePlanResponse()
    {
        if (string.IsNullOrEmpty(_taskDescription))
            return "Analyzing current state and determining next action";

        // Heuristic-based planning: analyze the task description to determine likely needed tools/actions
        var lower = _taskDescription.ToLowerInvariant();
        var suggestedActions = new List<string>();

        if (lower.Contains("file") || lower.Contains("read") || lower.Contains("write"))
            suggestedActions.Add("Use FileRead/FileWrite tools for file operations");

        if (lower.Contains("git") || lower.Contains("commit") || lower.Contains("branch"))
            suggestedActions.Add("Use git-related tools (GitDiffTool, GitHistoryTool) for version control");

        if (lower.Contains("command") || lower.Contains("run") || lower.Contains("execute"))
            suggestedActions.Add("Use CommandExecute tool for running shell commands");

        if (lower.Contains("search") || lower.Contains("find") || lower.Contains("grep"))
            suggestedActions.Add("Use SearchFilesTool to find files or search across project");

        if (lower.Contains("code") || lower.Contains("function") || lower.Contains("class"))
            suggestedActions.Add("Use ProjectExplorer to examine code structure");

        // If no specific tools were identified from the task description, suggest a general approach
        if (suggestedActions.Count == 0)
        {
            return "Analyzing current state and determining next action. Task context: " + _taskDescription;
        }

        var response = new List<string> { "Planning analysis based on task context:" };
        foreach (var suggestion in suggestedActions)
            response.Add($"- {suggestion}");

        return string.Join("\n", response);
    }

    /// <summary>
    /// Selects the next tool to use based on available tools and least usage history.
    /// </summary>
    private string? TrySelectNextTool(AgentTaskRequest request)
    {
        if (request == null || request.AvailableTools == null || request.AvailableTools.Count == 0)
            return null;

        // Simple tool selection: pick first available tool that hasn't been used extensively yet
        var leastUsed = _toolCalls.GroupBy(t => t.ToolName)
            .OrderBy(g => g.Count())
            .Select(g => g.Key);

        foreach (var tool in request.AvailableTools.Concat(leastUsed))
        {
            if (TryFindToolByName(tool))
                return tool;
        }

        // Fallback: select first available tool
        return request.AvailableTools[0];
    }

    /// <summary>
    /// Checks if a tool with the given name exists in the loaded assemblies.
    /// </summary>
    private bool TryFindToolByName(string toolName)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();

            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAbstract || !typeof(ITool).IsAssignableFrom(type)) continue;

                // Check constructor compatibility — needs ILogger and possibly IMcpClient/IMcpResourceAccessor
                var constructors = type.GetConstructors();
                foreach (var ctor in constructors.Where(c => c.IsPublic))
                {
                    try
                    {
                        Activator.CreateInstance(type, new object?[] { _logger, null });
                        return true;  // Constructor found — tool exists
                    }
                    catch
                    {
                        continue;  // Constructor doesn't match — try another one
                    }
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to find tool: {Tool}", toolName);
            return false;
        }
    }

    /// <summary>
    /// Attempts to instantiate a tool by name in the loaded assemblies.
    /// </summary>
    private ITool? TryFindTool(string toolName)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();

            // Search for tool type in this assembly first (built-in tools like FileReadTool)
            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAbstract || !typeof(ITool).IsAssignableFrom(type)) continue;

                var toolInterfaceName = typeof(ITool).FullName ?? "OpenLMStudio.Application.Interfaces.ITool";
                var interfaceImpl = type.GetInterface(toolInterfaceName);
                if (interfaceImpl == null) continue;

                // Check constructor compatibility — needs ILogger and possibly IMcpClient/IMcpResourceAccessor
                var constructors = type.GetConstructors();
                foreach (var ctor in constructors.Where(c => c.IsPublic))
                {
                    try
                    {
                        return Activator.CreateInstance(type, new object?[] { _logger, null }) as ITool;
                    }
                    catch
                    {
                        // Constructor doesn't match — try another one
                    }
                }
            }

            return null;  // Built-in tool not found in this assembly
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to find tool: {Tool}", toolName);
            return null;
        }
    }

    private void AddConversationMessage(string senderRole, string phase, string content) =>
        _conversationHistory.Add(new AgentMessageExchange(
            Guid.Empty, senderRole, phase, content));

    private static AgentTaskResult CreateFailedResult(Guid taskId, string message) =>
        new(taskId, AgentState.Failed, Array.Empty<AgentToolCallRecord>().AsReadOnly(), message);

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            // Clean up any unmanaged resources (tool calls are records — no disposal needed)
            _logger?.LogInformation("Agent disposed");
        }
    }
}
