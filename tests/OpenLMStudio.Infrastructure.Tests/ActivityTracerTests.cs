using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Infrastructure.Tracing;
using NUnit.Framework;

namespace OpenLMStudio.Infrastructure.Tests;

/// <summary>
/// Tests for <see cref="ActivityTracer"/>.
/// </summary>
public class ActivityTracerTests
{
    [Test]
    public void Constructor_CreatesTracer()
    {
        var tracer = new ActivityTracer(new TestLogger<ActivityTracer>());
        Assert.That(tracer, Is.Not.Null);
    }

    [Test]
    public void StartSpan_CreatesSpan()
    {
        var tracer = new ActivityTracer(new TestLogger<ActivityTracer>());
        var span = tracer.StartSpan("test-operation", "task-1");
        Assert.That(span, Is.Not.Null);
    }

    [Test]
    public void RecordToolCall_RecordsTrace()
    {
        var tracer = new ActivityTracer(new TestLogger<ActivityTracer>());
        tracer.RecordToolCall(new AgentToolCallTrace("task-1", "test-tool", "1.0.0", TimeSpan.FromMilliseconds(50).TotalMilliseconds, true, null, 0, 0, 0, DateTimeOffset.UtcNow));
        var calls = tracer.GetRecentToolCalls("task-1");
        Assert.That(calls, Is.Not.Empty);
    }

    [Test]
    public void GetTaskStatistics_ReturnsCounts()
    {
        var tracer = new ActivityTracer(new TestLogger<ActivityTracer>());
        tracer.RecordToolCall(new AgentToolCallTrace("task-1", "tool-1", "1.0.0", TimeSpan.FromMilliseconds(100).TotalMilliseconds, true, null, 0, 0, 0, DateTimeOffset.UtcNow));
        tracer.RecordToolCall(new AgentToolCallTrace("task-1", "tool-2", "1.0.0", TimeSpan.FromMilliseconds(200).TotalMilliseconds, false, null, 0, 0, 0, DateTimeOffset.UtcNow));
        var stats = tracer.GetTaskStatistics("task-1");
        Assert.Multiple(() =>
        {
            Assert.That(stats.TotalCalls, Is.EqualTo(2));
            Assert.That(stats.SuccessfulCalls, Is.EqualTo(1));
            Assert.That(stats.FailedCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public void Dispose_CleansUp()
    {
        var tracer = new ActivityTracer(new TestLogger<ActivityTracer>());
        tracer.Dispose();
        Assert.DoesNotThrow(() => tracer.Dispose());
    }
}