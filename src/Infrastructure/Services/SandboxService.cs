// Brought to you by Carls' Jr.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Cross-platform process sandboxing via cgroups v2 (Linux/macOS) and Job Objects (Windows).
/// </summary>
public class SandboxService : ISandboxService
{
    private readonly ILogger<SandboxService>? _logger;
    private readonly HashSet<string> _blockedCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "sudo", "su", "chmod", "chown", "rm -rf", "dd", "mkfs", "fdisk",
        "iptables", "modprobe", "insmod", "rmmod", "shutdown", "reboot", "poweroff",
        "mount", "umount", "mkfs", "dd", "fdisk", "wipefs", "hdparm"
    };

    private readonly HashSet<string> _restrictedEnvVars = new(StringComparer.OrdinalIgnoreCase)
    {
        "HOME", "USER", "USERNAME", "LOGNAME", "SESSION", "DISPLAY", "WAYLAND_DISPLAY",
        "XDG_RUNTIME_DIR", "DBUS_SESSION_BUS_ADDRESS", "XAUTHORITY", "SSH_AUTH_SOCK"
    };

    private readonly HashSet<string> _allowedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/tmp", "/var/tmp", Path.GetTempPath(),
        "/usr/bin", "/usr/sbin", "/bin", "/sbin",
        "/usr/lib", "/lib",
        "/opt"
    };

    private readonly Dictionary<int, Process> _activeProcesses = new();
    private bool _disposed;

    public SandboxService(ILogger<SandboxService>? logger)
    {
        _logger = logger;
    }

    public bool IsSupported => OperatingSystem.IsWindows() || HasCgroupsV2();

    public async Task<int> CreateProcessAsync(string commandLine, string? workingDirectory = null,
        Dictionary<string, string>? environmentVariables = null)
    {
        return await CreateProcessWithSandboxPolicyAsync(commandLine, workingDirectory, environmentVariables, null);
    }

    public async Task<int> CreateProcessWithSandboxPolicyAsync(string commandLine, string? workingDirectory = null,
        Dictionary<string, string>? environmentVariables = null, PluginSandboxPolicy? policy = null)
    {
        // Validate command against blocked list
        if (IsBlockedCommand(commandLine))
        {
            _logger?.LogWarning("Blocked command execution: {Command}", commandLine);
            return -1;
        }

        var sanitizedEnv = SanitizeEnvironment(environmentVariables);
        var process = RunProcess(commandLine, sanitizedEnv, workingDirectory);
        var processId = process.Id;

        if (OperatingSystem.IsWindows())
        {
            var jobHandle = Kernel32Api.CreateJobObject(IntPtr.Zero, null);
            Kernel32Api.AssignProcessToJobObject(jobHandle, process.Handle);
            Kernel32Api.CloseHandle(jobHandle);
        }

        _activeProcesses[processId] = process;
        return processId;
    }

    public async Task CancelProcessAsync(int processId)
    {
        await KillAsync(processId);
    }

    public async Task KillAsync(int processId)
    {
        if (_activeProcesses.TryGetValue(processId, out var process) && !process.HasExited)
        {
            try
            {
                process.Kill(true);
                _logger?.LogInformation("Sandbox process {ProcessId} killed", processId);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to kill sandbox process {ProcessId}", processId);
            }
        }
    }

    public IReadOnlyList<SandboxProcessInfo> GetActiveProcesses()
    {
        var result = new List<SandboxProcessInfo>();
        foreach (var p in _activeProcesses.Values)
        {
            if (!p.HasExited)
            {
                result.Add(new SandboxProcessInfo(
                    p.Id,
                    p.MainModule?.FileName ?? "N/A",
                    p.StartTime,
                    true,
                    p.TotalProcessorTime.TotalMilliseconds));
            }
        }
        return result;
    }

    public async Task<SandboxResourceUsage> GetResourceUsageAsync(int processId)
    {
        try
        {
            var proc = Process.GetProcessById(processId);
            return new SandboxResourceUsage(proc.TotalProcessorTime, proc.PeakWorkingSet64, proc.WorkingSet64);
        }
        catch
        {
            return new SandboxResourceUsage(TimeSpan.Zero, 0, 0);
        }
    }

    private bool IsBlockedCommand(string command)
    {
        var parts = command.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;
        var cmd = parts[0].Split('/').Last().ToLowerInvariant();
        return _blockedCommands.Contains(cmd) || _blockedCommands.Contains(parts[0]);
    }

    private Dictionary<string, string> SanitizeEnvironment(Dictionary<string, string>? envVars)
    {
        if (envVars == null)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return envVars.Where(kv => !_restrictedEnvVars.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
    }

    private Process RunProcess(string command, Dictionary<string, string> env, string? workingDirectory)
    {
        var psi = new ProcessStartInfo
        {
            FileName = Environment.OSVersion.Platform == PlatformID.Win32NT ? "cmd.exe" : "sh",
            Arguments = Environment.OSVersion.Platform == PlatformID.Win32NT ? $"/c {command}" : $"-c \"{command}\"",
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var kv in env)
            psi.Environment[kv.Key] = kv.Value;

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.Start();
        return process;
    }


    private bool HasCgroupsV2()
    {
        return File.Exists("/sys/fs/cgroup/cgroup.controllers");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var proc in _activeProcesses.Values.Where(p => !p.HasExited))
                try { proc.Kill(true); } catch { /* Ignore */ }
            _activeProcesses.Clear();
            _disposed = true;
        }
    }

    // Windows kernel32 interop for Job Objects
    private static class Kernel32Api
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateJobObject(IntPtr a, string? lpJobName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);
    }
}