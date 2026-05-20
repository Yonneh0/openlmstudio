using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models.QEMU;
using OpenLMStudio.Infrastructure.Services.QEMU;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Resource monitoring with VM-aware allocation.
/// </summary>
public class ResourceManager : IResourceManager, IDisposable
{
    private readonly IQEMUProcessManager _qemuManager;
    private readonly System.Threading.Timer? _monitorTimer;
    private readonly int _monitorIntervalMs;
    private static readonly int MAX_CONTEXT = 128 * 1024;
    private readonly IContextCompressionService? _contextCompression;
    private bool _disposed;

    public ResourceManager(IQEMUProcessManager qemuManager, IContextCompressionService? contextCompression = null, int monitorIntervalMs = 10000)
    {
        _qemuManager = qemuManager;
        _contextCompression = contextCompression;
        _monitorIntervalMs = monitorIntervalMs;
        _monitorTimer = new System.Threading.Timer(_ => MonitorResourcesAsync().ConfigureAwait(false).GetAwaiter().GetResult(), null, _monitorIntervalMs, _monitorIntervalMs);
    }

    public async Task<ResourceMetrics> MonitorResourcesAsync()
    {
        var cpu = GetCpuUsage();
        var memory = GetMemoryUsage();
        var disk = GetDiskSpace();

        var vmInstances = _qemuManager.Instances.Select(vm => new VmResource
        {
            Id = vm.Id,
            Architecture = vm.Architecture,
            RamBytes = vm.RamBytes,
            State = vm.State,
            CpuCores = vm.CpuTopology.Sockets ?? 1
        }).ToList();

        return new ResourceMetrics
        {
            Cpu = cpu,
            Memory = memory,
            Gpu = 0,
            Disk = disk,
            VmInstances = vmInstances
        };
    }

    public async Task AdjustModelSettingsAsync(ResourceMetrics metrics)
    {
        if (metrics.Cpu > 90)
        {
            // In a real implementation, this would adjust model engine settings
            // For now, just log the resource pressure
            // System.Diagnostics.Debug.WriteLine($"CPU pressure detected: {metrics.Cpu}%");
        }
    }

    public async Task CompactPromptIfNeededAsync(int contextLength)
    {
        if (contextLength > MAX_CONTEXT && _contextCompression != null)
        {
            await _contextCompression.CompressConversationAsync(
                Array.Empty<OpenLMStudio.Domain.Models.Message>(),
                Array.Empty<OpenLMStudio.Domain.Models.ContextCompression.CompressedEntry>()).ConfigureAwait(false);
        }
    }

    private double GetCpuUsage()
    {
        try
        {
            var proc = System.Diagnostics.Process.GetCurrentProcess();
            var totalCores = Environment.ProcessorCount;
            if (totalCores == 0)
                return 0;
            var elapsed = DateTime.UtcNow - proc.StartTime;
            var cpuTime = proc.TotalProcessorTime.TotalMilliseconds;
            return Math.Min(100, (cpuTime / (elapsed.TotalMilliseconds * totalCores)) * 100);
        }
        catch
        {
            return 0;
        }
    }

    private double GetMemoryUsage()
    {
        try
        {
            var proc = System.Diagnostics.Process.GetCurrentProcess();
            return ((double)proc.WorkingSet64 / (1024 * 1024));
        }
        catch
        {
            return 0;
        }
    }

    private double GetDiskSpace()
    {
        try
        {
            var drive = new System.IO.DriveInfo(Environment.GetEnvironmentVariable("HOME") ?? Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            return drive.TotalFreeSpace / (1024.0 * 1024 * 1024);
        }
        catch
        {
            return 0;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _monitorTimer?.Dispose();
    }
}