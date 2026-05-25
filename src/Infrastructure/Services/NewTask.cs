namespace OpenLMStudio.Infrastructure.Services.Agent;

using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Interfaces;
using OpenLMStudio.Domain.Models;

/// <summary>
/// Stub implementation for new_task tool.
/// Creates a new task with preloaded context.
/// 
/// REMAINING WORK:
/// - Create new AgenticTask with context
/// - Preload current work, technical concepts, relevant files
/// - Preload problem solving history and pending tasks
/// </summary>
public class NewTask
{
    private readonly ILogger<NewTask> _logger;

    public NewTask(ILogger<NewTask>? logger = null)
    {
        _logger = logger;
    }

    public async Task<ToolResult> CreateAsync(string context)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(context))
                return ToolResult.Fail("Missing required parameter: context");

            // TODO: Create new AgenticTask with context
            // var task = new AgenticTask
            // {
            //     Description = "New task",
            //     Instructions = context,
            //     Phase = TaskPhase.Planning
            // };
            // _taskRepository.Create(task);

            _logger?.LogInformation("new_task: Created task with context");
            return ToolResult.Ok($"New task created with context:\n\n{context.Trim()}")
                with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ToolResult.Fail($"Error creating new task: {ex.Message}")
                with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
    }
}