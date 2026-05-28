namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Represents information about a GPU device in the system.
/// </summary>
public record DeviceHardwareInfo(
    string Name,
    string Vendor,
    long TotalMemoryBytes,
    long FreeMemoryBytes,
    int ComputeCapability,
    bool IsCudaCompatible
);

/// <summary>
/// Immutable record holding memory information for a single model.
/// </summary>
public record ModelMemoryEntry(
    string ModelId,
    string ModelName,
    ModelType ModelType,
    long VramBytes,
    long CpuBytes,
    DateTime LastAccessed,
    int AccessCount);

/// <summary>
/// Represents a single log entry from a llama.cpp engine binary.
/// </summary>
public record LogEntry(
    string Id,
    DateTime Timestamp,
    LogLevel Level,
    string Message,
    EngineType Source,
    bool IsImportant = false,
    string? Category = null
);

/// <summary>
/// Form model for VM creation wizard state.
/// </summary>
public class VMCreationForm
{
    public string Name { get; set; } = "vm-1";
    public ArchitectureType Architecture { get; set; } = ArchitectureType.X86_64;
    public AcceleratorType Accelerator { get; set; } = AcceleratorType.KVM;
    public int CpuCores { get; set; } = 2;
    public int RamMB { get; set; } = 2048;
    public int DiskSizeGB { get; set; } = 20;
    public List<DiskImageConfig> DiskImages { get; set; } = new();
    public List<NetworkDeviceConfig> NetworkDevices { get; set; } = new();
}

/// <summary>
/// Result from a parallel subagent execution.
/// </summary>
public class SubagentResult
{
    /// <summary>Index of this subagent (1-5).</summary>
    public int Index { get; set; }

    /// <summary>The prompt given to this subagent.</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>The result produced by this subagent.</summary>
    public string? Result { get; set; }

    /// <summary>Error message if this subagent failed.</summary>
    public string? Error { get; set; }

    /// <summary>Whether this subagent succeeded.</summary>
    public bool Success => Error == null;

    /// <summary>Duration of the subagent execution in milliseconds.</summary>
    public double DurationMs { get; set; }
}

/// <summary>
/// Current status of a task.
/// </summary>
public enum TaskStatus
{
    /// <summary>Task is pending execution.</summary>
    Pending = 0,

    /// <summary>Task is currently running.</summary>
    Running = 1,

    /// <summary>Task is paused.</summary>
    Paused = 2,

    /// <summary>Task is queued and waiting for dependencies.</summary>
    Queued = 3,

    /// <summary>Task has failed.</summary>
    Failed = 4,

    /// <summary>Task has been cancelled.</summary>
    Cancelled = 5,

    /// <summary>Task has completed successfully.</summary>
    Completed = 6,
}

/// <summary>
/// Represents the current phase of a task's lifecycle.
/// </summary>
public enum TaskPhase
{
    /// <summary>Planning the approach and strategy.</summary>
    Planning = 1,

    /// <summary>Executing the planned actions.</summary>
    Acting = 2,

    /// <summary>Reviewing results and validating correctness.</summary>
    Reviewing = 3,

    /// <summary>Task is complete and finalizing.</summary>
    Completed = 4,

    /// <summary>Task has failed.</summary>
    Failed = 5,
}

// ============================================================
// AgentIgnoreRule.cs (64 lines)
// ============================================================

/// <summary>
/// Represents a single .agentignore rule for matching files.
/// Supports glob patterns similar to .gitignore.
/// </summary>
public class AgentIgnoreRule
{
    /// <summary>The raw pattern string (e.g., "*.log", "bin/**").</summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>Whether this rule is negated (e.g., "!*.log").</summary>
    public bool IsNegated { get; set; }

    /// <summary>Whether the pattern matches directory names.</summary>
    public bool MatchesDirectories { get; set; } = true;

