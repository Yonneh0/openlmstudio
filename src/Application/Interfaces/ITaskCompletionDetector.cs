using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Detects whether an agent task has been completed based on tool results and goal verification.
/// Supports both keyword-based detection (no LLM needed) and LLM-assisted detection.
/// </summary>
public interface ITaskCompletionDetector
{
    /// <summary>
    /// Detects task completion using keyword-based analysis.
    /// </summary>
    Task<bool> DetectAsync(string taskDescription, IReadOnlyList<AgentToolCallRecord> toolCalls);

    /// <summary>
    /// Detects task completion using an LLM to compare the goal against tool results.
    /// Falls back to keyword detection if the LLM call fails.
    /// </summary>
    Task<bool> DetectAsync(string taskDescription, IReadOnlyList<AgentToolCallRecord> toolCalls, bool useLlmFallback);
}