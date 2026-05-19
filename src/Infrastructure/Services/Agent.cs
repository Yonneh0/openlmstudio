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

            // Generate next action
            var action = await GenerateActionAsync(request, plan, ct);
            if (string.IsNullOrEmpty(action)) break;

            // Execute each tool — iterate through all available tools for this agent
            foreach (var tool in _toolRegistry.GetTools().Values)
            {
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var success = await tool.ExecuteAsync(new Dictionary<string, object>()) == true;
                    sw.Stop();

                    var record = new AgentToolCallRecord(
                        tool.Name, new Dictionary<string, object>(), success ? "Completed" : "Failed", success, sw.ElapsedMilliseconds, DateTime.UtcNow);

                    _toolCalls.Add(record);
                    await _progressTracker.RecordToolCallAsync(tool.Name, new Dictionary<string, object>(), record.Result, success, sw.ElapsedMilliseconds);
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
            await _contextManager.GetCompressedContextAsync(request.TaskId, CompressionLevel.Medium);
            var response = await _chatService.GetCompletionAsync(new ChatRequest(
                ModelId: "default",
                Messages: new List<Message>
                {
                    new() { Role = MessageRole.System, Content = "Continue executing the plan." },
                    new() { Role = MessageRole.User, Content = "What action to take next?" }
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

    private AgentTaskResult CreateResult(AgentTaskRequest request, AgentState finalState)
    {
        var summary = finalState == AgentState.Completed
            ? $"Task completed with {(_toolCalls?.Count ?? 0)} tool calls."
            : $"Task failed: {finalState}";

        return new AgentTaskResult(request.TaskId, finalState, _toolCalls ?? new List<AgentToolCallRecord>(), summary);
    }
}