    /// <summary>
    /// Checks if the given file path matches this rule.
    /// </summary>
    public bool Matches(string filePath)
    {
        var pattern = Pattern;
        if (pattern.StartsWith('!'))
            pattern = pattern.Substring(1);

        // Handle ** (match everything including subdirectories)
        if (pattern.Contains("**"))
        {
            var parts = pattern.Split(new[] { "**" }, StringSplitOptions.RemoveEmptyEntries);
            var lastPart = parts[^1].Trim('/');
            return filePath.EndsWith(lastPart, StringComparison.OrdinalIgnoreCase) ||
                   filePath.Contains(lastPart);
        }

        // Handle * (match within single directory level)
        if (pattern.Contains('*'))
        {
            var fileName = Path.GetFileName(filePath);
            return WildcardMatch(fileName, pattern) ||
                   WildcardMatch(filePath, pattern);
        }

        // Simple string match
        return filePath.Equals(pattern, StringComparison.OrdinalIgnoreCase) ||
               filePath.EndsWith($"/{pattern}", StringComparison.OrdinalIgnoreCase) ||
               filePath.Contains($"/{pattern}/");
    }

    /// <summary>
    /// Simple wildcard matching (* and ? patterns).
    /// </summary>
    private static bool WildcardMatch(string text, string pattern)
    {
        // Convert glob pattern to regex
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        return System.Text.RegularExpressions.Regex.IsMatch(
            text, regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}

// ============================================================
// BinaryInfo.cs (84 lines)
// ============================================================

/// <summary>
/// Supported backend types for engine binaries.
/// </summary>
public enum BackendType
{
    Cpu,
    Cuda,
    Metal,
    Vulkan
}

/// <summary>
/// Information about an engine binary (downloaded or locally compiled).
/// </summary>
public record BinaryInfo(
    string Id,              // e.g. "llama-server-cuda-12.2"
    string Name,            // e.g. "llama-server-cuda"
    BackendType Backend,
    string Platform,        // "windows", "linux", "macos"
    string Architecture,    // "x64", "arm64"
    string Version,         // "main", "v0.1.0", etc.
    string? Checksum,
    string? DownloadUrl,
    DateTime? DownloadDate,
    bool IsBuiltLocally,
    string? GitBranch,
    string? GitCommit,
    string? BuildDate,
    string? BuildFlags,
    string BinaryPath,
    string? ManifestPath
);

/// <summary>
/// Information about a GGUF model.
/// </summary>
public record GgufModelInfo(
    string Id,              // e.g. "llama-3.2-3b-q4_k_m"
    string Name,            // e.g. "llama-3.2-3b"
    string? Architecture,   // e.g. "llama"
    string? Quantization,   // e.g. "Q4_K_M"
    long? ContextLength,
    long? EmbeddingDim,
    long FileSizeBytes,
    string FilePath,
    string? ChatTemplate,
    string? Description,
    DateTime? LastUsed,
    int? UsageCount
);

/// <summary>
/// Smart recommendation for a model based on its characteristics.
/// </summary>
public record ModelRecommendation(
    string ModelId,
    string ModelName,
    string? Architecture,
    string? Quantization,
    long? ContextLength,
    long FileSizeBytes,
    RecommendedSettings Settings,
    string Reason
);

/// <summary>
/// Recommended settings for a model.
/// </summary>
public record RecommendedSettings
{
    public int GpuLayers { get; set; }
    public int ContextSize { get; set; }
    public int BatchSize { get; set; }
    public int Threads { get; set; }
    public bool FlashAttention { get; set; }
    public bool KvOffload { get; set; }
    public bool Mmap { get; set; }
    public bool Mlock { get; set; }
    public string? Pooling { get; set; }
    public bool Embedding { get; set; }
    public bool Reranking { get; set; }
}

// ============================================================
// SystemAIConfig.cs (49 lines)
// ============================================================

/// <summary>
/// Configuration for the System AI (llama.cpp) client.
/// </summary>
public class SystemAIConfig
{
    /// <summary>
    /// Path to the GGUF model file to use for System AI inference.
    /// </summary>
    public string ModelPath { get; set; } = "";

    /// <summary>
    /// Port for the llama-server process (default: 8081).
    /// </summary>
    public int Port { get; set; } = 8081;

    /// <summary>
    /// System prompt to use for the System AI.
    /// </summary>
    public string SystemPrompt { get; set; } = "You are a helpful assistant.";

    /// <summary>
    /// Temperature for sampling (lower = more deterministic).
    /// </summary>
    public float Temperature { get; set; } = 0.3f;

    /// <summary>
    /// Top P for sampling.
    /// </summary>
    public float TopP { get; set; } = 0.9f;

    /// <summary>
    /// Whether to use memory lock (mlock) for the model.
    /// </summary>
    public bool MemoryLock { get; set; } = true;

    /// <summary>
    /// Recommended backend for the llama-server process (cpu, cuda, metal, vulkan).
    /// Used by SystemAIClient to select the appropriate engine binary and GPU layer settings.
    /// </summary>
    public string? RecommendedBackend { get; set; }

    /// <summary>
    /// Number of GPU layers to offload (0 = CPU only, 99 = all layers).
    /// Only applies when using a GPU backend.
    /// </summary>
    public int GpuLayers { get; set; } = 35;
}

// ============================================================
// TokenEstimator.cs (108 lines)
// ============================================================

/// <summary>
/// Service implementation for token estimation operations.
/// Provides standardized token counting using character-based estimation.
/// </summary>
public class TokenEstimator
{
    /// <summary>
    /// Average characters per token for English text (approximate).
    /// Based on the observation that ~4 characters ≈ 1 token.
    /// </summary>
    private const double CharsPerToken = 4.0;

    /// <summary>
    /// Average characters per token for complex text with punctuation and special characters.
    /// </summary>
    private const double ComplexCharsPerToken = 3.5;

    /// <summary>
    /// Average characters per token for code/technical content.
    /// </summary>
    private const double CodeCharsPerToken = 3.0;

    /// <summary>
    /// Estimates the token count for a given text using standardized method: ~1 token per 4 characters for English.
    /// </summary>
    /// <param name="text">The text to estimate.</param>
    /// <returns>Estimated token count.</returns>
    public int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        return (int)Math.Ceiling(text.Length / CharsPerToken);
    }

    /// <summary>
    /// Estimates the token count using an advanced algorithm considering word boundaries.
    /// </summary>
    /// <param name="text">The text to estimate.</param>
    /// <returns>Estimated token count.</returns>
    public int EstimateTokensAdvanced(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // Count words, punctuation, and special characters
        var wordCount = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        var punctuationCount = text.Count(c => char.IsPunctuation(c));
        var newlineCount = text.Count(c => c == '\n');

        // Word-based estimation: each word is roughly 1-2 tokens
        // Punctuation adds ~0.1 tokens each
        // Newlines add ~0.5 tokens each
        var wordTokens = wordCount * 1.3;
        var punctTokens = punctuationCount * 0.1;
        var newlineTokens = newlineCount * 0.5;

        return (int)Math.Ceiling(wordTokens + punctTokens + newlineTokens);
    }

    /// <summary>
    /// Estimates the total token count for a list of messages.
    /// </summary>
    /// <param name="messages">Messages with role and content.</param>
    /// <returns>Estimated total token count.</returns>
    public int EstimateMessagesTokens(IEnumerable<(string Role, string Content)> messages)
    {
        if (messages == null)
            return 0;

        var totalTokens = 0;
        foreach (var (role, content) in messages)
        {
            var contentTokens = EstimateTokens(content);
            // Add role prefix tokens (e.g., "user: ", "assistant: ")
            var roleTokens = EstimateTokens(role);
            totalTokens += contentTokens + roleTokens;
        }

        // Add message separator overhead (~2 tokens per message)
        totalTokens += messages.Count() * 2;

        return totalTokens;
    }

    /// <summary>
    /// Gets a human-readable representation of a token count.
    /// </summary>
    /// <param name="tokenCount">The token count.</param>
    /// <returns>Human-readable string (e.g., "1.5K tokens").</returns>
    public string GetHumanReadableTokenCount(int tokenCount)
    {
        if (tokenCount < 0)
            return "0 tokens";

        if (tokenCount < 1000)
            return $"{tokenCount} tokens";

        if (tokenCount < 1_000_000)
            return $"{tokenCount / 1000.0:F1}K tokens";

        return $"{tokenCount / 1_000_000.0:F1}M tokens";
    }
}
