using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Infrastructure.Services;
using Xunit;

namespace Infrastructure.Unit.Tests;

/// <summary>
/// Tests for OomRecoveryService graceful degradation and eviction priority.
/// </summary>
public class OomRecoveryServiceTests
{
    [Fact]
    public void GetMemoryUsageStatusAsync_ReturnsNormalStatus_WhenMemoryHealthy()
    {
        var modelManager = new MockModelManager();
        var deviceMonitor = new MockDeviceMonitor();
        var service = new OomRecoveryService(modelManager, deviceMonitor);

        var status = service.GetMemoryUsageStatusAsync(CancellationToken.None).Result;
        Assert.Equal(MemoryStatus.Normal, status.Status);
    }

    [Fact]
    public void CheckAndRecoverAsync_ReturnsTrue_WhenMemoryHealthy()
    {
        var modelManager = new MockModelManager();
        var deviceMonitor = new MockDeviceMonitor();
        var service = new OomRecoveryService(modelManager, deviceMonitor);

        var result = service.CheckAndRecoverAsync(CancellationToken.None).Result;
        Assert.True(result);
    }

    [Fact]
    public void MemoryUsageStatusRecord_ContainsAllProperties()
    {
        var status = new MemoryUsageStatus(
            GpuVramTotalBytes: 8L * 1024 * 1024 * 1024,
            GpuVramUsedBytes: 4L * 1024 * 1024 * 1024,
            CpuMemoryTotalBytes: 32L * 1024 * 1024 * 1024,
            CpuMemoryUsedBytes: 16L * 1024 * 1024 * 1024,
            UsageRatio: 0.5,
            Status: MemoryStatus.Normal
        );

        Assert.Equal(8L * 1024 * 1024 * 1024, status.GpuVramTotalBytes);
        Assert.Equal(4L * 1024 * 1024 * 1024, status.GpuVramUsedBytes);
        Assert.Equal(0.5, status.UsageRatio);
        Assert.Equal(MemoryStatus.Normal, status.Status);
    }
}

/// <summary>
/// Mock IModelManager that reports healthy memory for testing normal conditions.
/// </summary>
internal class MockModelManager : IModelManager
{
    public IModelLoader? ActiveTextGeneration => null;
    public IModelLoader? ActiveImageGeneration => null;
    public IModelLoader? ActiveEmbedding => null;
    public IModelLoader? ActiveVae => null;
    public IReadOnlyDictionary<ModelType, IModelLoader> ActiveModels => new Dictionary<ModelType, IModelLoader>();
    public int LoadedCount => 0;
    public long EstimatedMemoryUsageBytes => 0;

    public Task<DeviceMemoryInfo> GetAvailableMemoryAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new DeviceMemoryInfo(
            GpuVramTotalBytes: 8L * 1024 * 1024 * 1024,
            GpuVramFreeBytes: 6L * 1024 * 1024 * 1024,
            CpuMemoryTotalBytes: 32L * 1024 * 1024 * 1024,
            CpuMemoryFreeBytes: 20L * 1024 * 1024 * 1024
        ));

    public string GetCurrentDevice(string modelId) => "cpu";
    public Task<bool> OffloadModelToCpuAsync(string modelId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<bool> MoveModelToDeviceAsync(string modelId, string targetDevice, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task LoadModelAsync(string modelId, ModelType modelType, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UnloadModelByIdAsync(string modelId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UnloadAllModelsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void RegisterLoader(IModelLoader loader) { }

    public void Dispose() { }
}

/// <summary>
/// Mock IDeviceMonitor for OomRecoveryService testing.
/// </summary>
internal class MockDeviceMonitor : IDeviceMonitor
{
    public DeviceInfo CurrentDeviceInformation => new(
        new CpuInfo("Mock CPU", 8, 16, 32L * 1024 * 1024 * 1024),
        new List<GpuDevice>());

    public event Action<DeviceInfo>? DeviceInfoChanged
    {
        add { }
        remove { }
    }

    public void StartMonitoring(TimeSpan? updateInterval = null) { }
    public void StopMonitoring() { }
    public Task<IEnumerable<GpuDevice>> GetGpuDevicesAsync() => Task.FromResult<IEnumerable<GpuDevice>>(Array.Empty<GpuDevice>());
    public Task<IEnumerable<GpuDevice>> DetectAllGpusAsync(CancellationToken ct = default) => Task.FromResult<IEnumerable<GpuDevice>>(Array.Empty<GpuDevice>());
    public double GetCpuUtilization() => 0;
    public long GetAvailableMemoryBytes() => 32L * 1024 * 1024 * 1024;
    public Task<bool> IsGpuCudaCompatibleAsync(string gpuName) => Task.FromResult(false);
    public double GetRecommendedGpuMemoryRatio() => 0.5;
    public void Dispose() { }
}