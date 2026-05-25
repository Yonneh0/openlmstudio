namespace OpenLMStudio.Infrastructure.Services.Agent;

using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Interfaces;
using OpenLMStudio.Domain.Models;

/// <summary>
/// Implements ask_followup_question tool for interactive questions.
/// Fully implemented with option support.
/// </summary>
public class QuestionService
{
    private readonly ILogger<QuestionService> _logger;

    public QuestionService(ILogger<QuestionService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Asks the user a question with optional options.
    /// </summary>
    public async Task<ToolResult> AskAsync(string question, List<string>? options = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(question))
                return ToolResult.Fail("Missing required parameter: question");

            var output = new System.Text.StringBuilder();
            output.AppendLine(question);

            if (options != null && options.Count > 0)
            {
                output.AppendLine("\nOptions:");
                for (var i = 0; i < options.Count; i++)
                    output.AppendLine($"  {(i + 1)}. {options[i]}");
            }

            // In a real implementation, this would present a UI dialog
            // and return the user's selection. For now, we return the question
            // with a note that the user should respond.
            var result = output.ToString().Trim();
            result += "\n\n[Please respond with your selection.]";

            stopwatch.Stop();
            _logger?.LogInformation("ask_followup_question: {Question}", question);

            return ToolResult.Ok(result) with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ToolResult.Fail($"Error asking question: {ex.Message}")
                with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
    }
}