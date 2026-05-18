using System;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using OpenLMStudio.Infrastructure.Services;

namespace OpenLMStudio.Domain.Tests;

/// <summary>
/// Tests for the SandboxService to verify platform-specific support detection.
/// </summary>
[TestFixture]
public class SandboxServiceTests
{
    [Test]
    public void IsSupported_ReturnsTrueOnWindows()
    {
        // Arrange & Act
        var service = new SandboxService(NullLogger<SandboxService>.Instance);

        // Assert — Windows always supports Job Objects.
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            Assert.That(service.IsSupported, Is.True, "Sandbox should be supported on Windows");
    }

    [Test]
    public void CreateProcessAsync_ReturnsNegativeForInvalidCommandLine()
    {
        // Arrange
        var service = new SandboxService(NullLogger<SandboxService>.Instance);

        // Act — empty command line should fail.
        var result = Assert.ThrowsAsync<ArgumentException>(async () => await service.CreateProcessAsync(""));

        // Assert
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void CreateProcessWithSandboxPolicyAsync_ReturnsNegativeForInvalidCommandLine()
    {
        // Arrange
        var service = new SandboxService(NullLogger<SandboxService>.Instance);

        // Act — empty command line should fail.
        var result = Assert.ThrowsAsync<ArgumentException>(async () => await service.CreateProcessWithSandboxPolicyAsync(""));

        // Assert
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void GetActiveProcesses_ReturnsEmptyListWhenNoSandboxedProcesses()
    {
        // Arrange & Act
        var service = new SandboxService(NullLogger<SandboxService>.Instance);

        // Assert — should have no active processes.
        var active = service.GetActiveProcesses();
        CollectionAssert.IsEmpty(active, "Should have zero active sandboxed processes");
    }

    [Test]
    public void Dispose_DoesNotThrowWhenNoSandboxedProcesses()
    {
        // Arrange & Act — disposing with no processes should not throw.
        var service = new SandboxService(NullLogger<SandboxService>.Instance);
        Assert.DoesNotThrow(() => service.Dispose());
    }

    [Test]
    public void Dispose_IsIdempotent()
    {
        // Arrange & Act — calling dispose multiple times should be safe.
        var service = new SandboxService(NullLogger<SandboxService>.Instance);
        Assert.DoesNotThrow(() => { service.Dispose(); service.Dispose(); });
    }
}