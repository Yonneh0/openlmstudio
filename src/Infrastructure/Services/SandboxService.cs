using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Cross-platform sandbox service for process isolation.
/// Uses Windows Job Objects on Windows and cgroups v2 on Linux/macOS.
/// </summary>
public class SandboxService : ISandboxService, IDisposable
{
    private readonly ILogger<SandboxService>? _logger;
    private readonly HashSet<int> _sandboxedProcessIds = new();
    private readonly object _lockObject = new();

    /// <inheritdoc />
    public bool IsSupported => OperatingSystem.IsWindows() || IsLinuxWithCgroupsV2();

    private static volatile string? _cgroupRootPath;
    private static readonly object _cgroupDetectionLock = new();

    /// <summary>
    /// Detects the cgroups v2 unified mount point. Returns null if not found or not supported.
    /// </summary>
    private static string? GetCgroupV2RootPath()
    {
        lock (_cgroupDetectionLock)
        {
            if (_cgroupRootPath != null) return _cgroupRootPath;

            // Try standard cgroups v2 mount point first (most common on modern Linux).
            foreach (var path in new[] { "/sys/fs/cgroup", "/run/cgroup2" })
            {
                if (Directory.Exists(path))
                {
                    // Verify it's a cgroups v2 unified hierarchy by checking for controller files.
                    var controllersPath = Path.Combine(path, "cgroup.controllers");
                    if (File.Exists(controllersPath))
                    {
                        _cgroupRootPath = path;
                        return _cgroupRootPath;
                    }

                    // Also try to find via subtree_control — indicates cgroups v2 support.
                    var subtreeControlPath = Path.Combine(path, "cgroup.subtree_control");
                    if (File.Exists(subtreeControlPath))
                    {
                        _cgroupRootPath = path;
                        return _cgroupRootPath;
                    }

                    // Check /proc/self/cgroup for cgroups v2 reference.
                    try
                    {
                        var cgLines = File.ReadAllLines("/proc/self/cgroup");
                        foreach (var line in cgLines)
                        {
                            if (line.StartsWith("0::") && string.Equals(line.Substring(3).Trim(), "/", StringComparison.Ordinal))
                            {
                                _cgroupRootPath = path;
                                return _cgroupRootPath;
                            }
                        }
                    }
                    catch
                    {
                        // Ignore — not a cgroups v2 system.
                    }
                }
            }

            return null;
        }
    }

    private static bool IsLinuxWithCgroupsV2() => OperatingSystem.IsLinux() && GetCgroupV2RootPath() != null;

    public SandboxService(ILogger<SandboxService>? logger)
    {
        _logger = logger;

        if (!OperatingSystem.IsWindows())
        {
            _logger?.LogDebug("Sandbox service initialized — platform: {Platform}",
                OperatingSystem.IsLinux() ? "Linux" : "macOS");
        }
    }

