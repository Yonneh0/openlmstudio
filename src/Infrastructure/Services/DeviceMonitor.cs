using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Windows-specific implementation for hardware device monitoring.
/// Uses Domain layer types (DeviceInfo, GpuDevice, CpuInfo) exclusively - no duplicate definitions.
/// </summary>
public class WindowsDeviceMonitor : IDeviceMonitor, IDisposable
{
    private readonly ILogger<WindowsDeviceMonitor> _logger;
    private readonly Timer? _monitoringTimer;
    private volatile bool _isMonitoring;

    // Cached device info for CurrentDeviceInformation property
    private DeviceInfo? _cachedDeviceInfo;

    /// <summary>
    /// Event raised when device metrics change.
    /// </summary>
    public event Action<DeviceInfo>? DeviceInfoChanged;

    public WindowsDeviceMonitor(ILogger<WindowsDeviceMonitor> logger)
    {
        _logger = logger;

        // Poll for hardware updates every 10 seconds
        try
        {
            _monitoringTimer = new Timer(MonitorHardware, null, Timeout.Infinite, 10_000);
        }
        catch (PlatformNotSupportedException)
        {
            // Timer not available on this platform - monitoring will be manual only
            _logger.LogDebug("WindowsDeviceMonitor: Timer not available on this platform");
            _monitoringTimer = null;
        }

        // Initialize cached device info
        UpdateDeviceInfo();
    }

    /// <inheritdoc />
    public DeviceInfo CurrentDeviceInformation => _cachedDeviceInfo ?? DeviceInfo.CreateDefaultPlaceholder();

