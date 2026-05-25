namespace OpenLMStudio.Application.Services;

using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

/// <summary>
/// Handles auto-approval of agent tools and commands.
/// </summary>
public class AgentTaskAutoApprover : IAgentTaskAutoApprover
{
    private readonly AgentAutoApprovalSettings _settings;
    private readonly HashSet<string> _approvedTools;
    private readonly HashSet<string> _approvedPaths;
    private readonly HashSet<string> _approvedCommands;
    private readonly object _lock = new();

    public AgentTaskAutoApprover(AgentAutoApprovalSettings? settings = null)
    {
        _settings = settings ?? new AgentAutoApprovalSettings();
        _approvedTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _approvedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _approvedCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public bool ShouldAutoApproveTool(string toolName)
    {
        if (!_settings.Enabled)
            return false;

        lock (_lock)
        {
            return toolName switch
            {
                "write_to_file" => _settings.AutoApproveFileOperations,
                "replace_in_file" => _settings.AutoApproveFileOperations,
                "read_file" => _settings.AutoApproveFileOperations,
                "search_files" => _settings.AutoApproveFileOperations,
                "list_files" => _settings.AutoApproveFileOperations,
                "execute_command" => _settings.AutoApproveCommandExecution,
                "browser_action" => _settings.AutoApproveBrowserActions,
                "use_mcp_tool" => _settings.AutoApproveCommandExecution,
                "access_mcp_resource" => _settings.AutoApproveCommandExecution,
                "load_mcp_documentation" => _settings.AutoApproveCommandExecution,
                "plan_mode_respond" => _settings.AutoApproveCommandExecution,
                "act_mode_respond" => _settings.AutoApproveCommandExecution,
                "attempt_completion" => _settings.AutoApproveCommandExecution,
                "new_task" => _settings.AutoApproveCommandExecution,
                "use_skill" => _settings.AutoApproveCommandExecution,
                "use_subagents" => _settings.AutoApproveCommandExecution,
                "apply_patch" => _settings.AutoApproveFileOperations,
                "generate_explanation" => _settings.AutoApproveCommandExecution,
                "web_fetch" => _settings.AutoApproveCommandExecution,
                "web_search" => _settings.AutoApproveCommandExecution,
                "ask_followup_question" => _settings.AutoApproveCommandExecution,
                _ => _approvedTools.Contains(toolName),
            };
        }
    }

    public bool ShouldAutoApproveToolWithPath(string toolName, string path)
    {
        if (!ShouldAutoApproveTool(toolName))
            return false;

        lock (_lock)
        {
            return _approvedPaths.Contains(path);
        }
    }

    public bool ShouldAutoApproveCommand(string command)
    {
        if (!_settings.Enabled || !_settings.AutoApproveCommandExecution)
            return false;

        lock (_lock)
        {
            return _approvedCommands.Contains(command);
        }
    }

    public int GetCommandTimeout(string command)
    {
        // Long-running commands get extended timeouts
        var isLongRunning = IsLongRunningCommand(command);
        return isLongRunning ? _settings.DefaultTimeout * 3 : _settings.DefaultTimeout;
    }

    public bool IsLongRunningCommand(string command)
    {
        var lowerCommand = command.ToLowerInvariant();
        return lowerCommand.Contains("dotnet") ||
               lowerCommand.Contains("npm") ||
               lowerCommand.Contains("yarn") ||
               lowerCommand.Contains("build") ||
               lowerCommand.Contains("test") ||
               lowerCommand.Contains("run") ||
               lowerCommand.Contains("install") ||
               lowerCommand.Contains("publish");
    }

    public void RecordToolApproval(string toolName, string path)
    {
        lock (_lock)
        {
            _approvedTools.Add(toolName);
            if (!string.IsNullOrEmpty(path))
                _approvedPaths.Add(path);
        }
    }

    public void RecordCommandApproval(string command)
    {
        lock (_lock)
        {
            _approvedCommands.Add(command);
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _approvedTools.Clear();
            _approvedPaths.Clear();
            _approvedCommands.Clear();
        }
    }

    public void Dispose()
    {
        Reset();
    }
}