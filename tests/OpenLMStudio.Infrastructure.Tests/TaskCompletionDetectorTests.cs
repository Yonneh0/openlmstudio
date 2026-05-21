using NUnit.Framework;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Infrastructure.Services;

namespace OpenLMStudio.Infrastructure.Tests;

[TestFixture]
public class TaskCompletionDetectorTests
{
    private TaskCompletionDetector _detector = null!;

    [SetUp]
    public void SetUp()
    {
        _detector = new TaskCompletionDetector();
    }

    [TearDown]
    public void TearDown()
    {
        _detector?.Dispose();
    }

    [Test]
    public void DetectAsync_ReturnsTrue_WhenToolOutputContainsCompletionSignal()
    {
        var toolCalls = new List<AgentToolCallRecord>
        {
            new("FileWriteTool", new Dictionary<string, object>(), "File written successfully.", true, 10, DateTime.UtcNow)
        };

        var result = _detector.DetectAsync("Create a file", toolCalls).Result;
        Assert.That(result, Is.True);
    }

    [Test]
    public void DetectAsync_ReturnsTrue_WhenGitOperationsComplete()
    {
        var toolCalls = new List<AgentToolCallRecord>
        {
            new("GitHistoryTool", new Dictionary<string, object>(), "Git operations completed successfully.", true, 10, DateTime.UtcNow)
        };

        var result = _detector.DetectAsync("Check git history", toolCalls).Result;
        Assert.That(result, Is.True);
    }

    [Test]
    public void DetectAsync_ReturnsTrue_WhenReadOnlyTaskFewCalls()
    {
        var toolCalls = new List<AgentToolCallRecord>
        {
            new("FileReadTool", new Dictionary<string, object>(), "File content", true, 5, DateTime.UtcNow),
            new("ProjectExplorerTool", new Dictionary<string, object>(), "Directory listing", true, 5, DateTime.UtcNow)
        };

        var result = _detector.DetectAsync("Read project files", toolCalls).Result;
        Assert.That(result, Is.True);
    }

    [Test]
    public void DetectAsync_ReturnsFalse_WhenNoCompletionSignals()
    {
        // Use tools that are NOT FileWriteTool or GitHistoryTool, and contain
        // no completion keywords, so the detector returns false.
        var toolCalls = new List<AgentToolCallRecord>
        {
            new("CommandExecuteTool", new Dictionary<string, object>(), "Some partial output without completion signals", true, 10, DateTime.UtcNow),
            new("CommandExecuteTool", new Dictionary<string, object>(), "More output without completion signals", true, 10, DateTime.UtcNow)
        };

        var result = _detector.DetectAsync("Run some commands", toolCalls).Result;
        Assert.That(result, Is.False);
    }

    [Test]
    public void DetectAsync_FallsBackToKeywordDetection_WhenLlmIsNull()
    {
        var detector = new TaskCompletionDetector(
            logger: null,
            chatService: null);

        var toolCalls = new List<AgentToolCallRecord>
        {
            new("FileWriteTool", new Dictionary<string, object>(), "File written successfully.", true, 10, DateTime.UtcNow)
        };

        var result = detector.DetectAsync("Create a file", toolCalls, useLlmFallback: true).Result;
        Assert.That(result, Is.True);
        detector.Dispose();
    }

    [Test]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        _detector.Dispose();
        _detector.Dispose();
        // Should not throw
    }
}