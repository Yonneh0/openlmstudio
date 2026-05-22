using OpenLMStudio.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Tool that allows Pingu to autonomously explore the project structure and report findings.
/// Implements "wandering" behavior by randomly selecting and examining files/directories.
/// </summary>
public class PinguWanderingTool : ITool, IDisposable
{
    private readonly ILogger<PinguWanderingTool>? _logger;
    private readonly IProjectExplorer? _projectExplorer;
    private readonly IFileOperationsService? _fileOperations;
    private bool _disposed;

    public string Name => "PinguWander";
    public string Description => "Autonomously explores the project structure to discover interesting files, directories, and patterns. Useful for Pingu to get oriented in a project or find interesting code to examine. Accepts optional parameters: 'startPath' (starting directory), 'depth' (max recursion depth), 'maxFiles' (max files to examine).";

    public PinguWanderingTool(
        ILogger<PinguWanderingTool>? logger = null,
        IProjectExplorer? projectExplorer = null,
        IFileOperationsService? fileOperations = null)
    {
        _logger = logger;
        _projectExplorer = projectExplorer;
        _fileOperations = fileOperations;
    }

    public async Task<bool> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var startPath = parameters.GetValueOrDefault("startPath", Directory.GetCurrentDirectory()) as string ?? Directory.GetCurrentDirectory();
        var depth = int.TryParse(parameters.GetValueOrDefault("depth", "2")?.ToString(), out var d) ? d : 2;
        var maxFiles = int.TryParse(parameters.GetValueOrDefault("maxFiles", "20")?.ToString(), out var f) ? f : 20;

        _logger?.LogDebug("PinguWander: exploring {StartPath} to depth {Depth}, max {MaxFiles} files", startPath, depth, maxFiles);

        try
        {
            if (_projectExplorer == null)
            {
                _logger?.LogWarning("PinguWander: IProjectExplorer not available");
                return false;
            }

            // Get the project tree
            IReadOnlyList<ProjectNode> projectTree = await _projectExplorer.GetProjectTreeAsync(startPath);

            // Build a summary of the project structure
            var findings = new List<string>();

            // List interesting files (code files, config files, etc.)
            var interestingExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".cs", ".py", ".js", ".ts", ".rs", ".go", ".java",
                ".md", ".json", ".yaml", ".yml", ".toml", ".xml",
                ".csproj", ".sln", ".csproj", ".slnx", ".config"
            };

            var interestingFiles = new List<ProjectNode>();
            foreach (var node in projectTree)
            {
                if (node.IsDirectory == false && interestingExtensions.Contains(System.IO.Path.GetExtension(node.Path)))
                {
                    interestingFiles.Add(node);
                    if (interestingFiles.Count >= 15)
                        break;
                }
            }

            foreach (var file in interestingFiles)
            {
                var fileName = Path.GetFileName(file.Path);
                var sizeStr = file.SizeBytes > 1024
                    ? $"{file.SizeBytes / 1024}KB"
                    : $"{file.SizeBytes}B";
                findings.Add($"📄 {fileName} ({sizeStr})");
            }

            // If we have file operations, peek at a few interesting files
            if (_fileOperations != null && interestingFiles.Any())
            {
                var sampleFile = interestingFiles.OrderBy(_ => Guid.NewGuid()).FirstOrDefault();
                if (sampleFile != null)
                {
                    try
                    {
                        var content = await _fileOperations.ReadFileAsync(new FileReadRequest(sampleFile.Path), CancellationToken.None);
                        var previewLines = content.Content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Take(3).ToArray();
                        findings.Add($"🔍 Preview of {Path.GetFileName(sampleFile.Path)}:");
                        foreach (var line in previewLines)
                        {
                            findings.Add($"    {line}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug("PinguWander: could not read {File}: {Error}", sampleFile.Path, ex.Message);
                    }
                }
            }

            _logger?.LogInformation("PinguWander: found {Count} findings in {StartPath}", findings.Count, startPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "PinguWander: failed to explore {StartPath}", startPath);
            return false;
        }
    }

    public Dictionary<string, ToolParameterSchema> GetParameterSchema()
    {
        return new Dictionary<string, ToolParameterSchema>
        {
            { "startPath", new ToolParameterSchema("string", false) },
            { "depth", new ToolParameterSchema("integer", false) },
            { "maxFiles", new ToolParameterSchema("integer", false) }
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}