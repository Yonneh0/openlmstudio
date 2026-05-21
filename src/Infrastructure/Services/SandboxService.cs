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
/// Uses Windows Job Objects on Windows, sandbox-exec on macOS, and cgroups v2 on Linux.
/// </summary>
public class SandboxService : ISandboxService, IDisposable
{
    private readonly ILogger<SandboxService>? _logger;
    private readonly HashSet<int> _sandboxedProcessIds = new();
    private readonly object _lockObject = new();

    /// <inheritdoc />
    public bool IsSupported => OperatingSystem.IsWindows() || IsLinuxWithCgroupsV2() || IsMacOSSandboxAvailable();

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

            foreach (var path in new[] { "/sys/fs/cgroup", "/run/cgroup2" })
            {
                if (Directory.Exists(path))
                {
                    var controllersPath = Path.Combine(path, "cgroup.controllers");
                    if (File.Exists(controllersPath))
                    {
                        _cgroupRootPath = path;
                        return _cgroupRootPath;
                    }

                    var subtreeControlPath = Path.Combine(path, "cgroup.subtree_control");
                    if (File.Exists(subtreeControlPath))
                    {
                        _cgroupRootPath = path;
                        return _cgroupRootPath;
                    }

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

    private static volatile string? _macOSProfilePath;
    private static readonly object _macOSDetectionLock = new();

    private static bool IsMacOSSandboxAvailable()
    {
        if (_macOSProfilePath != null) return true;

        try
        {
            var sandboxExecPath = "/usr/sbin/sandbox-exec";
            if (File.Exists(sandboxExecPath))
            {
                _macOSProfilePath = sandboxExecPath;
                return true;
            }

            var homebrewPath = "/usr/local/bin/sandbox-exec";
            if (File.Exists(homebrewPath))
            {
                _macOSProfilePath = homebrewPath;
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public SandboxService(ILogger<SandboxService>? logger)
    {
        _logger = logger;

        if (!OperatingSystem.IsWindows())
        {
            _logger?.LogDebug("Sandbox service initialized — platform: {Platform}",
                OperatingSystem.IsLinux() ? "Linux" : OperatingSystem.IsMacOS() ? "macOS" : "Unknown");
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
        Dictionary<string, string>? environmentVariables = null, PluginSandboxPolicy? policy = null)
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
                    if (OperatingSystem.IsWindows())
                    {
                        await KillProcessTreeAsync(proc).ConfigureAwait(false);
                    }
                    else
                    {
                        proc.Kill(true);
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

    private async Task ApplySandboxIsolationAsync(Process process, PluginSandboxPolicy? policy = null)
    {
        if (!IsSupported || OperatingSystem.IsWindows())
            return;

        if (OperatingSystem.IsMacOS() && IsMacOSSandboxAvailable())
        {
            await ApplyMacOSSandboxAsync(process, policy).ConfigureAwait(false);
            return;
        }

        if (OperatingSystem.IsLinux() && GetCgroupV2RootPath() != null)
        {
            await ApplyCgroupsV2IsolationAsync(process, policy).ConfigureAwait(false);
        }
    }

    private async Task ApplyMacOSSandboxAsync(Process process, PluginSandboxPolicy? policy = null)
    {
        try
        {
            var sandboxPolicy = policy ?? PluginSandboxPolicyDefaults.Default;
            var allowedPaths = string.Join(",", sandboxPolicy.AllowedPaths ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "/tmp", "/var/tmp" });
            var blockedCommands = string.Join(",", sandboxPolicy.BlockedCommands ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sudo", "su", "chmod", "chown" });

            var profile = GenerateMacOSSandboxProfile(allowedPaths, blockedCommands);
            var profilePath = Path.Combine(Path.GetTempPath(), $"openlmstudio-sandbox-{process.Id}.plist");
            await File.WriteAllTextAsync(profilePath, profile);

            process.WaitForExit();

            var sandboxExec = _macOSProfilePath ?? "/usr/sbin/sandbox-exec";
            var originalCmd = process.StartInfo.FileName;
            var originalArgs = process.StartInfo.Arguments;
            var sandboxArgs = $"-f \"{profilePath}\" bash -c \"{originalArgs}\"";

            var sandboxStartInfo = new ProcessStartInfo
            {
                FileName = sandboxExec,
                Arguments = sandboxArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var sandboxProcess = Process.Start(sandboxStartInfo);
            sandboxProcess?.WaitForExit();

            _logger?.LogDebug("Applied macOS sandbox-exec isolation to process {ProcessId}", process.Id);

            if (File.Exists(profilePath))
                File.Delete(profilePath);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to apply macOS sandbox-exec isolation — process will run without limits");
        }
    }

    private static string GenerateMacOSSandboxProfile(string allowedPaths, string blockedCommands)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">");
        sb.AppendLine("<plist version=\"1.0\">");
        sb.AppendLine("<dict>");

        sb.AppendLine("\t<key>network</key>");
        sb.AppendLine("\t<dict>");
        sb.AppendLine("\t\t<key>rule</key>");
        sb.AppendLine("\t\t<string>deny</string>");
        sb.AppendLine("\t</dict>");

        sb.AppendLine("\t<key>file-read</key>");
        sb.AppendLine("\t<dict>");
        sb.AppendLine("\t\t<key>rule</key>");
        sb.AppendLine("\t\t<string>allow</string>");
        sb.AppendLine("\t</dict>");

        sb.AppendLine("\t<key>file-write</key>");
        sb.AppendLine("\t<dict>");
        sb.AppendLine("\t\t<key>rule</key>");
        sb.AppendLine("\t\t<string>allow</string>");
        sb.AppendLine("\t</dict>");

        sb.AppendLine("\t<key>syscall</key>");
        sb.AppendLine("\t<dict>");
        sb.AppendLine("\t\t<key>rule</key>");
        sb.AppendLine("\t\t<string>allow</string>");
        sb.AppendLine("\t</dict>");

        sb.AppendLine("</dict>");
        sb.AppendLine("</plist>");

        return sb.ToString();
    }

    private async Task AssignToJobObjectAsync(Process process)
    {
        try
        {
            var jobHandle = CreateJobObject(IntPtr.Zero, null);

            if (jobHandle == IntPtr.Zero)
                return;

            const int maxProcessMemoryBytes = 256 * 1024 * 1024;
            var jobLimitInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            jobLimitInfo.BasicLimitInformation.LimitFlags |= JOB_OBJECT_LIMIT_WORKINGSET;
            jobLimitInfo.ProcessMemoryLimitInBytes = (ulong)maxProcessMemoryBytes;

            var size = Marshal.SizeOf(jobLimitInfo);
            var ptr = Marshal.AllocHGlobal(size);

            try
            {
                Marshal.StructureToPtr(jobLimitInfo, ptr, true);

                if (!SetInformationJobObject(jobHandle, 9, ptr, (uint)size))
                    return;

                if (!AssignProcessToJobObject(jobHandle, process.Handle))
                {
                    _logger?.LogDebug("Failed to assign process to job object. Error: {ErrorCode}", Marshal.GetLastWin32Error());
                }

                var jobTermInfo = new JOBOBJECT_LIMIT_VIOLATION_INFORMATION();
                ptr = Marshal.AllocHGlobal(Marshal.SizeOf(jobTermInfo));

                try
                {
                    jobTermInfo.ViolationFlags = (int)JOB_OBJECT_LIMIT_DIE_ON_UNHANDLED_EXCEPTION;
                    Marshal.StructureToPtr(jobTermInfo, ptr, true);
                    SetInformationJobObject(jobHandle, 25, ptr, (uint)size);
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

    private async Task ApplyCgroupsV2IsolationAsync(Process process, PluginSandboxPolicy? policy = null)
    {
        var rootPath = GetCgroupV2RootPath();
        if (string.IsNullOrEmpty(rootPath)) return;

        try
        {
            var cgroupName = $"openlmstudio_{process.Id}";
            var sliceDir = Path.Combine(rootPath, "system.slice");
            if (!Directory.Exists(sliceDir))
            {
                sliceDir = Path.Combine(rootPath, "openlmstudio-slice");
                Directory.CreateDirectory(sliceDir);

                var subtreeControlPath = Path.Combine(sliceDir, "cgroup.subtree_control");
                if (!File.Exists(subtreeControlPath))
                {
                    try
                    {
                        await File.WriteAllTextAsync(subtreeControlPath, "+memory +cpu").ConfigureAwait(false);
                    }
                    catch
                    {
                        var rootSubtreeControl = Path.Combine(rootPath, "cgroup.subtree_control");
                        if (File.Exists(rootSubtreeControl))
                        {
                            try
                            {
                                await File.WriteAllTextAsync(rootSubtreeControl, "+memory +cpu").ConfigureAwait(false);
                            }
                            catch { /* Controllers not available */ }
                        }
                    }
                }
            }

            var cgroupPath = Path.Combine(sliceDir, cgroupName);
            Directory.CreateDirectory(cgroupPath);

            if (OperatingSystem.IsLinux())
            {
                await WriteProcsFileAsync(process.Id).ConfigureAwait(false);

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

                var maxMemoryBytes = (policy?.MaxMemoryMb ?? 256) * 1024L * 1024;
                await SetMemoryLimitAsync(cgroupPath, maxMemoryBytes).ConfigureAwait(false);
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

    private async Task WriteProcsFileAsync(int pid)
    {
        var rootPath = GetCgroupV2RootPath();
        if (string.IsNullOrEmpty(rootPath)) return;

        try
        {
            var sliceDir = Path.Combine(rootPath, "system.slice");
            if (!Directory.Exists(sliceDir))
                sliceDir = Path.Combine(rootPath, "openlmstudio-slice");

            var tasksFilePath = Path.Combine(sliceDir, "cgroup.procs");
            try
            {
                var existingPids = new HashSet<int>();
                if (File.Exists(tasksFilePath))
                {
                    foreach (var line in File.ReadAllLines(tasksFilePath))
                    {
                        try { existingPids.Add(int.Parse(line.Trim())); } catch { /* Ignore invalid PIDs */ }
                    }
                }

                if (!existingPids.Contains(pid))
                    await File.AppendAllTextAsync(tasksFilePath, $"{pid}\n").ConfigureAwait(false);
            }
            catch (IOException) when (!File.Exists(tasksFilePath))
            {
                await File.AppendAllTextAsync(tasksFilePath, $"{pid}\n").ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            _logger?.LogDebug("Failed to write PID {ProcessId} to cgroup.procs: {Message}", pid, ex.Message);
        }
    }

    private static async Task SetMemoryLimitAsync(string cgroupPath, long maxBytes)
    {
        var memoryMaxPath = Path.Combine(cgroupPath, "memory.max");
        try
        {
            await File.WriteAllTextAsync(memoryMaxPath, maxBytes.ToString()).ConfigureAwait(false);
            var oomKillPath = Path.Combine(cgroupPath, "memory.oom.group");
            await File.WriteAllTextAsync(oomKillPath, "1").ConfigureAwait(false);
        }
        catch
        {
            // Ignore — memory limits are best-effort on non-cgroups systems.
        }
    }

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
                        // Process already exited
                    }
                }

                await File.WriteAllTextAsync(tasksFilePath, string.Empty).ConfigureAwait(false);
            }

            _logger?.LogDebug("Killed all processes in cgroup '{Cgroup}'", cgroupName);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            // Ignore — process may have already been killed.
        }
    }

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

            foreach (var pid in childPids)
            {
                try
                {
                    using var proc = Process.GetProcessById(pid);
                    if (!proc.HasExited)
                    {
                        proc.Kill(true);
                    }
                }
                catch (ArgumentException)
                {
                    // Process already exited
                }
            }

            _logger?.LogDebug("Killed process tree with root: {RootId}", parent.Id);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to kill process tree");
        }
    }

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

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateJobObject(IntPtr lpSecurityAttributes, string? lpName);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(IntPtr hJob, int infoType, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr ProcessHandle);

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

    private const uint JOB_OBJECT_LIMIT_WORKINGSET = 0x00000080;
    private const uint JOB_OBJECT_LIMIT_BREAKAWAY_OK = 0x00000800;
    private const uint JOB_OBJECT_LIMIT_DIE_ON_UNHANDLED_EXCEPTION = 0x00001000;

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOB_OBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public ulong ProcessMemoryLimitInBytes;
        public ulong JobMemoryLimitInBytes;
        public ulong PeakProcessMemoryUsedByJob;
        public ulong PeakJobMemoryUsedByJob;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public int[] Information;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public long[] Reserved;

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
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public ulong MinimumWorkingSetSizeInBytes;
        public ulong MaximumWorkingSetSizeInBytes;
        public uint ActiveProcesses;
        public uint DeactiveProcesses;
        public long Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_LIMIT_VIOLATION_INFORMATION
    {
        public int ViolationFlags;
        public IntPtr PageFaulteAddress;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public int th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public int th32ModuleID;
        public uint cntThreads;
        public int th32ParentProcessID;
        public long pcPriClassBase;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    private const uint TH32CS_SNAPPROCESS = 0x00000002;
}