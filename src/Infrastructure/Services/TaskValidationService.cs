using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Uses Pingu (System AI) to validate whether a task has been completed successfully.
/// Sends task description, result summary, and validation criteria to the System AI.
/// </summary>
public class TaskValidationService : ITaskValidationService
{
    private readonly ILogger<TaskValidationService> _logger;
    private readonly IPinguPromptGenerator _promptGenerator;
    private readonly IChatCompletionService _chatCompletion;

    public TaskValidationService(
        ILogger<TaskValidationService> logger,
        IPinguPromptGenerator promptGenerator,
        IChatCompletionService chatCompletion)
    {
        _logger = logger;
        _promptGenerator = promptGenerator;
        _chatCompletion = chatCompletion;
    }

    public async Task<TaskValidationResult> ValidateTaskCompletionAsync(
        AgenticTask task,
        string resultSummary,
        CancellationToken ct = default)
    {
        try
        {
            var prompt = BuildValidationPrompt(task, resultSummary);
            var request = new ChatRequest(
                ModelId: "",
                Messages: new List<Message>
                {
                    new() { Role = MessageRole.System, Content = "You are a task validation assistant. Review the task results and determine if the task has been completed successfully." },
                    new() { Role = MessageRole.User, Content = prompt }
                },
                MaxTokens: 512,
                Temperature: 0.1
            );

            var response = await _chatCompletion.GetCompletionAsync(request);
            var answer = response?.Message?.Content ?? "Error generating validation response.";

            return ParseValidationResponse(answer, task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task validation failed for task {TaskId}", task.Id);
            return new TaskValidationResult(false, $"Validation error: {ex.Message}");
        }
    }

    public async Task<TaskValidationResult> ValidateStructuredOutputAsync(
        AgenticTask task,
        Dictionary<string, object> outputFields,
        CancellationToken ct = default)
    {
        try
        {
            var prompt = $"""
                # Structured Output Validation
                Task: {task.Description}
                Expected Output Fields: {task.OutputFields}
                Actual Output: {JsonSerializer.Serialize(outputFields, new JsonSerializerOptions { WriteIndented = true })}
                Validation Criteria: {task.ValidationCriteria}

                Validate that the output fields match the expected schema and meet the validation criteria.
                Respond with PASS/FAIL and a brief explanation.
                """;

            var request = new ChatRequest(
                ModelId: "",
                Messages: new List<Message>
                {
                    new() { Role = MessageRole.System, Content = "You are a structured output validator." },
                    new() { Role = MessageRole.User, Content = prompt }
                },
                MaxTokens: 512,
                Temperature: 0.1
            );

            var response = await _chatCompletion.GetCompletionAsync(request);
            var answer = response?.Message?.Content ?? "Error generating validation response.";

            return ParseValidationResponse(answer, task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Structured output validation failed for task {TaskId}", task.Id);
            return new TaskValidationResult(false, $"Validation error: {ex.Message}");
        }
    }

    private static string BuildValidationPrompt(AgenticTask task, string resultSummary)
    {
        return $"""
            # Task Validation Request
            Task ID: {task.Id}
            Description: {task.Description}
            Phase: {task.Phase}
            Validation Criteria: {task.ValidationCriteria ?? "N/A"}
            Instructions: {task.Instructions}

            ## Result Summary
            {resultSummary}

            ## Evaluation
            Does the result meet the validation criteria? Consider:
            1. Were the instructions followed?
            2. Does the output satisfy the validation criteria?
            3. Are there any errors or issues?

            Respond with:
            - PASS/FAIL
            - Brief explanation
            - Retry suggestion if failed (optional)
            """;
    }

    private static TaskValidationResult ParseValidationResponse(string response, AgenticTask task)
    {
        var normalized = response.Trim().ToUpperInvariant();
        var passed = normalized.Contains("PASS");
        var message = response.Trim().Length > 500 ? response[..500] : response.Trim();
        var retrySuggestion = passed ? null : response.Contains("RETRY") ? response.Split("RETRY", StringSplitOptions.None).LastOrDefault()?.Trim() : null;
        return new TaskValidationResult(passed, message, retrySuggestion);
    }
}