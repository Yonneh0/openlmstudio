using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Infrastructure.Services;
using NUnit.Framework;

namespace OpenLMStudio.Infrastructure.Tests;

/// <summary>
/// Tests for <see cref="TaskCompletionDetector"/>.
/// </summary>
public class TaskCompletionDetectorTests
{
    private TaskCompletionDetector _detector = null!;

    [SetUp]
    public void SetUp()
    {
        var logger = new TestLogger<TaskCompletionDetector>();
        var validator = new MockValidationService();
        _detector = new TaskCompletionDetector(logger, validator);
    }

    [Test]
    public void QuickHeuristicCheck_ReturnsCompleted_WhenTaskAlreadyCompleted()
    {
        var task = new AgenticTask
        {
            Id = Guid.NewGuid(),
            Description = "Test task",
            Status = Domain.Models.TaskStatus.Completed,
            Priority = TaskPriority.Normal,
            MaxIterations = 5
        };
        var result = TaskCompletionDetector.QuickHeuristicCheck(task, Array.Empty<AgentToolCallRecord>());
        Assert.That(result.Passed, Is.True);
    }

    [Test]
    public void QuickHeuristicCheck_ReturnsFailed_WhenMaxIterationsReached()
    {
        var task = new AgenticTask
        {
            Id = Guid.NewGuid(),
            Description = "Test task",
            Status = Domain.Models.TaskStatus.Running,
            Priority = TaskPriority.Normal,
            MaxIterations = 5
        };
        var toolCalls = new[]
        {
            new AgentToolCallRecord
            {
                TaskId = task.Id,
                ToolName = "Tool1",
                Parameters = new Dictionary<string, object>(),
                Result = "result",
                Success = true,
                DurationMs = 10,
                Timestamp = DateTime.UtcNow
            }
        };
        var result = TaskCompletionDetector.QuickHeuristicCheck(task, toolCalls);
        Assert.That(result.Passed, Is.False);
    }

    private class MockValidationService : ITaskValidationService
    {
        public Task<TaskValidationResult> ValidateTaskCompletionAsync(AgenticTask task, string summary, CancellationToken ct = default)
            => Task.FromResult(new TaskValidationResult(false, "Mock: not validated"));
        public Task<TaskValidationResult> ValidateStructuredOutputAsync(AgenticTask task, Dictionary<string, object> output, CancellationToken ct = default)
            => Task.FromResult(new TaskValidationResult(true, "Mock: validated"));
    }
}