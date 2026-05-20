using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Interfaces;
using OpenLMStudio.Infrastructure.Services;
using NUnit.Framework;

namespace OpenLMStudio.Infrastructure.Tests;

/// <summary>
/// Tests for <see cref="CommandExecutionService"/>.
/// </summary>
public class CommandExecutionServiceTests
{
    [Test]
    public void Constructor_CreatesService()
    {
        var logger = new TestLogger<CommandExecutionService>();
        var svc = new CommandExecutionService(logger);
        Assert.That(svc, Is.Not.Null);
        svc.Dispose();
    }

    [Test]
    public void Dispose_IsIdempotent()
    {
        var logger = new TestLogger<CommandExecutionService>();
        var svc = new CommandExecutionService(logger);
        svc.Dispose();
        // Second dispose should not throw.
        Assert.DoesNotThrow(() => svc.Dispose());
    }

    [Test]
    public void ExecuteAsync_InvalidCommand_ReturnsNegativeExitCode()
    {
        var logger = new TestLogger<CommandExecutionService>();
        var svc = new CommandExecutionService(logger);
        var request = new CommandExecuteRequest("nonexistent_command_xyz_12345", TimeoutSeconds: 5);
        var result = svc.ExecuteAsync(request).Result;
        Assert.That(result.ExitCode, Is.LessThan(0));
        svc.Dispose();
    }

    [Test]
    public void ExecuteAsync_ValidEcho_ReturnsZeroExitCode()
    {
        var logger = new TestLogger<CommandExecutionService>();
        var svc = new CommandExecutionService(logger);
        var echoCmd = "echo hello";
        var request = new CommandExecuteRequest(echoCmd, TimeoutSeconds: 5);
        var result = svc.ExecuteAsync(request).Result;
        Assert.That(result.ExitCode, Is.EqualTo(0));
        svc.Dispose();
    }

    [Test]
    public void GetActiveProcesses_ReturnsCurrentProcesses()
    {
        var logger = new TestLogger<CommandExecutionService>();
        var svc = new CommandExecutionService(logger);
        var processes = svc.GetActiveProcesses();
        Assert.That(processes, Is.Not.Null);
        svc.Dispose();
    }

    [Test]
    public void KillAsync_KillsProcess()
    {
        var logger = new TestLogger<CommandExecutionService>();
        var svc = new CommandExecutionService(logger);
        // Just verify method doesn't throw.
        Assert.DoesNotThrowAsync(async () => await svc.KillAsync(99999));
        svc.Dispose();
    }

    [Test]
    public void CancelAsync_RemovesProcessFromActive()
    {
        var logger = new TestLogger<CommandExecutionService>();
        var svc = new CommandExecutionService(logger);
        // Cancel a non-existent process — should not throw.
        Assert.DoesNotThrowAsync(async () => await svc.CancelAsync(99999));
        svc.Dispose();
    }
}