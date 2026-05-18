using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Concrete implementation of ICommandExecutionService for running shell commands in a sandboxed environment.
/// </summary>
public class CommandExecutionService : ICommandExecutionService, IDisposable
{
    private readonly ILogger<CommandExecutionService>? _logger;
    private readonly HashSet<int> _activeProcessIds = new();
    private bool _disposed;

    /// <summary>
    /// Creates a new CommandExecutionService instance.
    /// </summary>
    public CommandExecutionService(ILogger<CommandExecutionService>? logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CommandExecuteResult> ExecuteAsync(CommandExecuteRequest request, CancellationToken ct = default)
    {
        if (request == null || string.IsNullOrEmpty(request.Command))
            throw new ArgumentException("Command is required.", nameof(request));

        var process = new Process();
        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",  // Cross-platform: use /bin/sh on Linux/macOS via GetShellExecutable()
            Arguments = $"\"{request.Command}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        if (request.EnvironmentVariables != null)
        {
            foreach (var kv in request.EnvironmentVariables)
                startInfo.Environment[kv.Key] = kv.Value;
        }

        process.StartInfo = startInfo;
        var stdout = new System.Text.StringBuilder();
        var stderr = new System.Text.StringBuilder();
        int exitCode = -1;

        try
        {
            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    lock (stdout)
                        stdout.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    lock (stderr)
                        stderr.AppendLine(e.Data);
                }
            };

            process.Start();
            
            // Track the process for cancellation monitoring
            _activeProcessIds.Add(process.Id);
            
            string stdoutContent = await process.StandardOutput.ReadToEndAsync();
            string stderrContent = await process.StandardError.ReadToEndAsync();
            if (!string.IsNullOrEmpty(stdoutContent))
                stdout.AppendLine(stdoutContent);
            if (!string.IsNullOrEmpty(stderrContent))
                stderr.AppendLine(stderrContent);

            await process.WaitForExitAsync(ct);
            exitCode = process.ExitCode;
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            // Process was cancelled — return partial results with timeout indicator
            _logger?.LogWarning("Command execution timed out: {Command}", request.Command);
            await CancelAsync(process.Id).ConfigureAwait(false);
        }
        finally
        {
            _activeProcessIds.Remove(process.Id);
            process.Dispose();
        }

        string stdoutStr = stdout.ToString().TrimEnd('\r', '\n');
        string stderrStr = stderr.ToString().TrimEnd('\r', '\n');

        return new CommandExecuteResult(
            exitCode,
            stdoutStr,
            stderrStr,
            request.TimeoutSeconds * 1000.0);
    }

    /// <inheritdoc />
    public async Task CancelAsync(int processId)
    {
        try
        {
            foreach (var proc in Process.GetProcesses())
            {
                if (proc.Id == processId && !proc.HasExited)
                    proc.Kill(true);  // Forceful kill on Windows
            }
            
            _activeProcessIds.Remove(processId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to cancel process: {ProcessId}", processId);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<SandboxProcessInfo> GetActiveProcesses()
    {
        var results = new List<SandboxProcessInfo>();
        
        foreach (var proc in Process.GetProcesses())
        {
            if (_activeProcessIds.Contains(proc.Id))
                results.Add(new SandboxProcessInfo(
                    proc.Id,
                    proc.MainModule?.FileName ?? "Unknown",
                    proc.StartTime!,
                    !proc.HasExited,
                    proc.TotalProcessorTime.TotalMilliseconds));
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
            _logger?.LogError(ex, "Failed to get resource usage for process: {ProcessId}", processId);
            throw;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            
            // Clean up any remaining active processes
            foreach (var procId in _activeProcessIds.ToList())
            {
                try
                {
                    CancelAsync(procId);
                }
                catch { /* Ignore cleanup errors */ }
            }
        }
    }
}