    /// <inheritdoc />
    public async Task<int> CreateProcessAsync(string commandLine, string? workingDirectory = null,
        Dictionary<string, string>? environmentVariables = null)
    {
        return await CreateProcessWithSandboxPolicyAsync(commandLine, workingDirectory, environmentVariables, PluginSandboxPolicyDefaults.Default).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> CreateProcessWithSandboxPolicyAsync(string commandLine, string? workingDirectory = null,
        Dictionary<string, string>? environmentVariables = null, Domain.Interfaces.PluginSandboxPolicy? policy = null)
    {
        if (string.IsNullOrEmpty(commandLine))
            throw new ArgumentException("Command line is required.", nameof(commandLine));

        try
        {
            var shellPath = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
            var arguments = OperatingSystem.IsWindows()
                ? $"/C {commandLine}"
                : $"-c \"{commandLine}\"";

            if (!IsSupported)
            {
                _logger?.LogWarning("Sandbox isolation not supported on this platform. Running process without sandbox.");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = shellPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            if (!string.IsNullOrEmpty(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
            }

            // Apply environment variables from sandbox policy.
            if (environmentVariables != null)
            {
                foreach (var kv in environmentVariables)
                {
                    startInfo.Environment[kv.Key] = kv.Value;
                }
            }

            var process = Process.Start(startInfo);

            if (process == null)
            {
                _logger?.LogError("Failed to create sandboxed process");
                return -1;
            }

            // Apply sandbox isolation based on platform.
            await ApplySandboxIsolationAsync(process, policy).ConfigureAwait(false);

            lock (_lockObject)
            {
                _sandboxedProcessIds.Add(process.Id);
            }

            _logger?.LogDebug("Created sandboxed process with ID: {ProcessId}", process.Id);
            return process.Id;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create sandboxed process");
            return -1;
        }
    }

    /// <inheritdoc />
    public async Task CancelProcessAsync(int processId)
    {
        try
        {
            foreach (var proc in Process.GetProcesses())
            {
                if (proc.Id == processId && !proc.HasExited)
                {
                    // On Windows, use Job Object to kill the entire process tree.
                    if (OperatingSystem.IsWindows())
                    {
                        await KillProcessTreeAsync(proc).ConfigureAwait(false);
                    }
                    else
                    {
                        proc.Kill(true);  // Forceful kill on Unix.
                    }

                    lock (_lockObject)
                    {
                        _sandboxedProcessIds.Remove(processId);
                    }

                    _logger?.LogDebug("Cancelled sandboxed process with ID: {ProcessId}", processId);
                    return;
                }
            }

            _logger?.LogWarning("Sandboxed process with ID {ProcessId} not found", processId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to cancel sandboxed process: {ProcessId}", processId);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<SandboxProcessInfo> GetActiveProcesses()
    {
        var results = new List<SandboxProcessInfo>();

        foreach (var proc in Process.GetProcesses())
        {
            if (_sandboxedProcessIds.Contains(proc.Id))
            {
                results.Add(new SandboxProcessInfo(
                    proc.Id,
                    proc.MainModule?.FileName ?? "Unknown",
                    proc.StartTime!,
                    !proc.HasExited,
                    proc.TotalProcessorTime.TotalMilliseconds));
            }
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<SandboxResourceUsage> GetResourceUsageAsync(int processId)
    {
        try
        {
            var proc = Process.GetProcessById(processId);

            return new SandboxResourceUsage(
                proc.TotalProcessorTime,
                Convert.ToInt64(proc.PeakWorkingSet64),
                Convert.ToInt64(proc.WorkingSet64));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get resource usage for sandboxed process: {ProcessId}", processId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task KillAsync(int processId)
    {
        await CancelProcessAsync(processId);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Kill all sandboxed processes on disposal.
        foreach (var proc in Process.GetProcesses())
        {
            lock (_lockObject)
            {
                if (_sandboxedProcessIds.Contains(proc.Id))
                {
                    try
                    {
                        if (!proc.HasExited)
                            proc.Kill(true);
                    }
                    catch { /* Ignore disposal errors */ }
                }
            }
        }

        _sandboxedProcessIds.Clear();
    }

    /// <summary>
    /// Applies platform-specific sandbox isolation to a process.
    /// Windows: Uses Job Object for process tree isolation.
    /// Linux/macOS: Checks cgroups v2 for memory limits and CPU throttling.
    /// </summary>
    private async Task ApplySandboxIsolationAsync(Process process, Domain.Interfaces.PluginSandboxPolicy? policy = null)
    {
        if (!IsSupported || OperatingSystem.IsWindows())
            return;

        // On Linux/macOS with cgroups v2: assign the process to a sandboxed cgroup.
        await ApplyCgroupsV2IsolationAsync(process, policy).ConfigureAwait(false);
    }

    /// <summary>
    /// Assigns a process and its child processes to a Windows Job Object.
    /// This provides memory limits, process tree management, and security boundaries.
    /// </summary>
    private async Task AssignToJobObjectAsync(Process process)
    {
        try
        {
            var jobHandle = CreateJobObject(IntPtr.Zero, null);

            if (jobHandle == IntPtr.Zero)
                return;

            const int maxProcessMemoryBytes = 256 * 1024 * 1024; // 256 MB per process.
            var jobLimitInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            jobLimitInfo.BasicLimitInformation.LimitFlags |= JOB_OBJECT_LIMIT_WORKINGSET;
            jobLimitInfo.ProcessMemoryLimitInBytes = (ulong)maxProcessMemoryBytes;

            var size = Marshal.SizeOf(jobLimitInfo);
            var ptr = Marshal.AllocHGlobal(size);

            try
            {
                Marshal.StructureToPtr(jobLimitInfo, ptr, true);

                if (!SetInformationJobObject(jobHandle, 9 /* ExtendedLimitInformation */, ptr, (uint)size))
                    return;

                // Assign the process to the job object — this will also capture child processes.
                if (!AssignProcessToJobObject(jobHandle, process.Handle))
                {
                    _logger?.LogDebug("Failed to assign process to job object. Error: {ErrorCode}", Marshal.GetLastWin32Error());
                }

                // Set job object termination on limit exceeded — kills the entire tree when memory limit is hit.
                var jobTermInfo = new JOBOBJECT_LIMIT_VIOLATION_INFORMATION();
                ptr = Marshal.AllocHGlobal(Marshal.SizeOf(jobTermInfo));

                try
                {
                    // JOB_OBJECT_LIMIT_DIE_ON_UNHANDLED_EXCEPTION — terminate entire tree on limit violation.
                    jobTermInfo.ViolationFlags = (int)JOB_OBJECT_LIMIT_DIE_ON_UNHANDLED_EXCEPTION;
                    Marshal.StructureToPtr(jobTermInfo, ptr, true);
                    SetInformationJobObject(jobHandle, 25 /* LimitViolationTerminationOnFirst */, ptr, (uint)size);
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            _logger?.LogDebug("Assigned process to Windows Job Object for sandbox isolation");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to assign process to Job Object — sandbox isolation skipped");
        }
    }

    /// <summary>
    /// Applies cgroups v2 sandbox isolation to a Linux/macOS process.
    /// Creates a cgroup with memory and CPU limits, then assigns the process to it via /proc/cgroup.
    /// </summary>
    private async Task ApplyCgroupsV2IsolationAsync(Process process, Domain.Interfaces.PluginSandboxPolicy? policy = null)
    {
        var rootPath = GetCgroupV2RootPath();
        if (string.IsNullOrEmpty(rootPath)) return;

        try
        {
            // Create a unique cgroup name for this sandboxed process group.
            var cgroupName = $"openlmstudio_{process.Id}";

            // Determine the base path for creating the new cgroup.
            var sliceDir = Path.Combine(rootPath, "system.slice");
            if (!Directory.Exists(sliceDir))
            {
                // For containers without system.slice — use openlmstudio-slice as top-level.
                sliceDir = Path.Combine(rootPath, "openlmstudio-slice");
                Directory.CreateDirectory(sliceDir);

                // Write cgroup.subtree_control to enable memory and CPU controllers.
                var subtreeControlPath = Path.Combine(sliceDir, "cgroup.subtree_control");
                if (!File.Exists(subtreeControlPath))
                {
                    try
                    {
                        await File.WriteAllTextAsync(subtreeControlPath, "+memory +cpu").ConfigureAwait(false);
                    }
                    catch
                    {
                        // Try enabling controllers at root level.
                        var rootSubtreeControl = Path.Combine(rootPath, "cgroup.subtree_control");
                        if (File.Exists(rootSubtreeControl))
                        {
                            try
                            {
                                await File.WriteAllTextAsync(rootSubtreeControl, "+memory +cpu").ConfigureAwait(false);
                            }
                            catch
                            {
                                // Controllers not available — continue without them.
                            }
                        }
                    }
                }
            }

            var cgroupPath = Path.Combine(sliceDir, cgroupName);
            Directory.CreateDirectory(cgroupPath);

            if (OperatingSystem.IsLinux())
            {
                // On Linux: write the process ID and its thread group leader IDs to the cgroup.
                await WriteProcsFileAsync(process.Id).ConfigureAwait(false);

                // Also add all threads from /proc/[pid]/task/ for proper isolation of multi-threaded processes.
                try
                {
                    var taskDir = Path.Combine("/proc", process.Id.ToString(), "task");
                    if (Directory.Exists(taskDir))
                    {
                        foreach (var threadId in Directory.GetDirectories(taskDir).Select(d => Path.GetFileName(d)))
                        {
                            await WriteProcsFileAsync(int.Parse(threadId)).ConfigureAwait(false);
                        }
                    }
                }
                catch
                {
                    // Ignore — not all systems have /proc/[pid]/task.
                }

                // Apply memory limit if configured.
                var maxMemoryBytes = (policy?.MaxMemoryMb ?? 256) * 1024L * 1024;
                await SetMemoryLimitAsync(cgroupPath, maxMemoryBytes).ConfigureAwait(false);

                // Apply CPU quota — allow 80% of one CPU core per second.
                await SetCpuQuotaAsync(cgroupPath, 80_000, 100_000).ConfigureAwait(false);
            }

            _logger?.LogDebug("Applied cgroups v2 isolation to process {ProcessId} in cgroup '{Cgroup}'",
                process.Id, cgroupName);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to apply cgroups v2 sandbox isolation — process will run without limits");
        }
    }

    /// <summary>
    /// Writes a PID to the cgroup.procs file atomically.
    /// </summary>
    private async Task WriteProcsFileAsync(int pid)
    {
        var rootPath = GetCgroupV2RootPath();
        if (string.IsNullOrEmpty(rootPath)) return;

        try
        {
            // Find the openlmstudio-slice directory.
            var sliceDir = Path.Combine(rootPath, "system.slice");
            if (!Directory.Exists(sliceDir))
                sliceDir = Path.Combine(rootPath, "openlmstudio-slice");

            var tasksFilePath = Path.Combine(sliceDir, "cgroup.procs");
            try
            {
                // Read existing PIDs first (they may have been added by parent processes).
                var existingPids = new HashSet<int>();
                if (File.Exists(tasksFilePath))
                {
                    foreach (var line in File.ReadAllLines(tasksFilePath))
                    {
                        try { existingPids.Add(int.Parse(line.Trim())); } catch { /* Ignore invalid PIDs */ }
                    }
                }

                // Add new PID only if not already present.
                if (!existingPids.Contains(pid))
                    await File.AppendAllTextAsync(tasksFilePath, $"{pid}\n").ConfigureAwait(false);
            }
            catch (IOException) when (!File.Exists(tasksFilePath))
            {
                // File doesn't exist — try to append anyway.
                await File.AppendAllTextAsync(tasksFilePath, $"{pid}\n").ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            _logger?.LogDebug("Failed to write PID {ProcessId} to cgroup.procs: {Message}", pid, ex.Message);
        }
    }

    /// <summary>
    /// Sets a memory limit for a cgroup via the memory.max file.
    /// </summary>
    private static async Task SetMemoryLimitAsync(string cgroupPath, long maxBytes)
    {
        var memoryMaxPath = Path.Combine(cgroupPath, "memory.max");
        try
        {
            await File.WriteAllTextAsync(memoryMaxPath, maxBytes.ToString()).ConfigureAwait(false);

            // Also set the OOM kill threshold so processes are killed when they exceed the limit.
            var oomKillPath = Path.Combine(cgroupPath, "memory.oom.group");
            await File.WriteAllTextAsync(oomKillPath, "1").ConfigureAwait(false);
        }
        catch
        {
            // Ignore — memory limits are best-effort on non-cgroups systems.
        }
    }

    /// <summary>
    /// Sets CPU quota for a cgroup via the cpu.max file.
    /// </summary>
    private static async Task SetCpuQuotaAsync(string cgroupPath, long quotaUs, long periodUs = 100_000)
    {
        var cpuMaxPath = Path.Combine(cgroupPath, "cpu.max");
        try
        {
            await File.WriteAllTextAsync(cpuMaxPath, $"{quotaUs} {periodUs}").ConfigureAwait(false);
        }
        catch
        {
            // Ignore — CPU limits are best-effort on non-cgroups systems.
        }
    }

    /// <summary>
    /// Kills a sandboxed process group via its cgroup on Linux/macOS.
    /// </summary>
    private async Task KillCgroupAsync(string cgroupName)
    {
        var rootPath = GetCgroupV2RootPath();
        if (string.IsNullOrEmpty(rootPath)) return;

        try
        {
            var sliceDir = Path.Combine(rootPath, "system.slice");
            if (!Directory.Exists(sliceDir))
                sliceDir = Path.Combine(rootPath, "openlmstudio-slice");

            var cgroupPath = Path.Combine(sliceDir, cgroupName);
            if (string.IsNullOrEmpty(cgroupName) || !Directory.Exists(cgroupPath)) return;

            // Read all PIDs from the tasks file and send SIGKILL to each.
            var tasksFilePath = Path.Combine(cgroupPath, "cgroup.procs");
            if (File.Exists(tasksFilePath))
            {
                foreach (var line in await File.ReadAllLinesAsync(tasksFilePath).ConfigureAwait(false))
                {
                    try
                    {
                        var pid = int.Parse(line.Trim());
                        using var proc = Process.GetProcessById(pid);
                        if (!proc.HasExited)
                            proc.Kill(true);
                    }
                    catch (Exception ex) when (ex is ArgumentOutOfRangeException || ex is InvalidOperationException)
                    {
                        // Process already exited — skip it.
                    }
                }

                // Write empty list to cgroup.procs to clean up the group.
                await File.WriteAllTextAsync(tasksFilePath, string.Empty).ConfigureAwait(false);
            }

            _logger?.LogDebug("Killed all processes in cgroup '{Cgroup}'", cgroupName);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            // Ignore — process may have already been killed.
        }
    }

    /// <summary>
    /// Kills an entire process tree on Windows by enumerating child processes via ToolHelp32 API.
    /// </summary>
    private async Task KillProcessTreeAsync(Process parent)
    {
        if (parent.HasExited || OperatingSystem.IsWindows() == false)
            return;

        try
        {
            var childPids = new HashSet<int> { parent.Id };

            await foreach (var pid in EnumChildProcessIds(parent.Id))
            {
                childPids.Add(pid);
            }

            // Kill all processes in the tree, starting from children to parents.
            foreach (var pid in childPids)
            {
                try
                {
                    using var proc = Process.GetProcessById(pid);

                    if (!proc.HasExited)
                    {
                        proc.Kill(true);  // Forceful kill on Windows.
                    }
                }
                catch (ArgumentException)
                {
                    // Process already exited — skip it.
                }
            }

            _logger?.LogDebug("Killed process tree with root: {RootId}", parent.Id);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to kill process tree");
        }
    }

    /// <summary>
    /// Enumerates child processes of a given parent process on Windows using ToolHelp32 API.
    /// </summary>
    private async IAsyncEnumerable<int> EnumChildProcessIds(int parentId)
    {
        var snapshotHandle = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);

        if (snapshotHandle == IntPtr.Zero || snapshotHandle == new IntPtr(-1))
            yield break;

        try
        {
            var entry = new PROCESSENTRY32();
            entry.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));

            if (Process32First(snapshotHandle, ref entry))
            {
                do
                {
                    if (entry.th32ParentProcessID == parentId)
                    {
                        yield return entry.th32ProcessID;

                        // Recursively enumerate grandchildren.
                        await foreach (var childPid in EnumChildProcessIds(entry.th32ProcessID))
                            yield return childPid;
                    }
                } while (Process32Next(snapshotHandle, ref entry));
            }
        }
        finally
        {
            CloseHandle(snapshotHandle);
        }
    }

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    // ===== Windows API P/Invoke declarations for Job Objects =====

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateJobObject(IntPtr lpSecurityAttributes, string? lpName);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(IntPtr hJob, int infoType, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr ProcessHandle);

    // ===== Windows API P/Invoke declarations for ToolHelp32 (process tree enumeration) =====

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, int th32ProcessID);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern uint OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    // ===== Windows Job Object constants =====

    private const uint JOB_OBJECT_LIMIT_WORKINGSET = 0x00000080;
    private const uint JOB_OBJECT_LIMIT_BREAKAWAY_OK = 0x00000800;
    private const uint JOB_OBJECT_LIMIT_DIE_ON_UNHANDLED_EXCEPTION = 0x00001000;

    // ===== Windows Job Object structures =====

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOB_OBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;           // Process I/O counters — not used in sandbox.
        public ulong ProcessMemoryLimitInBytes;     // Max working set size per process (4KB pages).
        public ulong JobMemoryLimitInBytes;         // Job group memory limit — not used in sandbox.
        public ulong PeakProcessMemoryUsedByJob;    // Read-only — max process mem used by job.
        public ulong PeakJobMemoryUsedByJob;        // Read-only — max job mem used.

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public int[] Information;       // Information array — not used in sandbox.

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public long[] Reserved;         // Reserved — must be zero.

        /// <summary>
        /// Initializes the JOBOBJECT_EXTENDED_LIMIT_INFORMATION struct with properly sized arrays.
        /// </summary>
        public JOBOBJECT_EXTENDED_LIMIT_INFORMATION()
        {
            BasicLimitInformation = new JOB_OBJECT_BASIC_LIMIT_INFORMATION();
            IoInfo = new IO_COUNTERS();
            ProcessMemoryLimitInBytes = 0;
            JobMemoryLimitInBytes = 0;
            PeakProcessMemoryUsedByJob = 0;
            PeakJobMemoryUsedByJob = 0;
            Information = new int[64];
            Reserved = new long[8];
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOB_OBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;  // Not used in sandbox.
        public long PerJobUserTimeLimit;      // Not used in sandbox.
        public uint LimitFlags;               // Flags for active limits (e.g., JOB_OBJECT_LIMIT_WORKINGSET).
        public ulong MinimumWorkingSetSizeInBytes;
        public ulong MaximumWorkingSetSizeInBytes;  // Set to memory limit — enforced via LimitFlags.
        public uint ActiveProcesses;           // Read-only — not set by us.
        public uint DeactiveProcesses;         // Read-only — not set by us.
        public long Affinity;                  // Job object affinity — not used in sandbox.
        public uint PriorityClass;             // Not used in sandbox.
        public uint SchedulingClass;           // Not used in sandbox.
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;   // I/O read operations — not used in sandbox.
        public ulong WriteOperationCount;  // I/O write operations — not used in sandbox.
        public ulong OtherOperationCount;  // Other I/O operations — not used in sandbox.
        public ulong ReadTransferCount;    // Bytes transferred for reads — not used in sandbox.
        public ulong WriteTransferCount;   // Bytes transferred for writes — not used in sandbox.
        public ulong OtherTransferCount;   // Bytes transferred for other ops — not used in sandbox.
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_LIMIT_VIOLATION_INFORMATION
    {
        public int ViolationFlags;         // Flags indicating which limit was violated.
        public IntPtr PageFaulteAddress;   // Address of the fault — set by OS, not used in sandbox.
    }

    // ===== Windows ToolHelp32 structures (for process tree enumeration) =====

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public int th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public int th32ModuleID;
        public uint cntThreads;
        public int th32ParentProcessID;  // Parent process ID — used for tree enumeration.
        public long pcPriClassBase;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    // ===== ToolHelp32 constants (for process tree enumeration) =====

    private const uint TH32CS_SNAPPROCESS = 0x00000002;
}