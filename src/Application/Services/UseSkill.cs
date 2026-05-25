namespace OpenLMStudio.Application.Services.Agent;

using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Interfaces;
using OpenLMStudio.Domain.Models;

/// <summary>
/// Stub implementation for use_skill tool.
/// Loads and activates a skill.
/// 
/// REMAINING WORK:
/// - Implement skill discovery and loading
/// - Implement skill activation
/// - Support skill persistence
/// </summary>
public class UseSkill
{
    private readonly ILogger<UseSkill> _logger;

    public UseSkill(ILogger<UseSkill>? logger = null)
    {
        _logger = logger;
    }

    public async Task<ToolResult> ActivateAsync(string skillName)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(skillName))
                return ToolResult.Fail("Missing required parameter: skill_name");

            // TODO: Load and activate skill
            // var skill = _skillRegistry.GetSkill(skillName);
            // if (skill == null)
            //     return ToolResult.Fail($"Skill not found: {skillName}");
            //
            // await skill.ActivateAsync();
            // return ToolResult.Ok(skill.Content);

            _logger?.LogInformation("use_skill: Activated skill '{SkillName}'", skillName);
            return ToolResult.Ok($"Skill '{skillName}' activated.")
                with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ToolResult.Fail($"Error activating skill: {ex.Message}")
                with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
    }
}
