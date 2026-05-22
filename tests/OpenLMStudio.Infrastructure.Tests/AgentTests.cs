using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Infrastructure.Services;
using NUnit.Framework;

namespace OpenLMStudio.Infrastructure.Tests;

/// <summary>
/// Infrastructure-level tests for Agent execution and error recovery.
/// </summary>
[TestFixture]
public class AgentTests
{
    private TestTaskProgressTracker _tracker = null!;
    private TestToolRegistry _toolRegistry = null!;
    private TestChatCompletionService _chatService = null!;
    private TestChatContextManager _contextManager = null!;
    private Agent _agent = null!;

    [SetUp]
    public void SetUp()
    {
        _tracker = new TestTaskProgressTracker();
        _toolRegistry = new TestToolRegistry();
        _chatService = new TestChatCompletionService();
        _contextManager = new TestChatContextManager();

        _agent = new Agent(
            null,
            _tracker,
            toolRegistry: _toolRegistry,
            chatService: _chatService,
            contextManager: _contextManager);
    }

    [TearDown]
    public void TearDown()
    {
        _agent?.Dispose();
    }

    [Test]
    public void ExecuteAsync_WithValidTools_CompletesSuccessfully()
    {
        _toolRegistry.AddTool("echo", new EchoTool());

        var request = new AgentTaskRequest(
            Guid.NewGuid(),
            "Echo the word hello");

        var result = _agent.ExecuteAsync(request).GetAwaiter().GetResult();

        Assert.That(result.FinalState, Is.EqualTo(AgentState.Completed));
    }

    [Test]
    public void GetToolCalls_ReturnsRecordedCalls()
    {
        _toolRegistry.AddTool("echo", new EchoTool());

        var request = new AgentTaskRequest(Guid.NewGuid(), "Echo hello");
        _agent.ExecuteAsync(request).GetAwaiter().GetResult();

        var calls = _agent.GetToolCalls();
        Assert.That(calls, Is.Not.Null);
    }

    [Test]
    public void GetConversationHistory_ReturnsRecordedHistory()
    {
        _toolRegistry.AddTool("echo", new EchoTool());

        var request = new AgentTaskRequest(Guid.NewGuid(), "Echo hello");
        _agent.ExecuteAsync(request).GetAwaiter().GetResult();

        var history = _agent.GetConversationHistory();
        Assert.That(history, Is.Not.Null);
    }

    [Test]
    public void ResumeAsync_WithoutRequest_DoesNothing()
    {
        var agent = new Agent(
            null,
            _tracker,
            toolRegistry: new TestToolRegistry(),
            chatService: new TestChatCompletionService(),
            contextManager: new TestChatContextManager());

        agent.ResumeAsync().GetAwaiter().GetResult();

        Assert.That(agent.State, Is.EqualTo(AgentState.Idle));
    }

    [Test]
    public void AgentState_EnumHasAllExpectedValues()
    {
        Assert.That(Enum.IsDefined(typeof(AgentState), AgentState.Idle), Is.True);
        Assert.That(Enum.IsDefined(typeof(AgentState), AgentState.Planning), Is.True);
        Assert.That(Enum.IsDefined(typeof(AgentState), AgentState.Acting), Is.True);
        Assert.That(Enum.IsDefined(typeof(AgentState), AgentState.Paused), Is.True);
        Assert.That(Enum.IsDefined(typeof(AgentState), AgentState.Completed), Is.True);
        Assert.That(Enum.IsDefined(typeof(AgentState), AgentState.Failed), Is.True);
    }

    [Test]
    public void TaskProgressStage_EnumHasAllExpectedValues()
    {
        Assert.That(Enum.IsDefined(typeof(TaskProgressStage), TaskProgressStage.NotStarted), Is.True);
        Assert.That(Enum.IsDefined(typeof(TaskProgressStage), TaskProgressStage.InProgress), Is.True);
        Assert.That(Enum.IsDefined(typeof(TaskProgressStage), TaskProgressStage.Reviewing), Is.True);
        Assert.That(Enum.IsDefined(typeof(TaskProgressStage), TaskProgressStage.Completed), Is.True);
        Assert.That(Enum.IsDefined(typeof(TaskProgressStage), TaskProgressStage.Failed), Is.True);
    }

    [Test]
    public void AgentCheckpoint_SerializesCorrectly()
    {
        var checkpoint = new AgentCheckpoint(
            Iteration: 5,
            ToolCalls: new List<AgentToolCallRecord>(),
            ConversationHistory: new List<AgentMessageExchange>(),
            State: AgentState.Planning,
            Timestamp: DateTime.UtcNow);

        Assert.That(checkpoint.Iteration, Is.EqualTo(5));
        Assert.That(checkpoint.State, Is.EqualTo(AgentState.Planning));
        Assert.That(checkpoint.Timestamp, Is.Not.Null);
    }

    [Test]
    public void AgentToolCallRecord_CanBeCreated()
    {
        var record = new AgentToolCallRecord
        {
            ToolName = "TestTool",
            Parameters = new Dictionary<string, object> { { "param", "value" } },
            Result = "success",
            Success = true,
            DurationMs = 100.0,
            Timestamp = DateTime.UtcNow
        };

        Assert.That(record.ToolName, Is.EqualTo("TestTool"));
        Assert.That(record.Success, Is.True);
    }

