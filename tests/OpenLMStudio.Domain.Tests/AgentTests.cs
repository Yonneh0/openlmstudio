using NUnit.Framework;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Domain.Tests;

/// <summary>
/// Tests for AgentState enum completeness — verifies that idle state is defined for the agent harness.
/// </summary>
[TestFixture]
public class AgentStateTests
{
    [Test]
    public void AgentState_HasIdleState()
    {
        Assert.That((int)AgentState.Idle, Is.GreaterThan(0));
    }

    [Test]
    public void AgentState_ContainsAllExpectedStates()
    {
        var expectedStates = new[]
        {
            AgentState.NotStarted,
            AgentState.Planning,
            AgentState.Acting,
            AgentState.Paused,
            AgentState.Completed,
            AgentState.Failed,
            AgentState.Idle
        };

        var actualValues = Enum.GetValues<AgentState>().Cast<int>().ToList();
        foreach (var state in expectedStates)
        {
            Assert.That(actualValues, Does.Contain((int)state), $"Missing state: {state}");
        }
    }
}