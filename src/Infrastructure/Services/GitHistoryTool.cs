using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Tool that lists recent git commits with metadata.
/// </summary>
public class GitHistoryTool : ITool
{
    private readonly IGitRepositoryService _gitService;

    public GitHistoryTool(IGitRepositoryService gitService)
    {
        _gitService = gitService;
    }

    public string Name => "git_history";

    public string Description => "Lists recent git commits with author, date, and message. Supports filtering by path.";

    public async Task<bool> ExecuteAsync(Dictionary<string, object> args)
    {
        var repoPath = args["repo_path"] as string ?? ".";
        var maxCount = args["max_count"] is int n && n > 0 ? n : 30;
        var pathFilter = args["path_filter"] as string;

        var commits = await _gitService.ListCommitsAsync(maxCount, pathFilter);

        System.Console.WriteLine($"## Git History (showing {commits.Count} commits):");
        foreach (var c in commits)
        {
            System.Console.WriteLine($"- {c.ShortSha} | {c.AuthorName} | {c.CommittedAt:yyyy-MM-dd HH:mm} | {c.Message}");
        }

        return true;
    }

    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new()
    {
        ["repo_path"] = new ToolParameterSchema("string", false),
        ["max_count"] = new ToolParameterSchema("number", false),
        ["path_filter"] = new ToolParameterSchema("string", false)
    };

    public void Dispose() { }
}

/// <summary>
/// Tool that shows blame annotation for a file — who wrote each line.
/// </summary>
public class GitBlameTool : ITool
{
    private readonly IGitRepositoryService _gitService;

    public GitBlameTool(IGitRepositoryService gitService)
    {
        _gitService = gitService;
    }

    public string Name => "git_blame";

    public string Description => "Shows blame annotation for a file — line-by-line attribution of who wrote each line.";

    public async Task<bool> ExecuteAsync(Dictionary<string, object> args)
    {
        var repoPath = args["repo_path"] as string ?? ".";
        var filePath = args["file_path"] as string ?? throw new ArgumentException("file_path is required.");

        var blame = await _gitService.GetBlameForFileAsync(filePath);

        System.Console.WriteLine($"## Git Blame: {filePath}");
        foreach (var kvp in blame)
        {
            var author = kvp.Value ?? "(unknown)";
            System.Console.WriteLine($"{kvp.Key}: {author}");
        }

        return true;
    }

    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new()
    {
        ["repo_path"] = new ToolParameterSchema("string", false),
        ["file_path"] = new ToolParameterSchema("string", true)
    };

    public void Dispose() { }
}

/// <summary>
/// Tool that lists git branches, tags, and remotes.
/// </summary>
public class GitBranchesTool : ITool
{
    private readonly IGitRepositoryService _gitService;

    public GitBranchesTool(IGitRepositoryService gitService)
    {
        _gitService = gitService;
    }

    public string Name => "git_branches";

    public string Description => "Lists git branches, tags, and remotes in the repository.";

    public async Task<bool> ExecuteAsync(Dictionary<string, object> args)
    {
        var repoPath = args["repo_path"] as string ?? ".";

        var branches = await _gitService.ListBranchesAsync();
        var tags = await _gitService.ListTagsAsync();
        var remotes = await _gitService.ListRemotesAsync();

        System.Console.WriteLine("## Branches:");
        foreach (var b in branches)
            System.Console.WriteLine($"  {b.ShortName} {(b.IsCurrentBranch ? "(current)" : "")} [{b.TipSha?.Substring(0, 7)}]");

        System.Console.WriteLine("## Tags:");
        foreach (var t in tags)
            System.Console.WriteLine($"  {t.ShortName} [{t.TargetSha?.Substring(0, 7)}]");

        System.Console.WriteLine("## Remotes:");
        foreach (var r in remotes)
            System.Console.WriteLine($"  {r.Name} -> {r.Url}");

        return true;
    }

    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new()
    {
        ["repo_path"] = new ToolParameterSchema("string", false)
    };

    public void Dispose() { }
}
