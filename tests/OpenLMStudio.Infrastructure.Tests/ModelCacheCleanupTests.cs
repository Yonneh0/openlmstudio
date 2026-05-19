using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Infrastructure.Services;

namespace OpenLMStudio.Infrastructure.Tests;

[TestFixture]
public class ModelCacheCleanupTests
{
    [Test]
    public void Dispose_DoesNotThrow()
    {
        var logger = Substitute.For<ILogger<ModelCacheCleanupService>>();
        var logger2 = Substitute.For<ILogger<AppDataDirectoryResolver>>();
        var appDataResolver = new AppDataDirectoryResolver(logger2);

        var sut = new ModelCacheCleanupService(logger, appDataResolver);
        sut.Dispose();
        // No exception = success
    }

    [Test]
    public async Task CleanAsync_KeepAll_ReturnsZeroFreed()
    {
        var logger = Substitute.For<ILogger<ModelCacheCleanupService>>();
        var logger2 = Substitute.For<ILogger<AppDataDirectoryResolver>>();
        var appDataResolver = new AppDataDirectoryResolver(logger2);

        var sut = new ModelCacheCleanupService(logger, appDataResolver);
        var result = await sut.CleanAsync(ModelCacheRetentionPolicy.KeepAll);
        Assert.That(result.BytesFreed, Is.Zero);
    }

    [Test]
    public async Task GetCacheSizeAsync_ReturnsNonNegativeValue()
    {
        var logger = Substitute.For<ILogger<ModelCacheCleanupService>>();
        var logger2 = Substitute.For<ILogger<AppDataDirectoryResolver>>();
        var appDataResolver = new AppDataDirectoryResolver(logger2);

        var sut = new ModelCacheCleanupService(logger, appDataResolver);
        var size = await sut.GetCacheSizeAsync();
        Assert.That(size, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public async Task GetCachedModelsAsync_ReturnsNonNegativeCount()
    {
        var logger = Substitute.For<ILogger<ModelCacheCleanupService>>();
        var logger2 = Substitute.For<ILogger<AppDataDirectoryResolver>>();
        var appDataResolver = new AppDataDirectoryResolver(logger2);

        var sut = new ModelCacheCleanupService(logger, appDataResolver);
        var models = await sut.GetCachedModelsAsync();
        Assert.That(models.Count, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public async Task RemoveModelAsync_WhenModelNotFound_ReturnsFalse()
    {
        var logger = Substitute.For<ILogger<ModelCacheCleanupService>>();
        var logger2 = Substitute.For<ILogger<AppDataDirectoryResolver>>();
        var appDataResolver = new AppDataDirectoryResolver(logger2);

        var sut = new ModelCacheCleanupService(logger, appDataResolver);
        var result = await sut.RemoveModelAsync("__nonexistent_model__");
        Assert.That(result, Is.False);
    }
}