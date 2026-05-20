namespace OpenLMStudio.Domain.Models.ContextCompression;

/// <summary>
/// Represents a compressed conversation entry for context management.
/// </summary>
public class CompressedEntry
{
    /// <summary>
    /// Human-readable summary of the compressed content.
    /// </summary>
    public string Summary { get; set; } = "";

    /// <summary>
    /// Key decisions extracted from the compressed content.
    /// </summary>
    public List<string> KeyDecisions { get; set; } = new();

    /// <summary>
    /// Files that were modified during the compressed content period.
    /// </summary>
    public List<string> FilesModified { get; set; } = new();

    /// <summary>
    /// Timestamp when this entry was created.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Statistics about context compression for UI display.
/// </summary>
public class CompressedStats
{
    /// <summary>
    /// Total number of messages in the context.
    /// </summary>
    public int TotalMessages { get; set; }

    /// <summary>
    /// Number of messages in the active window.
    /// </summary>
    public int ActiveWindowSize { get; set; }

    /// <summary>
    /// Number of compressed entries in the history.
    /// </summary>
    public int CompressedEntriesCount { get; set; }

    /// <summary>
    /// Estimated token count of active messages.
    /// </summary>
    public int EstimatedActiveTokens { get; set; }

    /// <summary>
    /// Estimated token count of compressed content.
    /// </summary>
    public int EstimatedCompressedTokens { get; set; }

    /// <summary>
    /// Compression ratio as a percentage (0-100).
    /// </summary>
    public int CompressionRatio { get; set; }
}