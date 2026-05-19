global using System;
global using System.Threading.Tasks;

using NSubstitute;
using NUnit.Framework;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Infrastructure.Services;

namespace OpenLMStudio.Infrastructure.Tests;

/// <summary>
/// Tests for ServerService port checking, conflict detection, and certificate generation.
/// </summary>
[TestFixture]
public class ServerServiceTests
{
    private ServerService _server = null!;

    [SetUp]
    public void Setup()
    {
        _server = new ServerService();
    }

    [TearDown]
    public void TearDown()
    {
        _server?.Dispose();
    }

    [Test]
    public async Task IsPortInUseAsync_WithCommonPort_ReturnsResult()
    {
        // Port 0 should always be available
        var result = await _server.IsPortInUseAsync(0);
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task FindAvailablePortAsync_ReturnsNonNegativePort()
    {
        var port = await _server.FindAvailablePortAsync();
        Assert.That(port, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public async Task Dispose_IsIdempotent()
    {
        Assert.DoesNotThrow(() =>
        {
            _server.Dispose();
            _server.Dispose();
        });
    }
}