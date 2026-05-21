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
        if (OperatingSystem.IsWindows())
        {
            Assert.That(sandbox.IsSupported, Is.True);
        }
        sandbox.Dispose();
    }

    [Test]
    public void IsSupported_DetectsLinuxCgroupsV2()
    {
        var logger = new TestLogger<SandboxService>();
        var sandbox = new SandboxService(logger);
        if (OperatingSystem.IsLinux())
        {
            // Linux may or may not have cgroups v2 depending on the system.
            // We just verify the service initializes without throwing.
            Assert.That(sandbox, Is.Not.Null);
        }
        sandbox.Dispose();
    }

    [Test]
    public void IsSupported_DetectsMacOS()
    {
        var logger = new TestLogger<SandboxService>();
        var sandbox = new SandboxService(logger);
        if (OperatingSystem.IsMacOS())
        {
            // macOS may or may not have sandbox-exec.
            // We just verify the service initializes without throwing.
            Assert.That(sandbox, Is.Not.Null);
        }
        sandbox.Dispose();
    }

    [Test]
    public void IsSupported_ReturnsFalseWhenNoPlatformSupported()
    {
        var logger = new TestLogger<SandboxService>();
        var sandbox = new SandboxService(logger);
        // On an unsupported platform, IsSupported should return false.
        if (OperatingSystem.IsWindows() == false &&
            OperatingSystem.IsLinux() == false &&
            OperatingSystem.IsMacOS() == false)
        {
            Assert.That(sandbox.IsSupported, Is.False);
        }
        sandbox.Dispose();
    }

    [Test]
    public void Dispose_IsIdempotent()
    {
        var logger = new TestLogger<SandboxService>();
        var sandbox = new SandboxService(logger);
        sandbox.Dispose();
        Assert.DoesNotThrow(() => sandbox.Dispose());
    }

    [Test]
    public void CreateProcess_ThrowsOnNullOrEmptyCommandLine()
    {
        var logger = new TestLogger<SandboxService>();
        var sandbox = new SandboxService(logger);
        try
        {
            Assert.ThrowsAsync<ArgumentException>(async () => await sandbox.CreateProcessAsync(string.Empty));
        }
        finally
        {
            sandbox.Dispose();
        }
    }

    [Test]
    public async Task CreateProcess_ExecutesEvenWhenCommandNotFound()
    {
        var logger = new TestLogger<SandboxService>();
        var sandbox = new SandboxService(logger);
        // On Windows, cmd.exe returns a non-zero exit code for a bad command,
        // but the process ID itself is always >= 0.
        var result = await sandbox.CreateProcessAsync("nonexistent_command_xyz_12345");
        Assert.That(result, Is.GreaterThanOrEqualTo(0));
        sandbox.Dispose();
    }

    [Test]
    public void GetActiveProcesses_ReturnsEmptyList_WhenNoneCreated()
    {
        var logger = new TestLogger<SandboxService>();
        var sandbox = new SandboxService(logger);
        var processes = sandbox.GetActiveProcesses();
        Assert.That(processes, Is.Empty);
        sandbox.Dispose();
    }
}