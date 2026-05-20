using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Tool that extracts code definitions (classes, functions, methods) from a project directory.
/// Supports C#, Python, TypeScript/JavaScript, and other common languages.
/// </summary>
public class CodeDefinitionExtractorTool : ITool
{
    private readonly IGitRepositoryService _gitService;

    public CodeDefinitionExtractorTool(IGitRepositoryService gitService)
    {
        _gitService = gitService;
    }

    public string Name => "code_definitions";

    public string Description => "Extracts code definitions (classes, functions, methods) from source files in a project directory.";

    public async Task<bool> ExecuteAsync(Dictionary<string, object> args)
    {
        var repoPath = args["repo_path"] as string ?? ".";
        var maxDepth = args["max_depth"] is int d && d > 0 ? d : 4;
        var languageFilter = args["language"] as string;

        var definitions = ExtractDefinitions(repoPath, maxDepth, languageFilter);

        System.Console.WriteLine($"## Code Definitions (language: {languageFilter ?? "all"}, max_depth: {maxDepth}):");
        foreach (var def in definitions)
        {
            System.Console.WriteLine($"  {def}");
        }

        return true;
    }

    public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new()
    {
        ["repo_path"] = new ToolParameterSchema("string", false),
        ["max_depth"] = new ToolParameterSchema("number", false),
        ["language"] = new ToolParameterSchema("string", false)
    };

    public void Dispose() { }

    private static List<string> ExtractDefinitions(string path, int maxDepth, string? languageFilter)
    {
        var definitions = new List<string>();
        var exts = languageFilter?.ToLowerInvariant() switch
        {
            "csharp" or "cs" => new[] { ".cs" },
            "python" or "py" => new[] { ".py" },
            "typescript" or "ts" => new[] { ".ts", ".tsx" },
            "javascript" or "js" => new[] { ".js", ".jsx" },
            _ => new[] { ".cs", ".py", ".ts", ".tsx", ".js", ".jsx", ".java", ".go", ".rb", ".rs" }
        };

        foreach (var ext in exts)
        {
            var pattern = $"*{ext}";
            foreach (var file in Directory.GetFiles(path, pattern, SearchOption.AllDirectories)
                .Where(f => f.Replace('\\', '/').Length - path.Replace('\\', '/').Length <= maxDepth * 3))
            {
                try
                {
                    var relativePath = Path.GetRelativePath(path, file);
                    var content = File.ReadAllText(file);

                    if (ext == ".cs")
                    {
                        ExtractCSharpDefinitions(content, relativePath, definitions);
                    }
                    else if (ext == ".py")
                    {
                        ExtractPythonDefinitions(content, relativePath, definitions);
                    }
                    else if (ext is ".ts" or ".tsx" or ".js" or ".jsx")
                    {
                        ExtractJavaScriptDefinitions(content, relativePath, definitions);
                    }
                }
                catch
                {
                    // Skip files we can't read
                }
            }
        }

        return definitions;
    }

    private static void ExtractCSharpDefinitions(string content, string filePath, List<string> definitions)
    {
        var lines = content.Split('\n');
        var inClass = false;
        var className = "";

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (line.StartsWith("public class") || line.StartsWith("internal class") ||
                line.StartsWith("private class") || line.StartsWith("sealed class") ||
                line.StartsWith("abstract class"))
            {
                inClass = true;
                className = line.Split(' ', 2).LastOrDefault()?.TrimEnd('{', ',') ?? "Unknown";
                definitions.Add($"[{filePath}] class {className} (line {i + 1})");
            }
            else if (inClass && (line.StartsWith("public ") || line.StartsWith("public async ")))
            {
                var methodMatch = Regex.Match(line, @"(public|private|protected|internal)\s+(async\s+)?(Task|void|\w+)\s+(\w+)\s*\([^)]*\)");
                if (methodMatch.Success)
                {
                    var methodName = methodMatch.Groups[4].Value;
                    definitions.Add($"  [{filePath}] {methodName}() (line {i + 1})");
                }
            }
            else if (inClass && (line.StartsWith("private ") || line.StartsWith("protected ") || line.StartsWith("internal ")))
            {
                var methodMatch = Regex.Match(line, @"(public|private|protected|internal)\s+(async\s+)?(Task|void|\w+)\s+(\w+)\s*\([^)]*\)");
                if (methodMatch.Success)
                {
                    var methodName = methodMatch.Groups[4].Value;
                    definitions.Add($"    [{filePath}] {methodName}() (line {i + 1})");
                }
            }
            else if (line.Contains("interface") && line.Contains("class"))
            {
                definitions.Add($"[{filePath}] interface {className}");
            }

            if (line.StartsWith("public ") && line.Contains("get") && line.Contains("set"))
            {
                var propMatch = Regex.Match(line, @"(public|private|protected)\s+\w+\s+(\w+)\s*{");
                if (propMatch.Success)
                {
                    definitions.Add($"    [{filePath}] .{propMatch.Groups[2].Value}");
                }
            }
        }
    }

    private static void ExtractPythonDefinitions(string content, string filePath, List<string> definitions)
    {
        var lines = content.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimStart();

            if (line.StartsWith("class ") && line.Split('(')[0].Contains("class"))
            {
                var className = line.Split(':')[0].Replace("class ", "").Trim();
                definitions.Add($"[{filePath}] class {className} (line {i + 1})");
            }
            else if (line.StartsWith("def ") || line.StartsWith("async def "))
            {
                var funcName = Regex.Match(line, @"def\s+(\w+)").Groups[1].Value;
                if (!string.IsNullOrEmpty(funcName) && funcName != "__init__")
                {
                    definitions.Add($"    [{filePath}] def {funcName}() (line {i + 1})");
                }
            }
        }
    }

    private static void ExtractJavaScriptDefinitions(string content, string filePath, List<string> definitions)
    {
        var lines = content.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (Regex.Match(line, @"class\s+\w+").Success)
            {
                var className = Regex.Match(line, @"class\s+(\w+)").Groups[1].Value;
                definitions.Add($"[{filePath}] class {className} (line {i + 1})");
            }
            else if (Regex.Match(line, @"(export\s+)?(async\s+)?function\s+\w+|const\s+\w+\s*=\s*(async\s+)?\(?\(?").Success)
            {
                var funcMatch = Regex.Match(line, @"(export\s+)?(async\s+)?function\s+(\w+)|const\s+(\w+)\s*=\s*(async\s+)?\(?");
                if (funcMatch.Success)
                {
                    var funcName = funcMatch.Groups[3].Value ?? funcMatch.Groups[4].Value;
                    if (!string.IsNullOrEmpty(funcName))
                    {
                        definitions.Add($"    [{filePath}] function {funcName}() (line {i + 1})");
                    }
                }
            }
        }
    }
}