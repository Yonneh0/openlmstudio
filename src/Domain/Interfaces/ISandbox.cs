using System;
using System.Threading.Tasks;

namespace OpenLMStudio.Domain.Interfaces;

/// <summary>
/// Represents a sandboxed process with resource tracking.
/// </summary>
public record SandboxProcessInfo(
    int ProcessId,
    string Command,
    DateTime StartedAt,
    bool IsRunning,
    double ElapsedMs);

/// <summary>
/// Resource usage for a sandboxed process (CPU time, memory).
/// </summary>
public record SandboxResourceUsage(
    TimeSpan CpuTimeUsed,
    long PeakWorkingSetBytes,
    long CurrentWorkingSetBytes);

/// <summary>
/// Interface for cross-platform process isolation and resource enforcement.
/// Supports Windows Job Objects and Linux/macOS cgroups v2.
/// </summary>
public interface ISandboxService : IDisposable
{
    /// <summary>
    /// Creates a new sandboxed process with the given policy constraints.
    /// Returns the process ID on success, -1 on failure.
    /// </summary>
    Task<int> CreateProcessAsync(string commandLine, string? workingDirectory = null,
        Dictionary<string, string>? environmentVariables = null);

    /// <summary>
    /// Cancels a sandboxed process by its ID.
    /// </summary>
    Task CancelProcessAsync(int processId);

    /// <summary>
    /// Gets the current list of active sandboxed processes and their status.
    /// </summary>
    IReadOnlyList<SandboxProcessInfo> GetActiveProcesses();

    /// <summary>
    /// Gets a summary of resource usage for an active sandboxed process.
    /// </summary>
    Task<SandboxResourceUsage> GetResourceUsageAsync(int processId);

    /// <summary>
    /// Returns true if the platform supports process isolation (Job Objects on Windows, cgroups v2 on Linux/macOS).
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Creates a new sandboxed process with an explicit sandbox policy.
    /// Allows passing a custom policy for more granular control over the sandbox behavior.
    /// Returns the process ID on success, -1 on failure.
    /// </summary>
    Task<int> CreateProcessWithSandboxPolicyAsync(string commandLine, string? workingDirectory = null,
        Dictionary<string, string>? environmentVariables = null, PluginSandboxPolicy? policy = null);
}
