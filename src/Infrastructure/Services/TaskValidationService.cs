using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// AI-powered task completion verification using the System AI (Pingu).
/// </summary>
public class TaskValidationService : ITaskValidationService
{
    private readonly ISystemAIClient _systemAIClient;
    private readonly ILogger<TaskValidationService>? _logger;

    public TaskValidationService(
        ISystemAIClient systemAIClient,
        ILogger<TaskValidationService>? logger)
    {
        _systemAIClient = systemAIClient;
        _logger = logger;
    }

    public async Task<TaskValidationResult> ValidateTaskCompletionAsync(
        AgenticTask task,
        string result,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(task.ValidationCriteria))
        {
            // No validation criteria — assume passed
            return new TaskValidationResult(true, "No validation criteria specified.", null);
        }

        var prompt = $"""
            You are validating a task completion for an AI agent system.

            Task Description:
            {task.Description}

            Validation Criteria:
            {task.ValidationCriteria}

            Task Result:
            {result}

            Please evaluate whether the task has been completed successfully based on the validation criteria.

            Respond in the following format:
            PASS: <true or false>
            MESSAGE: <brief explanation of the validation result>
            FAILED_REASON: <if failed, explain why; otherwise "N/A">
            """;

        try
        {
            var response = (await _systemAIClient.SendMessageAsync(prompt)!) ?? string.Empty;

            var passed = ParseValidationResponse(response, out var message, out var failedReason);

            _logger?.LogInformation(
                "Task validation {Result}: {TaskId} - {Message}",
                passed ? "PASSED" : "FAILED",
                task.Id,
                message);

            return new TaskValidationResult(passed, message, failedReason);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Task validation failed for task {TaskId}", task.Id);
            return new TaskValidationResult(false, "Validation service unavailable.", ex.Message);
        }
    }

    public async Task<TaskValidationResult> ValidateStructuredOutputAsync(
        AgenticTask task,
        string output,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(task.OutputFields))
            return new TaskValidationResult(true, "No structured output schema specified.", null);

        var prompt = $"""
            You are validating structured output against a JSON schema.

            Schema:
            {task.OutputFields}

            Output to validate:
            {output}

            Please evaluate whether the output matches the schema.

            Respond in the following format:
            PASS: <true or false>
            MESSAGE: <brief explanation of the validation result>
            FAILED_REASON: <if failed, explain why; otherwise "N/A">
            """;

        try
        {
            var response = (await _systemAIClient.SendMessageAsync(prompt)!) ?? string.Empty;
            var passed = ParseValidationResponse(response, out var message, out var failedReason);

            return new TaskValidationResult(passed, message, failedReason);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Structured output validation failed for task {TaskId}", task.Id);
            return new TaskValidationResult(false, "Validation service unavailable.", ex.Message);
        }
    }

    private static bool ParseValidationResponse(string response, out string message, out string? failedReason)
    {
        message = "Unknown";
        failedReason = "N/A";

        var passLine = response.Split('\n')
            .Select(l => l.Trim())
            .FirstOrDefault(l => l.StartsWith("PASS:", StringComparison.OrdinalIgnoreCase));

        if (passLine != null)
        {
            var value = passLine.Substring(5).Trim();
            if (bool.TryParse(value, out var passed))
                return passed;
        }

        // Fallback: look for message
        var msgLine = response.Split('\n')
            .Select(l => l.Trim())
            .FirstOrDefault(l => l.StartsWith("MESSAGE:", StringComparison.OrdinalIgnoreCase));

        if (msgLine != null)
            message = msgLine.Substring(8).Trim();

        var reasonLine = response.Split('\n')
            .Select(l => l.Trim())
            .FirstOrDefault(l => l.StartsWith("FAILED_REASON:", StringComparison.OrdinalIgnoreCase));

        if (reasonLine != null)
            failedReason = reasonLine.Substring(14).Trim();

        return false;
    }
}