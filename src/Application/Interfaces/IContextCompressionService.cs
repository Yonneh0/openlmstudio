using OpenLMStudio.Domain.Models;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Configuration for context compression.
/// </summary>
public class CompressionConfig
{
    public int MaxTotalContextTokens { get; set; } = 131072;
    public int MinActiveWindowTokens { get; set; } = 2048;
    public double ActiveWindowPercentage { get; set; } = 0.15;
    public int MaxCompressedEntries { get; set; } = 50;
}

/// <summary>
/// Service for compressing conversation history using System AI.
/// </summary>
public interface IContextCompressionService
{
    /// <summary>
    /// Compresses messages that exceed the token budget using System AI.
    /// </summary>
    Task<CompressedEntry[]> CompressConversationAsync(
        Message[] messages,
        CompressedEntry[] existingCompressedHistory,
        CompressionConfig? config = null);

    /// <summary>
    /// Generates the full context (preamble + active messages) for the AI.
    /// </summary>
    (string? Preamble, Message[] ActiveMessages) GenerateFullContext(
        CompressedEntry[] compressedHistory,
        Message[] activeMessages);

    /// <summary>
    /// Gets compression statistics for display.
    /// </summary>
    CompressedStats GetCompressionStats(Message[] messages, CompressedEntry[] compressedHistory);
}