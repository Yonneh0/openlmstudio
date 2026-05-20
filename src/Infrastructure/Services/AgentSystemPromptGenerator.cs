using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Dynamically assembles the system prompt for agent tasks based on the current context
/// and available tools. Supports context-aware suggestions for next action.
/// </summary>
public class AgentSystemPromptGenerator
{
    private readonly ILogger<AgentSystemPromptGenerator>? _logger;
    private readonly Dictionary<string, string> _toolDescriptions;
    private const string DefaultSystemPrompt = """
        You are an autonomous agent that completes tasks by planning and executing actions.
        You receive a task description, propose a plan, and then execute actions using available tools.
        Be thorough, methodical, and efficient.
        """;

    public AgentSystemPromptGenerator(
        ILogger<AgentSystemPromptGenerator>? logger = null,
        Dictionary<string, string>? toolDescriptions = null)
    {
        _logger = logger;
        _toolDescriptions = toolDescriptions ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Generates a dynamic system prompt for the planning phase.
    /// </summary>
    public string GeneratePlanningPrompt(string taskDescription, string? additionalContext = null)
    {
        var toolSection = BuildToolSection();
        var contextSection = additionalContext ?? "";

        return $"""
{DefaultSystemPrompt}

## Task
{taskDescription}

## Available Tools
{toolSection}

## Context
{contextSection}

Propose a detailed plan for completing this task. Be specific about which tools to use and in what order.
If the task is simple, use fewer steps. If complex, break it down into manageable sub-tasks.
""";
    }

    /// <summary>
    /// Generates a dynamic system prompt for the acting phase.
    /// </summary>
    public string GenerateActingPrompt(
        string taskDescription,
        string plan,
        IReadOnlyList<string> completedActions,
        string? lastActionResult = null)
    {
        var toolSection = BuildToolSection();
        var completedSection = completedActions.Any()
            ? $"\n## Completed Actions\n{string.Join("\n", completedActions)}"
            : "";
        var resultSection = lastActionResult != null
            ? $"\n## Last Result\n{lastActionResult}"
            : "";

        return $"""
{DefaultSystemPrompt}

## Task
{taskDescription}

## Approved Plan
{plan}
{completedSection}
{resultSection}

## Available Tools
{toolSection}

Based on your progress so far, determine the next action to take. Be precise about tool names and parameters.
If the task is complete, state so explicitly.
""";
    }

    /// <summary>
    /// Generates context-aware suggestions for the next action based on available tools and current state.
    /// </summary>
    public List<string> GenerateNextActionSuggestions(
        string taskDescription,
        IReadOnlyList<string> completedActions)
    {
        var suggestions = new List<string>();

        // Suggest reading if nothing done
        if (!completedActions.Any() && taskDescription.Contains("read", StringComparison.OrdinalIgnoreCase)
            || taskDescription.Contains("find", StringComparison.OrdinalIgnoreCase)
            || taskDescription.Contains("list", StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add("Start by reading the relevant files to understand the current state.");
        }

        // Suggest writing if task involves changes
        if (taskDescription.Contains("write", StringComparison.OrdinalIgnoreCase)
            || taskDescription.Contains("create", StringComparison.OrdinalIgnoreCase)
            || taskDescription.Contains("add", StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add("After understanding the current state, create or modify the necessary files.");
        }

        // Suggest git operations if task involves version control
        if (taskDescription.Contains("commit", StringComparison.OrdinalIgnoreCase)
            || taskDescription.Contains("branch", StringComparison.OrdinalIgnoreCase)
            || taskDescription.Contains("diff", StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add("Use git commands to review and commit changes.");
        }

        return suggestions;
    }

    /// <summary>
    /// Registers a tool description for inclusion in future prompts.
    /// </summary>
    public void RegisterToolDescription(string toolName, string description)
    {
        _toolDescriptions[toolName] = description;
    }

    public IReadOnlyDictionary<string, string> GetToolDescriptions() => _toolDescriptions;

    private string BuildToolSection()
    {
        if (!_toolDescriptions.Any())
            return "No tools available.";

        return string.Join("\n", _toolDescriptions.Select(t => $"- **{t.Key}**: {t.Value}"));
    }
}