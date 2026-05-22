using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Manages the agent communication protocol for plan/act phase transitions.
/// Handles user approval gating, phase transition events, and safe operation tracking.
/// </summary>
public class AgentProtocolService : IAgentProtocolService, IDisposable
{
    private readonly ILogger<AgentProtocolService>? _logger;
    private readonly IChatCompletionService _chatService;
    private readonly IChatContextManager _contextManager;
    private AgentState _state = AgentState.Idle;
    private bool _userApprovedPlan = false;
    private string? _currentPlan;
    private bool _disposed;

    public AgentProtocolService(
        ILogger<AgentProtocolService>? logger,
        IChatCompletionService chatService,
        IChatContextManager contextManager)
    {
        _logger = logger;
        _chatService = chatService;
        _contextManager = contextManager;
    }

    public AgentState State => _state;
    public bool IsPlanApproved => _userApprovedPlan;
    public string? CurrentPlan => _currentPlan;

    public event EventHandler<PhaseTransitionEventArgs>? PhaseTransitioned;
    public event EventHandler<AgentApprovalRequiredEventArgs>? ApprovalRequired;

    /// <summary>
    /// Generates the initial plan for a task and requests user approval if auto-approve is disabled.
    /// </summary>
    public async Task<AgentPlan> GeneratePlanAsync(AgentTaskRequest request, CancellationToken ct = default)
    {
        _state = AgentState.Planning;
        _userApprovedPlan = false;
        PhaseTransitioned?.Invoke(this, new PhaseTransitionEventArgs(request.TaskId, AgentState.Planning, AgentState.Planning));

        var systemPrompt = BuildSystemPrompt(request);
        var response = await _chatService.GetCompletionAsync(new ChatRequest(
            ModelId: "default",
            Messages: new List<Message>
            {
                new() { Role = MessageRole.System, Content = systemPrompt },
                new() { Role = MessageRole.User, Content = "Propose a detailed plan for completing this task." }
            },
            Stream: false
        ));

        var plan = new AgentPlan(
            request.TaskId,
            response.Message.Content,
            DateTime.UtcNow,
            _userApprovedPlan);

        _currentPlan = response.Message.Content;
        _state = AgentState.Paused; // Paused while awaiting user approval

        _logger?.LogInformation("Plan generated for task {TaskId} — awaiting user approval", request.TaskId);

        return plan;
    }

    /// <summary>
    /// Approves the generated plan, allowing the agent to proceed to the act phase.
    /// </summary>
    public async Task ApprovePlanAsync(Guid taskId)
    {
        if (_currentPlan == null)
            throw new InvalidOperationException("No plan to approve.");

        _userApprovedPlan = true;
        _state = AgentState.Acting;
        PhaseTransitioned?.Invoke(this, new PhaseTransitionEventArgs(taskId, AgentState.Paused, AgentState.Acting));
        _logger?.LogInformation("Plan approved for task {TaskId}", taskId);
    }

    /// <summary>
    /// Rejects the current plan, requesting the agent to generate a new one.
    /// </summary>
    public async Task RejectPlanAsync(Guid taskId, string? reason = null)
    {
        _userApprovedPlan = false;
        var rejectionMessage = reason ?? "Plan rejected — please propose a different approach.";
        _logger?.LogInformation("Plan rejected for task {TaskId}: {Reason}", taskId, rejectionMessage);

        // Generate a new plan based on rejection feedback
        var systemPrompt = BuildSystemPrompt(new AgentTaskRequest(
            taskId,
            $"Task (revision): {reason ?? "Rejected"}",
            Array.Empty<string>()));

        var response = await _chatService.GetCompletionAsync(new ChatRequest(
            ModelId: "default",
            Messages: new List<Message>
            {
                new() { Role = MessageRole.System, Content = systemPrompt },
                new() { Role = MessageRole.User, Content = rejectionMessage }
            },
            Stream: false
        ));

        _currentPlan = response.Message.Content;
        PhaseTransitioned?.Invoke(this, new PhaseTransitionEventArgs(taskId, AgentState.Paused, AgentState.Paused));
    }

    /// <summary>
    /// Checks if a proposed action is safe to execute without user approval.
    /// Safe operations: file reads, search, command echo, etc.
    /// Risky operations: file writes, command execution, deletions — require approval.
    /// </summary>
    public async Task<bool> IsActionSafeAsync(Guid taskId, string toolName, Dictionary<string, object> parameters)
    {
        var safeTools = new[] { "FileRead", "SearchFiles", "ProjectExplorer", "GitDiff", "GitHistory", "GitBlame" };
        if (safeTools.Contains(toolName, StringComparer.OrdinalIgnoreCase))
            return true;

        // Risky tools require user approval
        return false;
    }

    /// <summary>
    /// Requests user approval for a risky action.
    /// </summary>
    public async Task<bool> RequestActionApprovalAsync(Guid taskId, string toolName, Dictionary<string, object> parameters)
    {
        ApprovalRequired?.Invoke(this, new AgentApprovalRequiredEventArgs(
            taskId,
            AgentApprovalType.Action,
            $"Tool: {toolName}\nParameters: {string.Join(", ", parameters)}"));

        // In a real implementation, this would block until the user responds.
        // For now, return true to allow the action (UI would need to update this).
        return true;
    }

    /// <summary>
    /// Generates a dynamic system prompt based on the current task context and available tools.
    /// </summary>
    private string BuildSystemPrompt(AgentTaskRequest request)
    {
        var toolDescriptions = string.Join("\n", request.AvailableTools ?? Array.Empty<string>());
        var context = request.InitialContext != null
            ? string.Join("\n", request.InitialContext.Select(s => s.Content))
            : "(no initial context)";

        return $"""
You are an autonomous agent executing a task.

Task: {request.Description}
Available tools:
{toolDescriptions}
Max iterations: {request.MaxIterations}

Initial context:
{context}

Your job is to complete this task efficiently. When you have a plan, present it clearly.
When executing actions, be precise and thorough.
""";
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}