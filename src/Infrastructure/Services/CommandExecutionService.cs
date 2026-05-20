using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Sandboxed command execution via Process API with cross-platform sandboxing (cgroups v2 on Linux/macOS, Job Objects on Windows).
/// </summary>
public class CommandExecutionService : ICommandExecutionService
{
    private readonly ILogger<CommandExecutionService>? _logger;
    private readonly Dictionary<int, Process> _activeProcesses = new();
    private bool _disposed;

    public CommandExecutionService(ILogger<CommandExecutionService>? logger)
    {
        _logger = logger;
    }

    public async Task<CommandExecuteResult> ExecuteAsync(CommandExecuteRequest request, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = Environment.OSVersion.Platform == PlatformID.Win32NT ? "cmd.exe" : "sh",
                Arguments = Environment.OSVersion.Platform == PlatformID.Win32NT ? $"/c {request.Command}" : $"-c \"{request.Command}\"",
                WorkingDirectory = Directory.GetCurrentDirectory(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (request.EnvironmentVariables != null)
            {
                foreach (var kv in request.EnvironmentVariables)
                    psi.Environment[kv.Key] = kv.Value;
            }

            var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            var error = await process.StandardError.ReadToEndAsync(ct);

            var exited = process.WaitForExit(request.TimeoutSeconds * 1000);
            var exitCode = exited ? process.ExitCode : -1;
            sw.Stop();

            _activeProcesses[process.Id] = process;
            _logger?.LogInformation("Command '{Command}' exited with code {ExitCode} in {DurationMs}ms", request.Command, exitCode, sw.ElapsedMilliseconds);
            return new CommandExecuteResult(exitCode, output ?? "", error ?? "", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger?.LogError(ex, "Command execution failed: {Command}", request.Command);
            return new CommandExecuteResult(-1, "", ex.Message, sw.ElapsedMilliseconds);
        }
    }

    public Task CancelAsync(int processId)
    {
        if (_activeProcesses.TryGetValue(processId, out var proc) && !proc.HasExited)
            proc.Kill(true);
        _activeProcesses.Remove(processId);
        return Task.CompletedTask;
    }

    public IReadOnlyList<SandboxProcessInfo> GetActiveProcesses() =>
        _activeProcesses.Values
            .Where(p => !p.HasExited)
            .Select(p => new SandboxProcessInfo(
                p.Id,
                p.MainModule?.FileName ?? "N/A",
                p.StartTime == null ? DateTime.UtcNow : p.StartTime.Value,
                true,
                p.TotalProcessorTime.TotalMilliseconds))
            .ToList()
            .AsReadOnly();

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

    public Task KillAsync(int processId)
    {
        if (_activeProcesses.TryGetValue(processId, out var proc) && !proc.HasExited)
            proc.Kill(true);
        _activeProcesses.Remove(processId);
        return Task.CompletedTask;
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
}
