#nullable disable
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Infrastructure.Services;
using Xunit;

namespace Infrastructure.Unit.Tests;

/// <summary>
/// Tests for ModelManager concurrent model loading and eviction policy.
/// </summary>
public class ModelManagerTests
{
    [Fact]
    public void LoadedCount_ReturnsZero_WhenNoModelsLoaded()
    {
        var modelManager = new ModelManager(null, new WindowsDeviceMonitor(null));
        Assert.Equal(0, modelManager.LoadedCount);
    }

    [Fact]
    public void ActiveModels_ReturnsEmpty_WhenNoModelsLoaded()
    {
        var modelManager = new ModelManager(null, new WindowsDeviceMonitor(null));
        Assert.Empty(modelManager.ActiveModels);
    }

    [Fact]
    public async Task UnloadAllModelsAsync_DoesNotThrow_WhenNoModelsLoaded()
    {
        var modelManager = new ModelManager(null, new WindowsDeviceMonitor(null));
        await modelManager.UnloadAllModelsAsync(CancellationToken.None);
        Assert.Equal(0, modelManager.LoadedCount);
    }

    [Fact]
    public async Task UnloadModelByIdAsync_DoesNotThrow_WhenModelNotFound()
    {
        var modelManager = new ModelManager(null, new WindowsDeviceMonitor(null));
        await modelManager.UnloadModelByIdAsync("nonexistent", CancellationToken.None);
    }

    [Fact]
    public void EstimatedMemoryUsageBytes_ReturnsZero_WhenNoModelsLoaded()
    {
        var modelManager = new ModelManager(null, new WindowsDeviceMonitor(null));
        Assert.Equal(0, modelManager.EstimatedMemoryUsageBytes);
    }

    [Fact]
    public void GetCurrentDevice_ReturnsCpu_WhenModelNotRegistered()
    {
        var modelManager = new ModelManager(null, new WindowsDeviceMonitor(null));
        Assert.Equal("cpu", modelManager.GetCurrentDevice("nonexistent"));
    }
}
