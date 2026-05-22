using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Tests;

/// <summary>
/// Test doubles for ITaskProgressTracker used by AgentTests.
/// </summary>
public class TestTaskProgressTracker : ITaskProgressTracker
{
    private TaskProgressStage _stage = TaskProgressStage.NotStarted;
    private int _progressPercentage;
    private readonly List<AgentToolCallRecord> _toolCalls = new();
    private string? _errorMessage;
    public int IterationLimit { get; set; }
    public int CurrentIteration { get; set; }

    public TaskProgressStage Stage => _stage;
    public int ProgressPercentage => _progressPercentage;
    public bool IsIterationLimitExceeded => IterationLimit > 0 && CurrentIteration >= IterationLimit;
    public bool HasError => _errorMessage != null;
    public string? ErrorMessage => _errorMessage;

    public Task UpdateStageAsync(TaskProgressStage newStage)
    {
        _stage = newStage;
        return Task.CompletedTask;
    }

    public Task ReportProgressAsync(int percentage)
    {
        _progressPercentage = Math.Clamp(percentage, 0, 100);
        return Task.CompletedTask;
    }

    public Task RecordToolCallAsync(string toolName, Dictionary<string, object> parameters, string result, bool success, double durationMs)
    {
        _toolCalls.Add(new AgentToolCallRecord { ToolName = toolName, Parameters = parameters, Result = result, Success = success, DurationMs = durationMs, Timestamp = DateTime.UtcNow });
        return Task.CompletedTask;
    }

    public Task RecordErrorAsync(string errorMessage)
    {
        _errorMessage = errorMessage;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _stage = TaskProgressStage.Failed;
        _errorMessage = "Test tracker disposed";
    }
}