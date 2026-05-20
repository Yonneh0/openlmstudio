using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Interfaces;
using OpenLMStudio.Infrastructure.Services;
using NUnit.Framework;

namespace OpenLMStudio.Infrastructure.Tests;

/// <summary>
/// Tests for <see cref="SandboxService"/>.
/// </summary>
public class SandboxServiceTests
{
    [Test]
    public void IsSupported_DetectsWindows()
    {
        var logger = new TestLogger<SandboxService>();
        var sandbox = new SandboxService(logger);
        // Windows should be supported via Job Objects.
        if (OperatingSystem.IsWindows())
        {
            Assert.That(sandbox.IsSupported, Is.True);
        }
        sandbox.Dispose();
    }

    [Test]
    public void Dispose_IsIdempotent()
    {
        var logger = new TestLogger<SandboxService>();
        var sandbox = new SandboxService(logger);
        sandbox.Dispose();
        Assert.DoesNotThrow(() => sandbox.Dispose()); // Second dispose should not throw.
    }
}