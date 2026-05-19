using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using OpenLMStudio.Infrastructure.Services;

namespace OpenLMStudio.Infrastructure.Tests;

[TestFixture]
public class UpdateManagerTests
{
    [Test]
    public void CurrentStatus_Initial_IsCurrent()
    {
        var logger = Substitute.For<ILogger<UpdateManager>>();
        var appDataLogger = Substitute.For<ILogger<AppDataDirectoryResolver>>();
        var appDataResolver = new AppDataDirectoryResolver(appDataLogger);

        var sut = new UpdateManager(logger, appDataResolver);
        Assert.That(sut.CurrentStatus, Is.EqualTo(Application.Interfaces.UpdateStatus.Current));
    }

    [Test]
    public void DownloadProgress_Initial_IsZero()
    {
        var logger = Substitute.For<ILogger<UpdateManager>>();
        var appDataLogger = Substitute.For<ILogger<AppDataDirectoryResolver>>();
        var appDataResolver = new AppDataDirectoryResolver(appDataLogger);

        var sut = new UpdateManager(logger, appDataResolver);
        Assert.That(sut.DownloadProgress, Is.Zero);
    }

    [Test]
    public async Task DownloadUpdateAsync_WhenNoUpdateAvailable_ReturnsFalse()
    {
        var logger = Substitute.For<ILogger<UpdateManager>>();
        var appDataLogger = Substitute.For<ILogger<AppDataDirectoryResolver>>();
        var appDataResolver = new AppDataDirectoryResolver(appDataLogger);

        var sut = new UpdateManager(logger, appDataResolver);
        var result = await sut.DownloadUpdateAsync();
        Assert.That(result, Is.False);
    }

    [Test]
    public void Dispose_DisposesHttpClient()
    {
        var logger = Substitute.For<ILogger<UpdateManager>>();
        var appDataLogger = Substitute.For<ILogger<AppDataDirectoryResolver>>();
        var appDataResolver = new AppDataDirectoryResolver(appDataLogger);

        var sut = new UpdateManager(logger, appDataResolver);
        sut.Dispose();
        // No exception = success
    }

    [Test]
    public async Task CheckPluginUpdatesAsync_ReturnsEmptyList()
    {
        var logger = Substitute.For<ILogger<UpdateManager>>();
        var appDataLogger = Substitute.For<ILogger<AppDataDirectoryResolver>>();
        var appDataResolver = new AppDataDirectoryResolver(appDataLogger);

        var sut = new UpdateManager(logger, appDataResolver);
        var result = await sut.CheckPluginUpdatesAsync();
        Assert.That(result, Is.Empty);
    }
}

[TestFixture]
public class UpdateManagerVersionComparisonTests
{
    [TestCase("v1.2.3", "v1.2.2", 1)]
    [TestCase("v1.2.3", "v1.2.4", -1)]
    [TestCase("v1.2.3", "v1.2.3", 0)]
    [TestCase("v2.0.0", "v1.9.9", 1)]
    [TestCase("v0.1.0", "v0.1.1", -1)]
    [TestCase("v10.0.0", "v9.0.0", 1)]
    public void CompareVersions_CorrectlyOrders(string v1, string v2, int expected)
    {
        var method = typeof(UpdateManager).GetMethod("CompareVersions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.That(method, Is.Not.Null, "CompareVersions method not found");

        var result = (int)method!.Invoke(null, new object[] { v1, v2 })!;
        Assert.That(result, Is.EqualTo(expected), $"Expected CompareVersions(\"{v1}\", \"{v2}\") to return {expected}");
    }
}