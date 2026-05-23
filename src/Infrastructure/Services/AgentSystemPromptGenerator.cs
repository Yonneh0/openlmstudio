using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Dynamically assembles the system prompt for agent tasks based on the current context
/// and available tools. Supports dynamic tool auto-discovery and context-aware suggestions.
/// </summary>
public class AgentSystemPromptGenerator
{
    private readonly ILogger<AgentSystemPromptGenerator>? _logger;
    private readonly Dictionary<string, string> _toolDescriptions;
    private readonly HashSet<string> _discoveredTools;
    private const string DefaultSystemPrompt = """
        You are an autonomous agent that completes tasks by planning and executing actions.
        You receive a task description, propose a plan, and then execute actions using available tools.
        Be thorough, methodical, and efficient.

        ## Task Management Instructions

        - You have access to the task queue. Tasks are ordered by priority (Critical > High > Normal > Low).
        - Complete dependent tasks before starting this one.
        - Before marking a task complete, verify the result meets the validation criteria. Check for errors. If validation fails, retry.
        - When completing a task, provide structured output in the format specified.
        - Tasks are organized in branches. Work through each branch sequentially.
        - If a task fails, check if dependent tasks can be skipped or if the branch should be abandoned.
        - The current task phase is your active phase. Move to the next phase when ready.
        """;

    public AgentSystemPromptGenerator(
        ILogger<AgentSystemPromptGenerator>? logger = null,
        Dictionary<string, string>? toolDescriptions = null)
    {
        _logger = logger;
        _toolDescriptions = toolDescriptions ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _discoveredTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
    /// Generates context-aware suggestions for the next action based on available tools,
    /// current state, project state, and task description.
    /// </summary>
    public List<string> GenerateNextActionSuggestions(
        string taskDescription,
        IReadOnlyList<string> completedActions,
        string? projectState = null,
        string? currentPhase = null)
    {
        var suggestions = new List<string>();

        // Suggest reading if nothing done
        if (!completedActions.Any() && (
            taskDescription.Contains("read", StringComparison.OrdinalIgnoreCase)
            || taskDescription.Contains("find", StringComparison.OrdinalIgnoreCase)
            || taskDescription.Contains("list", StringComparison.OrdinalIgnoreCase)))
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

        // Suggest testing if task involves code changes
        if (taskDescription.Contains("test", StringComparison.OrdinalIgnoreCase)
            || taskDescription.Contains("fix", StringComparison.OrdinalIgnoreCase)
            || taskDescription.Contains("bug", StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add("After making changes, run tests to verify correctness.");
        }

        // Phase-aware suggestions
        if (currentPhase == "Planning")
        {
            suggestions.Add("Review the project state before proposing a plan.");
            suggestions.Add("Consider the dependencies and priority of related tasks.");
        }
        else if (currentPhase == "Acting")
        {
            suggestions.Add("Execute the plan step by step. Verify each step before moving on.");
            if (projectState != null && projectState.Contains("modified", StringComparison.OrdinalIgnoreCase))
            {
                suggestions.Add("The project has modified files — verify they haven't been changed externally.");
            }
        }

        return suggestions;
    }

    /// <summary>
    /// Registers a tool description for inclusion in future prompts.
    /// </summary>
    public void RegisterToolDescription(string toolName, string description)
    {
        _toolDescriptions[toolName] = description;
        _discoveredTools.Add(toolName);
    }

    /// <summary>
    /// Discovers tools from the given assembly and auto-registers their descriptions.
    /// Looks for types with a [ToolDescription] attribute or a "Description" property.
    /// </summary>
    public void AutoDiscoverTools(Assembly assembly)
    {
        var toolTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Select(t => new { Type = t, Attr = t.GetCustomAttributes(typeof(ToolDescriptionAttribute), true).FirstOrDefault() as ToolDescriptionAttribute });

        foreach (var tool in toolTypes)
        {
            if (tool.Attr != null)
            {
                RegisterToolDescription(tool.Type.Name, tool.Attr.Description);
                _logger?.LogDebug("Auto-discovered tool: {ToolName} — {Description}", tool.Type.Name, tool.Attr.Description);
            }
            else
            {
                var descProp = tool.Type.GetProperty("Description", BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                if (descProp != null && descProp.PropertyType == typeof(string))
                {
                    var desc = descProp.GetValue(tool.Type)?.ToString() ?? tool.Type.Name;
                    RegisterToolDescription(tool.Type.Name, desc);
                    _logger?.LogDebug("Auto-discovered tool: {ToolName} — {Description}", tool.Type.Name, desc);
                }
            }
        }
    }

    /// <summary>
    /// Discovers tools from a list of types and auto-registers their descriptions.
    /// </summary>
    public void AutoDiscoverTools(IEnumerable<Type> toolTypes)
    {
        foreach (var t in toolTypes)
        {
            var attr = t.GetCustomAttributes(typeof(ToolDescriptionAttribute), true).FirstOrDefault() as ToolDescriptionAttribute;
            var desc = attr?.Description ?? t.GetProperty("Description", BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)?.GetValue(t)?.ToString() ?? t.Name;
            RegisterToolDescription(t.Name, desc);
        }
    }

    /// <summary>
    /// Clears all registered tools and auto-discovers from the current assembly.
    /// Useful for rebuilding prompts after tool plugins are loaded.
    /// </summary>
    public void RebuildWithAutoDiscoveredTools(Assembly assembly)
    {
        _toolDescriptions.Clear();
        _discoveredTools.Clear();
        AutoDiscoverTools(assembly);
    }

    public IReadOnlyDictionary<string, string> GetToolDescriptions() => _toolDescriptions;

    /// <summary>
    /// Gets the list of discovered tool names.
    /// </summary>
    public IReadOnlyCollection<string> GetDiscoveredToolNames() => _discoveredTools;

    private string BuildToolSection()
    {
        if (!_toolDescriptions.Any())
            return "No tools available.";

        return string.Join("\n", _toolDescriptions.Select(t => $"- **{t.Key}**: {t.Value}"));
    }
}

/// <summary>
/// Attribute for marking tool types with a description for auto-discovery.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class ToolDescriptionAttribute : Attribute
{
    public string Description { get; }
    public ToolDescriptionAttribute(string description) => Description = description;
}
