using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Detects whether a task has been completed based on tool results and goal verification.
/// Supports keyword-based detection and LLM-assisted detection with fallback.
/// </summary>
public class TaskCompletionDetector : ITaskCompletionDetector, IDisposable
{
    private readonly ILogger<TaskCompletionDetector>? _logger;
    private readonly IChatCompletionService? _chatService;
    private readonly bool _usesLlm;
    private bool _disposed;

    public TaskCompletionDetector(
        ILogger<TaskCompletionDetector>? logger = null,
        IChatCompletionService? chatService = null)
    {
        _logger = logger;
        _chatService = chatService;
        _usesLlm = chatService != null;
    }

    /// <summary>
    /// Simple keyword-based completion detection that works without an LLM.
    /// </summary>
    public Task<bool> DetectAsync(string taskDescription, IReadOnlyList<AgentToolCallRecord> toolCalls)
    {
        var completed = false;
        var reason = string.Empty;

        foreach (var call in toolCalls.Reverse())
        {
            var text = call?.Result ?? string.Empty;

            if (text.IndexOf("task completed", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("done.", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("successfully", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                completed = true;
                reason = "Tool output contains completion signal.";
                break;
            }

            if (call?.ToolName == "FileWriteTool" && call.Success)
            {
                completed = true;
                reason = "FileWriteTool completed successfully.";
                break;
            }

            if (call?.ToolName == "GitHistoryTool" && call.Success)
            {
                completed = true;
                reason = "Git operations completed successfully.";
                break;
            }
        }

        if (toolCalls.Count <= 3 && toolCalls.All(c => c?.ToolName == "FileReadTool" || c?.ToolName == "ProjectExplorerTool"))
        {
            completed = true;
            reason = "Read-only task completed (few tool calls).";
        }

        _logger?.LogDebug("Task completion check: Completed={Completed} - {Reason}", completed, reason);
        return Task.FromResult(completed);
    }

    /// <summary>
    /// LLM-assisted completion detection that sends the task goal and tool results to the model.
    /// Falls back to keyword detection if the LLM call fails.
    /// </summary>
    public async Task<bool> DetectAsync(string taskDescription, IReadOnlyList<AgentToolCallRecord> toolCalls, bool useLlmFallback)
    {
        if (!_usesLlm || !useLlmFallback)
        {
            return await DetectAsync(taskDescription, toolCalls);
        }

        var lastResult = toolCalls.LastOrDefault() is { Result: { } result }
            ? result
            : "No results available.";
        var prompt = $"""
            Task goal: {taskDescription}
            
            Tool call results (last):
            {lastResult}
            
            Has the task goal been achieved? Answer with only "yes" or "no".
            """;

        try
        {
            if (_chatService == null)
                return await DetectAsync(taskDescription, toolCalls);

            var response = await _chatService.GetCompletionAsync(new ChatRequest(
                ModelId: "default",
                Messages: new List<Message>
                {
                    new() { Role = MessageRole.User, Content = prompt }
                },
                Stream: false));

            var answer = response.Message.Content?.Trim().ToLowerInvariant() ?? "no";
            var completed = answer.StartsWith("yes");
            _logger?.LogDebug("LLM completion detection: {Answer}", answer);
            return completed;
        }
        catch
        {
            return await DetectAsync(taskDescription, toolCalls);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}