    [Test]
    public void AgentMessageExchange_CanBeCreated()
    {
        var exchange = new AgentMessageExchange(
            Guid.NewGuid(),
            "agent",
            "planning",
            "Test content");

        Assert.That(exchange.SenderRole, Is.EqualTo("agent"));
        Assert.That(exchange.Content, Is.EqualTo("Test content"));
    }

    [Test]
    public void AgentTaskResult_CanBeCreated()
    {
        var result = new AgentTaskResult(
            Guid.NewGuid(),
            AgentState.Completed,
            new List<AgentToolCallRecord>(),
            "Task completed.");

        Assert.That(result.FinalState, Is.EqualTo(AgentState.Completed));
    }

    [Test]
    public void TestTaskProgressTracker_TracksStateChanges()
    {
        var tracker = new TestTaskProgressTracker();

        tracker.UpdateStageAsync(TaskProgressStage.InProgress).GetAwaiter().GetResult();
        Assert.That(tracker.Stage, Is.EqualTo(TaskProgressStage.InProgress));

        tracker.ReportProgressAsync(50).GetAwaiter().GetResult();
        Assert.That(tracker.ProgressPercentage, Is.EqualTo(50));

        tracker.RecordErrorAsync("test error").GetAwaiter().GetResult();
        Assert.That(tracker.HasError, Is.True);
        Assert.That(tracker.ErrorMessage, Is.EqualTo("test error"));
    }

    [Test]
    public void TestTaskProgressTracker_IsIterationLimitExceeded_ReturnsFalseWhenBelowLimit()
    {
        var tracker = new TestTaskProgressTracker();
        Assert.That(tracker.IsIterationLimitExceeded, Is.False);
    }

    [Test]
    public void TestTaskProgressTracker_IsIterationLimitExceeded_ReturnsTrueWhenAtLimit()
    {
        var tracker = new TestTaskProgressTracker();
        tracker.IterationLimit = 10;
        tracker.CurrentIteration = 10;

        Assert.That(tracker.IsIterationLimitExceeded, Is.True);
    }

    #region Test Helpers

    private class EchoTool : ITool
    {
        public string Name => "echo";
        public string Description => "Echoes input text";

        public async Task<bool> ExecuteAsync(Dictionary<string, object> parameters) => true;
        public Dictionary<string, ToolParameterSchema> GetParameterSchema() => new();
        public void Dispose() { }
    }

    private class TestToolRegistry : IToolRegistry
    {
        private readonly Dictionary<string, ITool> _tools = new();

        public IReadOnlyDictionary<string, ITool> GetTools() => _tools;
        public bool RegisterTool(string name, ITool tool) { _tools[name] = tool; return true; }
        public bool UnregisterTool(string name) => _tools.Remove(name);
        public ITool? GetTool(string name) => _tools.TryGetValue(name, out var t) ? t : null;
        public void Register(ITool tool) => _tools[tool.Name] = tool;
        public bool Unregister(string name) => _tools.Remove(name);
        public void Dispose() { _tools.Clear(); }
        public void AddTool(string name, ITool tool) => _tools[name] = tool;
    }

    private class TestChatCompletionService : IChatCompletionService
    {
        public Task<ChatResponseChoice> GetCompletionAsync(ChatRequest request)
        {
            var msg = new Message { Role = MessageRole.Assistant, Content = "Plan: Echo the word 'hello'." };
            return Task.FromResult(new ChatResponseChoice(msg, null, 0));
        }

        public IAsyncEnumerable<string> GetStreamingCompletionAsync(ChatRequest request)
        {
            throw new NotImplementedException();
        }
    }

    private class TestChatContextManager : IChatContextManager
    {
        private readonly List<ContextSegment> _segments = new();

        public Task<ContextWindow> GetCompressedContextAsync(Guid chatId, CompressionLevel level)
        {
            var window = new ContextWindow
            {
                Segments = _segments,
                TotalTokenCount = 0,
                OverallCompression = level
            };
            return Task.FromResult(window);
        }

        public Task PinSegmentAsync(Guid chatId, Guid segmentId) => Task.CompletedTask;
        public Task UnpinSegmentAsync(Guid chatId, Guid segmentId) => Task.CompletedTask;
        public Task SuppressSegmentAsync(Guid chatId, Guid segmentId) => Task.CompletedTask;
        public Task RevealSegmentAsync(Guid chatId, Guid segmentId) => Task.CompletedTask;

        public Task<ContextSegment> InjectCustomContextAsync(Guid chatId, string content, ContextInjectionType type)
        {
            var segment = new ContextSegment
            {
                Id = Guid.NewGuid(),
                Content = content,
                Role = MessageRole.System,
                InjectionType = type,
                TokenCount = 0
            };
            _segments.Add(segment);
            return Task.FromResult(segment);
        }

        public Task RemoveCustomContextAsync(Guid chatId, Guid segmentId)
        {
            _segments.RemoveAll(s => s.Id == segmentId);
            return Task.CompletedTask;
        }

        public void Dispose() { _segments.Clear(); }
    }

    #endregion
}