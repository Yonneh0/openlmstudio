namespace OpenLMStudio.Application.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenLMStudio.Application.Types;
using OpenLMStudio.Domain.Models;

/// <summary>
/// Generates system prompts with dynamic context injection for the AI agent.
/// Supports template-based generation with dynamic sections for mode, tools, files, and focus chain.
/// </summary>
public class SystemPromptGenerator
{
    private readonly ILogger<SystemPromptGenerator> _logger;
    private readonly string _basePromptTemplate;

    /// <summary>
    /// Creates a new instance of the SystemPromptGenerator.
    /// </summary>
    public SystemPromptGenerator(
        string? basePromptTemplate = null,
        ILogger<SystemPromptGenerator>? logger = null)
    {
        _logger = logger ?? NullLogger<SystemPromptGenerator>.Instance;
        _basePromptTemplate = basePromptTemplate ?? GenerateDefaultTemplate();
    }

    /// <summary>
    /// Generates the full system prompt with all context injected.
    /// </summary>
    public string GeneratePrompt(
        string agentRole,
        string toolDefinitions,
        string fileStructure,
        string focusChainInstructions,
        string environmentDetails,
        string capabilities,
        string skills,
        string rules,
        string systemInfo,
        string objective,
        string userInstructions,
        string feedback,
        string mode)
    {
        var template = _basePromptTemplate;

        template = template.Replace("{AGENT_ROLE}", agentRole);
        template = template.Replace("{TOOL_DEFINITIONS}", toolDefinitions);
        template = template.Replace("{FILE_STRUCTURE}", fileStructure);
        template = template.Replace("{FOCUS_CHAIN_INSTRUCTIONS}", focusChainInstructions);
        template = template.Replace("{ENVIRONMENT_DETAILS}", environmentDetails);
        template = template.Replace("{CAPABILITIES}", capabilities);
        template = template.Replace("{SKILLS}", skills);
        template = template.Replace("{RULES}", rules);
        template = template.Replace("{SYSTEM_INFO}", systemInfo);
        template = template.Replace("{OBJECTIVE}", objective);
        template = template.Replace("{USER_INSTRUCTIONS}", userInstructions);
        template = template.Replace("{FEEDBACK}", feedback);
        template = template.Replace("{MODE}", mode);

        return template;
    }

    /// <summary>
    /// Generates instructions for focus chain list creation and management.
    /// </summary>
    public string GenerateFocusChainInstructions(string taskId, string currentFocusChain)
    {
        return $"""
            # Focus Chain List for Task {taskId}

            ## Current Focus Chain:
            {currentFocusChain}

            ## Instructions:
            1. Edit the focus chain list to update your focus.
            2. Use `- [ ]` for incomplete items and `- [x]` for completed items.
            3. Add new items as you discover them.
            4. Reorder items as the task progresses.
            5. Save the list to update your focus in the task.
            """;
    }

    /// <summary>
    /// Generates environment details for the current session.
    /// </summary>
    public string GenerateEnvironmentDetails(
        string cwd,
        string[] openFiles,
        string[] runningTerminals,
        string mode,
        string taskProgress)
    {
        var files = openFiles.Length > 0 ? string.Join(", ", openFiles) : "none";
        var terminals = runningTerminals.Length > 0 ? string.Join(", ", runningTerminals) : "none";

        return $"""
            - **Current Working Directory:** {cwd}
            - **Open Files:** {files}
            - **Running Terminals:** {terminals}
            - **Mode:** {mode}
            - **Task Progress:** {taskProgress}
            """;
    }

    /// <summary>
    /// Generates tool definitions in a readable format.
    /// </summary>
    public string GenerateToolDefinitions(IEnumerable<ToolDefinition> tools)
    {
        var definitions = new List<string>();
        foreach (var tool in tools)
        {
            var required = tool.RequiredParameters.Count > 0
                ? $"Required: {string.Join(", ", tool.RequiredParameters)}"
                : "No required parameters";
            definitions.Add($"""
                - **{tool.Name}**
                  - Description: {tool.Description}
                  - {required}
                  - Schema: {string.Join(", ", tool.ParameterSchema.Select(p => $"{p.Key}: {p.Value}"))}
                """);
        }
        return string.Join("\n", definitions);
    }

    /// <summary>
    /// Generates file structure for the current project.
    /// </summary>
    public string GenerateFileStructure(IEnumerable<string> filePaths, bool recursive = true)
    {
        if (!filePaths.Any())
            return "No files in the current project.";

        var lines = new List<string>();
        foreach (var path in filePaths.OrderBy(p => p))
        {
            lines.Add(recursive ? $"  - {path}" : $"- {path}");
        }
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Generates capabilities section for the system prompt.
    /// </summary>
    public string GenerateCapabilitiesSection(IEnumerable<string> capabilities)
    {
        var items = capabilities.Select(c => $"- {c}");
        return string.Join("\n", items);
    }

    /// <summary>
    /// Generates skills section for the system prompt.
    /// </summary>
    public string GenerateSkillsSection(IEnumerable<string> skills)
    {
        var items = skills.Select(s => $"- {s}");
        return string.Join("\n", items);
    }

    /// <summary>
    /// Generates rules section for the system prompt.
    /// </summary>
    public string GenerateRulesSection(IEnumerable<string> rules)
    {
        var items = rules.Select(r => $"- {r}");
        return string.Join("\n", items);
    }

    private string GenerateDefaultTemplate()
    {
        return """
            {AGENT_ROLE}

            ## Tools
            {TOOL_DEFINITIONS}

            ## File Structure
            {FILE_STRUCTURE}

            ## Focus Chain
            {FOCUS_CHAIN_INSTRUCTIONS}

            ## Environment Details
            {ENVIRONMENT_DETAILS}

            ## Capabilities
            {CAPABILITIES}

            ## Skills
            {SKILLS}

            ## Rules
            {RULES}

            ## System Info
            {SYSTEM_INFO}

            ## Objective
            {OBJECTIVE}

            ## User Instructions
            {USER_INSTRUCTIONS}

            ## Feedback
            {FEEDBACK}

            ## Mode
            {MODE}
            """;
    }
}