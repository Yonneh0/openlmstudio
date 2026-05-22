using NUnit.Framework;
using OpenLMStudio.Infrastructure.Services;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;
using System;
using System.Collections.Generic;
using AgentToolCallRecord = OpenLMStudio.Domain.Models.AgentToolCallRecord;

namespace OpenLMStudio.Infrastructure.Tests;

[TestFixture]
public class AgentCommunicationProtocolTests
{
    [Test]
    public void IsSafeOperation_ReturnsTrueForReadOnlyTools()
    {
        var protocol = new AgentCommunicationProtocol();
        Assert.That(protocol.IsSafeOperation("FileReadTool"), Is.True);
        Assert.That(protocol.IsSafeOperation("SearchFilesTool"), Is.True);
        Assert.That(protocol.IsSafeOperation("GitHistoryTool"), Is.True);
        Assert.That(protocol.IsSafeOperation("GitDiffTool"), Is.True);
        Assert.That(protocol.IsSafeOperation("ProjectExplorerTool"), Is.True);
    }

    [Test]
    public void IsSafeOperation_ReturnsFalseForWriteTools()
    {
        var protocol = new AgentCommunicationProtocol();
        Assert.That(protocol.IsSafeOperation("FileWriteTool"), Is.False);
        Assert.That(protocol.IsSafeOperation("FilePatchTool"), Is.False);
        Assert.That(protocol.IsSafeOperation("CommandExecuteTool"), Is.False);
    }

    [Test]
    public void RequiresUserApproval_DelegatesToIsSafeOperation()
    {
        var protocol = new AgentCommunicationProtocol();
        Assert.That(protocol.RequiresUserApproval("FileReadTool"), Is.False);
        Assert.That(protocol.RequiresUserApproval("FileWriteTool"), Is.True);
    }

    [Test]
    public void CreatePlanMessage_SetsCorrectPhase()
    {
        var protocol = new AgentCommunicationProtocol();
        var message = protocol.CreatePlanMessage(
            Guid.NewGuid(),
            "Test task",
            "Step 1: Read file\nStep 2: Modify file");

        Assert.That(message.Phase, Is.EqualTo(AgentCommunicationPhase.Planning));
        Assert.That(message.SenderRole, Is.EqualTo("agent"));
    }

    [Test]
    public void CreateActionMessage_SetsCorrectPhase()
    {
        var protocol = new AgentCommunicationProtocol();
        var message = protocol.CreateActionMessage(
            Guid.NewGuid(),
            "FileWriteTool",
            "File written successfully.",
            true);

        Assert.That(message.Phase, Is.EqualTo(AgentCommunicationPhase.Acting));
        Assert.That(message.SenderRole, Is.EqualTo("agent"));
    }

    [Test]
    public void CreateCompletionMessage_SetsCorrectPhase()
    {
        var protocol = new AgentCommunicationProtocol();
        var message = protocol.CreateCompletionMessage(
            Guid.NewGuid(),
            "Task completed.",
            5,
            new List<AgentToolCallRecord>
            {
                new AgentToolCallRecord { TaskId = Guid.NewGuid(), ToolName = "FileReadTool", Parameters = new Dictionary<string, object>(), Result = "File read", Success = true, DurationMs = 10, Timestamp = DateTime.UtcNow },
                new AgentToolCallRecord { TaskId = Guid.NewGuid(), ToolName = "FileWriteTool", Parameters = new Dictionary<string, object>(), Result = "File written", Success = true, DurationMs = 15, Timestamp = DateTime.UtcNow }
            });

        Assert.That(message.Phase, Is.EqualTo(AgentCommunicationPhase.Completed));
    }

    [Test]
    public void SubscribeAndUnsubscribe_ListenerEventFlow()
    {
        var protocol = new AgentCommunicationProtocol();
        var events = new List<PhaseTransitionEvent>();

        protocol.Subscribe(events.Add);

        protocol.RaisePhaseTransition(new PhaseTransitionEvent(
            Guid.NewGuid(),
            AgentCommunicationPhase.Planning,
            AgentCommunicationPhase.Acting,
            "User approved the plan."));

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].To, Is.EqualTo(AgentCommunicationPhase.Acting));

        protocol.Unsubscribe(events.Add);

        protocol.RaisePhaseTransition(new PhaseTransitionEvent(
            Guid.NewGuid(),
            AgentCommunicationPhase.Acting,
            AgentCommunicationPhase.Completed,
            "Task done."));

        // Should still have only one event (unsubscribed listener not called)
        Assert.That(events, Has.Count.EqualTo(1));
    }

    [Test]
    public void Dispose_ClearsListeners()
    {
        var protocol = new AgentCommunicationProtocol();
        protocol.Dispose();
        protocol.Dispose(); // Second dispose should not throw
    }

    [Test]
    public void Constructor_CreatesWithNullLogger()
    {
        var protocol = new AgentCommunicationProtocol();
        Assert.That(protocol, Is.Not.Null);
    }
}