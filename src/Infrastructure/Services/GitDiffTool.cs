using System.Collections.Generic;
using System.Threading.Tasks;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Tool that shows differences between two Git refs using unified diff format.
/// </summary>
public class GitDiffTool : ITool
{
    private readonly IGitRepositoryService _gitService;

    public GitDiffTool(IGitRepositoryService gitService)
    {
        _gitService = gitService;
    }

    public string Name => "git_diff";

    public string Description => "Shows differences between two Git refs (commits, branches, tags, or working directory).";

    public async Task<bool> ExecuteAsync(Dictionary<string, object> args)
    {
        var repoPath = args["repo_path"] as string ?? ".";
        var oldRef = args["old_ref"] as string ?? "HEAD";
        var newRef = args["new_ref"] as string ?? "HEAD";

        var diffInfo = await _gitService.GetDiffBetweenRefsAsync(oldRef, newRef, includeUnifiedDiff: true);
        var result = diffInfo is not null
            ? $"## Git Diff: {oldRef} → {newRef}\n{diffInfo}\nFiles changed: {diffInfo.Files?.Count ?? 0}\n" + (diffInfo.UnifiedDiff ?? "(no unified diff)")
            : "(No diff available)";

        System.Console.WriteLine(result);
        return true;
    }

    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new()
    {
        ["repo_path"] = new ToolParameterSchema("string", false),
        ["old_ref"] = new ToolParameterSchema("string", false),
        ["new_ref"] = new ToolParameterSchema("string", true)
    };

    public void Dispose() { }
}
