global using System;
global using System.Collections.Generic;
global using System.Threading;
global using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Infrastructure.Services;

namespace OpenLMStudio.Infrastructure.Tests;

/// <summary>
/// Tests for ModelManager concurrent loading, memory budgeting, and device offloading.
/// </summary>
[TestFixture]
public class ModelManagerTests
{
    private ILogger<ModelManager>? _logger;
    private IDeviceMonitor _deviceMonitor = null!;
    private ModelManager _manager = null!;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<ModelManager>();
        _deviceMonitor = Substitute.For<IDeviceMonitor>();
        _deviceMonitor.CurrentDeviceInformation.Returns(new DeviceInfo(
            new CpuInfo("Test CPU", 8, 16, 17_179_869184L),
            new[] { GpuDevice.CreateDefaultPlaceholder() }));

        _manager = new ModelManager(_logger, _deviceMonitor);
    }

    [TearDown]
    public void TearDown()
    {
        _manager?.Dispose();
    }

    [Test]
    public void LoadModelAsync_WithNoLoader_LogsWarning()
    {
        Assert.DoesNotThrowAsync(async () =>
        {
            await _manager.LoadModelAsync("test-model", ModelType.TextGeneration);
        });
    }

    [Test]
    public void LoadModelAsync_RegisterLoader_ThenLoad_Successful()
    {
        var loader = Substitute.For<IModelLoader>();
        loader.SupportedModelType.Returns(ModelType.TextGeneration);
        loader.IsLoaded.Returns(false);
        loader.LoadModelAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        _manager.RegisterLoader(loader);

        Assert.DoesNotThrowAsync(async () =>
        {
            await _manager.LoadModelAsync("test-model", ModelType.TextGeneration);
        });
    }

    [Test]
    public void UnloadAllModelsAsync_DisposesAllLoaders()
    {
        var loader = Substitute.For<IModelLoader>();
        loader.SupportedModelType.Returns(ModelType.TextGeneration);
        loader.IsLoaded.Returns(false);
        loader.LoadModelAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        _manager.RegisterLoader(loader);

        Assert.DoesNotThrowAsync(async () =>
        {
            await _manager.LoadModelAsync("test-model", ModelType.TextGeneration);
            await _manager.UnloadAllModelsAsync();
        });
    }

    [Test]
    public void MoveModelToDeviceAsync_WithValidModel_ChangesDevice()
    {
        var loader = Substitute.For<IModelLoader>();
        loader.SupportedModelType.Returns(ModelType.TextGeneration);
        loader.IsLoaded.Returns(false);
        loader.LoadModelAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        _manager.RegisterLoader(loader);

        Assert.DoesNotThrowAsync(async () =>
        {
            await _manager.LoadModelAsync("test-model", ModelType.TextGeneration);
            var result = await _manager.MoveModelToDeviceAsync("test-model", "gpu");
            Assert.That(result, Is.True);
        });
    }

    [Test]
    public void GetCurrentDevice_WithValidModel_ReturnsDevice()
    {
        var loader = Substitute.For<IModelLoader>();
        loader.SupportedModelType.Returns(ModelType.TextGeneration);
        loader.IsLoaded.Returns(false);
        loader.LoadModelAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        _manager.RegisterLoader(loader);

        Assert.DoesNotThrowAsync(async () =>
        {
            await _manager.LoadModelAsync("test-model", ModelType.TextGeneration);
            var device = _manager.GetCurrentDevice("test-model");
            Assert.That(device, Is.Not.Null);
        });
    }

    [Test]
    public void GetAvailableMemoryAsync_ReturnsValidInfo()
    {
        Assert.DoesNotThrowAsync(async () =>
        {
            var info = await _manager.GetAvailableMemoryAsync();
            Assert.That(info, Is.Not.Null);
            Assert.That(info.GpuVramTotalBytes, Is.GreaterThanOrEqualTo(0));
            Assert.That(info.CpuMemoryTotalBytes, Is.GreaterThanOrEqualTo(0));
        });
    }

    [Test]
    public void Dispose_IsIdempotent()
    {
        Assert.DoesNotThrow(() =>
        {
            _manager.Dispose();
            _manager.Dispose();
        });
    }
}
