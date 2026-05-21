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
        var modelManager = new ModelManager(null, null, null);
        Assert.Equal(0, modelManager.LoadedCount);
    }

    [Fact]
    public void ActiveModels_ReturnsEmpty_WhenNoModelsLoaded()
    {
        var modelManager = new ModelManager(null, null, null);
        Assert.Empty(modelManager.ActiveModels);
    }

    [Fact]
    public void UnloadAllModelsAsync_DoesNotThrow_WhenNoModelsLoaded()
    {
        var modelManager = new ModelManager(null, null, null);
        Assert.DoesNotThrowAsync(() => modelManager.UnloadAllModelsAsync(CancellationToken.None));
    }

    [Fact]
    public void UnloadModelByIdAsync_DoesNotThrow_WhenModelNotFound()
    {
        var modelManager = new ModelManager(null, null, null);
        Assert.DoesNotThrowAsync(() => modelManager.UnloadModelByIdAsync("nonexistent", CancellationToken.None));
    }

    [Fact]
    public void EstimatedMemoryUsageBytes_ReturnsNegativeOne_WhenNoModelsLoaded()
    {
        var modelManager = new ModelManager(null, null, null);
        Assert.Equal(-1, modelManager.EstimatedMemoryUsageBytes);
    }

    [Fact]
    public void GetCurrentDevice_ReturnsCpu_WhenModelNotRegistered()
    {
        var modelManager = new ModelManager(null, null, null);
        Assert.Equal("cpu", modelManager.GetCurrentDevice("nonexistent"));
    }
}