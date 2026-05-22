// Assembly info for OpenLMStudio — version tracking via git commit hash
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyCopyright("© 2026 OpenLMStudio Contributors. All rights reserved.")]

// Build-time git commit info — populated during compile via AssemblyInfo generator
[assembly: AssemblyMetadata("GitCommit", "$(GitCommit)")]
[assembly: AssemblyMetadata("GitBranch", "$(GitBranch)")]
[assembly: AssemblyMetadata("GitDirty", "$(GitDirty)")]

// Runtime-accessible git info exposed via static property
internal static class GitInfo
{
    private static string? _commit;
    private static string? _branch;
    private static string? _dirty;

    public static string Commit
    {
        get
        {
            _commit ??= GetAssemblyMetadata("GitCommit") ?? "unknown";
            return _commit;
        }
    }

    public static string Branch
    {
        get
        {
            _branch ??= GetAssemblyMetadata("GitBranch") ?? "unknown";
            return _branch;
        }
    }

    public static string Dirty
    {
        get
        {
            _dirty = GetAssemblyMetadata("GitDirty") ?? "false";
            return _dirty;
        }
    }

    public static string FullName => Commit;

    public static string FullBranch => Branch;

    private static string? GetAssemblyMetadata(string key)
    {
        try
        {
            var attr = typeof(GitInfo).Assembly.GetCustomAttribute<AssemblyMetadataAttribute>();
            if (attr != null && string.Equals(attr.Key, key, StringComparison.Ordinal))
                return attr.Value;
        }
        catch { /* best-effort */ }
        return null;
    }
}