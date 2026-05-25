namespace OpenLMStudio.Application.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenLMStudio.Domain.Interfaces;
using OpenLMStudio.Domain.Models;

/// <summary>
/// Stub implementation for attempt_completion tool.
/// Presents the final result to the user.
/// 
/// REMAINING WORK:
/// - Validate all previous tool uses were successful
/// - Execute command if provided
/// - Show system notification
/// - Save checkpoint
/// - Run TaskComplete hook
/// </summary>
public class AttemptCompletion
{
    private readonly ILogger<AttemptCompletion> _logger;

    public AttemptCompletion(ILogger<AttemptCompletion>? logger = null)
    {
        _logger = logger ?? NullLogger<AttemptCompletion>.Instance;
    }

    /// <summary>
    /// Presents the final result to the user.
    /// 
    /// REMAINING WORK:
    /// - Check previous tool results
    /// - Execute command via Process.Start()
    /// - Show OS notification
    /// - Save checkpoint
    /// - Run TaskComplete hook
    /// </summary>
    public async Task<ToolResult> CompleteAsync(string result, string? command = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(result))
                return ToolResult.Fail("Missing required parameter: result");

            var output = $"Task completed successfully:\n\n{result.Trim()}";

            if (!string.IsNullOrEmpty(command))
                output += $"\n\n[Command to run: {command}]";

            // TODO: Execute command
            // if (!string.IsNullOrEmpty(command))
            // {
            //     var startInfo = new ProcessStartInfo
            //     {
            //         FileName = "cmd.exe",
            //         Arguments = $"/c {command}",
            //         UseShellExecute = true
            //     };
            //     Process.Start(startInfo);
            // }

            // TODO: Show system notification
            // NotificationService.Show("Task Complete", result);

            // TODO: Save checkpoint
            // CheckpointService.Save();

            // TODO: Run TaskComplete hook
            // var hook = _serviceProvider.GetRequiredService<ITaskCompleteHook>();
            // await hook.ExecuteAsync();

            stopwatch.Stop();
            _logger?.LogInformation("attempt_completion: {Result}", result);

            return ToolResult.Ok(output) with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ToolResult.Fail($"Error in attempt_completion: {ex.Message}")
                with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
    }
}
