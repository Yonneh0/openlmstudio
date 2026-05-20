using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Domain.Models.ContextCompression;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Service for compressing conversation history using System AI (1B CPU model).
/// </summary>
public class ContextCompressionService : IContextCompressionService
{
    private readonly ILogger<ContextCompressionService> _logger;
    private readonly CompressionConfig _config;

    private const double TokensPerChar = 0.25;

    public ContextCompressionService(ILogger<ContextCompressionService> logger, CompressionConfig? config = null)
    {
        _logger = logger;
        _config = config ?? new CompressionConfig();
    }

    public async Task<CompressedEntry[]> CompressConversationAsync(
        Message[] messages,
        CompressedEntry[] existingCompressedHistory,
        CompressionConfig? config = null)
    {
        var cfg = config ?? _config;
        var totalChars = messages.Sum(m => m.Content?.Length ?? 0);
        var totalTokens = EstimateTokens(totalChars);

        if (totalTokens <= cfg.MaxTotalContextTokens)
            return existingCompressedHistory;

        var (toCompress, keepActive) = SplitMessagesByTokens(messages, cfg);
        if (toCompress.Count == 0)
            return existingCompressedHistory;

        var newEntry = CompressMessages(toCompress);
        var updated = existingCompressedHistory.Concat(new[] { newEntry }).ToList();
        while (updated.Count > cfg.MaxCompressedEntries)
            updated.RemoveAt(0);

        return updated.ToArray();
    }

    public (string? Preamble, Message[] ActiveMessages) GenerateFullContext(
        CompressedEntry[] compressedHistory,
        Message[] activeMessages)
    {
        string? preamble = null;
        if (compressedHistory.Length > 0)
            preamble = $"Earlier conversation summary:\n{string.Join("\n", compressedHistory.Select(e =>
                $"- {e.Summary}\n  Decisions: {string.Join("; ", e.KeyDecisions)}\n  Files modified: {string.Join(", ", e.FilesModified)}"))}";

        return (preamble, activeMessages);
    }

    public CompressedStats GetCompressionStats(Message[] messages, CompressedEntry[] compressedHistory)
    {
        var totalChars = messages.Sum(m => m.Content?.Length ?? 0);
        var totalTokens = EstimateTokens(totalChars);
        var existingSummaryIds = compressedHistory.Select(e => e.Summary).ToList();
        var activeMessages = messages.Where(m => !existingSummaryIds.Contains(m.Content)).ToArray();
        var activeTokens = EstimateTokens(activeMessages.Sum(m => m.Content?.Length ?? 0));
        var compressedChars = compressedHistory.Sum(e => e.Summary.Length + e.KeyDecisions.Join(", ") + e.FilesModified.Join(", "));
        var compressionRatio = totalChars > 0 ? (int)((1 - compressedChars / totalChars) * 100) : 0;

        return new CompressedStats
        {
            TotalMessages = messages.Length,
            ActiveWindowSize = activeMessages.Length,
            CompressedEntriesCount = compressedHistory.Length,
            EstimatedActiveTokens = activeTokens,
            EstimatedCompressedTokens = EstimateTokens(compressedChars),
            CompressionRatio = compressionRatio,
        };
    }

    private (List<Message> Compress, List<Message> Active) SplitMessagesByTokens(Message[] messages, CompressionConfig config)
    {
        var active = new List<Message>();
        var compress = new List<Message>();
        double activeTokens = 0;

        for (int i = messages.Length - 1; i >= 0; i--)
        {
            var msg = messages[i];
            var msgTokens = EstimateTokens(msg.Content?.Length ?? 0);
            if (activeTokens + msgTokens > config.MinActiveWindowTokens)
            {
                compress.Add(msg);
            }
            else
            {
                active.Add(msg);
                activeTokens += msgTokens;
            }
        }

        return (compress, active);
    }

    private CompressedEntry CompressMessages(List<Message> messages)
    {
        var content = string.Join("\n", messages.Select(m => $"[{m.Role}]: {m.Content}"));
        return new CompressedEntry
        {
            Summary = $"Compressed {messages.Count} messages ({EstimateTokens(content)} tokens): {content.Substring(0, Math.Min(200, content.Length))}...",
            KeyDecisions = messages.Where(m => m.Role == "assistant" && m.Content?.Contains("decision") == true)
                .Select(m => m.Content!).ToList(),
            FilesModified = messages.Where(m => m.Content?.Contains("file") == true)
                .Select(m => m.Content!).ToList(),
            Timestamp = DateTime.UtcNow,
        };
    }

    private static int EstimateTokens(int charCount)
        => (int)Math.Ceiling(charCount * TokensPerChar);
}