    /// <summary>
    /// Gets available GPU devices using Domain layer GpuDevice type.
    /// </summary>
    private IEnumerable<GpuDevice> GetGpus()
    {
        var gpus = new List<GpuDevice>();

        // Try to get GPU info via WMI (Windows Management Instrumentation)
        try
        {
            var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT * FROM Win32_VideoController");

            int index = 0;
            foreach (var device in searcher.Get())
            {
                var mo = (System.Management.ManagementObject)device;
                gpus.Add(new GpuDevice(index,
                    mo["Name"]?.ToString() ?? "Unknown GPU",
                    mo["Manufacturer"]?.ToString() ?? "Unknown",
                    ParseAdapterRam(mo))
                {
                    HasCudaSupport = IsNvidiaGpu(mo),
                    IsActive = true
                });
                index++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query GPU information via WMI");
        }

        // Fallback: return default placeholder if no GPUs found
        if (gpus.Count == 0)
        {
            gpus.Add(GpuDevice.CreateDefaultPlaceholder());
        }

        return gpus;
    }

    /// <inheritdoc />
    public Task<IEnumerable<GpuDevice>> GetGpuDevicesAsync()
        => Task.FromResult(GetGpus());

    /// <inheritdoc />
    public Task<IEnumerable<GpuDevice>> DetectAllGpusAsync(CancellationToken ct = default)
        => Task.FromResult(GetGpus());

    /// <inheritdoc />
    public double GetCpuUtilization()
    {
        // Calculate average CPU utilization via WMI (Windows Management Instrumentation)
        try
        {
            var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT LoadPercentage FROM Win32_Processor");

            double totalLoad = 0;
            int count = 0;
            foreach (var proc in searcher.Get())
            {
                var mo = (System.Management.ManagementObject)proc;
                totalLoad += Convert.ToDouble(mo["LoadPercentage"]);
                count++;
            }

            return count > 0 ? totalLoad / count : 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query CPU utilization via WMI");
        }

        return 0;
    }

    /// <inheritdoc />
    public long GetAvailableMemoryBytes()
    {
        // Try to get available (free) physical memory via WMI (Windows Management Instrumentation)
        try
        {
            var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT FreePhysicalMemory, TotalVisibleMemorySize FROM Win32_OperatingSystem");

            foreach (var mo in searcher.Get())
            {
                var device = (System.Management.ManagementObject)mo;

                // WMI reports memory in KB, convert to bytes
                long freeKb = Convert.ToInt64(device["FreePhysicalMemory"]);
                return freeKb * 1024L;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query available memory via WMI");
        }

        // Fallback: estimate based on total - assume ~30% of RAM is free as a reasonable default
        try
        {
            var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");

            foreach (var mo in searcher.Get())
            {
                var device = (System.Management.ManagementObject)mo;
                // WMI reports total visible memory in KB, convert to bytes and estimate 30% free
                long totalKb = Convert.ToInt64(device["TotalVisibleMemorySize"]);
                return (totalKb * 1024L / 10) * 3; // ~30% of total RAM as conservative estimate
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query total memory via WMI");
        }

        return 0L;
    }

    /// <inheritdoc />
    public Task<bool> IsGpuCudaCompatibleAsync(string gpuName)
    {
        // Check if the GPU name contains "NVIDIA" - case-insensitive comparison for proper detection
        return Task.FromResult(gpuName != null && gpuName.IndexOf("nvidia", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    /// <inheritdoc />
    public double GetRecommendedGpuMemoryRatio()
        => 0.85; // Recommend 85% of GPU VRAM for model loading by default

    /// <inheritdoc />
    public void StartMonitoring(TimeSpan? updateInterval = null)
    {
        _isMonitoring = true;
        var intervalMs = (int)(updateInterval?.TotalMilliseconds ?? 10_000);
        if (_monitoringTimer != null)
            _monitoringTimer.Change(0, intervalMs);
        _logger.LogInformation("Started hardware monitoring with interval {Interval}ms", intervalMs);
    }

    /// <inheritdoc />
    public void StopMonitoring()
    {
        _isMonitoring = false;
        if (_monitoringTimer != null)
            _monitoringTimer.Change(Timeout.Infinite, Timeout.Infinite);
        _logger.LogInformation("Stopped hardware monitoring");
    }

    private void UpdateDeviceInfo()
    {
        try
        {
            var gpus = GetGpus().ToList();

            // Try to get CPU info via WMI (Windows Management Instrumentation)
            CpuInfo cpuInfo;
            try
            {
                var coreCount = 0;

                // Get physical cores
                try
                {
                    var procSearcher = new System.Management.ManagementObjectSearcher(
                        "SELECT NumberOfCores FROM Win32_Processor");
                    foreach (var proc in procSearcher.Get())
                    {
                        coreCount += Convert.ToInt32(((System.Management.ManagementObject)proc)["NumberOfCores"]);
                    }
                }
                catch { /* Ignore, fall through to logical processor count */ }

                // Get logical processors if physical cores not available or as fallback
                if (coreCount == 0)
                {
                    try
                    {
                        var searcher = new System.Management.ManagementObjectSearcher(
                            "SELECT NumberOfLogicalProcessors FROM Win32_Processor");
                        foreach (var proc in searcher.Get())
                        {
                            coreCount += Convert.ToInt32(((System.Management.ManagementObject)proc)["NumberOfLogicalProcessors"]);
                        }
                    }
                    catch { /* Ignore, fall through to default */ }
                }

                // Get CPU model name
                string cpuModel = "Unknown CPU";
                try
                {
                    var moSearcher = new System.Management.ManagementObjectSearcher(
                        "SELECT Name FROM Win32_Processor");
                    foreach (var proc in moSearcher.Get())
                    {
                        cpuModel = ((System.Management.ManagementObject)proc)["Name"]?.ToString() ?? "Unknown CPU";
                        break;
                    }
                }
                catch
                {
                    cpuModel = "Unknown CPU";
                }

                // Get available RAM via WMI
                long availableRamBytes = 0;
                try
                {
                    var ramSearcher = new System.Management.ManagementObjectSearcher(
                        "SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
                    foreach (var mo in ramSearcher.Get())
                    {
                        var device = (System.Management.ManagementObject)mo;
                        long totalKb = Convert.ToInt64(device["TotalVisibleMemorySize"]);
                        availableRamBytes = totalKb * 1024L; // KB to bytes
                    }
                }
                catch
                {
                    availableRamBytes = 0;
                }

                cpuInfo = new CpuInfo(cpuModel, coreCount, coreCount, availableRamBytes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query CPU information via WMI");
                cpuInfo = Domain.Models.CpuInfo.CreateDefaultPlaceholder();
            }

            // Update CPU device with actual system RAM for model offloading decisions
            var cpuDevice = Device.CreateCpu(cpuInfo.Model, cpuInfo.PhysicalCoreCount);
            if (cpuInfo.AvailableMemoryBytes > 0)
                cpuDevice.SetAvailableMemoryOverride(cpuInfo.AvailableMemoryBytes);

            _cachedDeviceInfo = new DeviceInfo(cpuInfo, gpus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating device information");
            _cachedDeviceInfo = Domain.Models.DeviceInfo.CreateDefaultPlaceholder();
        }
    }

    private void MonitorHardware(object? state)
    {
        if (!_isMonitoring) return;

        try
        {
            var oldInfo = _cachedDeviceInfo;
            UpdateDeviceInfo();

            // If device info changed, raise event
            DeviceInfoChanged?.Invoke(_cachedDeviceInfo!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in hardware monitoring loop");
        }
    }

    private static bool IsNvidiaGpu(System.Management.ManagementObject device)
    {
        try
        {
            var manufacturer = device["Manufacturer"]?.ToString();
            return manufacturer != null && manufacturer.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch
        {
            return false;
        }
    }

    private static long ParseAdapterRam(System.Management.ManagementBaseObject device)
    {
        try
        {
            var ram = Convert.ToInt64(device["AdapterRAM"]);
            return ram > 0 ? ram : 0;
        }
        catch
        {
            return 0L;
        }
    }

    public void Dispose()
    {
        _monitoringTimer?.Dispose();
    }
}