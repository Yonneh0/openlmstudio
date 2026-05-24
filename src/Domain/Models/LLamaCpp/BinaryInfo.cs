namespace OpenLMStudio.Domain.Models.LLamaCpp;

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
