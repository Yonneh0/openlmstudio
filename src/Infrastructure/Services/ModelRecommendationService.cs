using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models.LLamaCpp;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Provides smart default settings for models based on their characteristics (size, name, architecture).
/// Includes specific optimizations for common model types.
/// </summary>
public class ModelRecommendationService
{
    private readonly ILogger<ModelRecommendationService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="ModelRecommendationService"/>.
    /// </summary>
    public ModelRecommendationService(ILogger<ModelRecommendationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generates a recommendation for a GGUF model based on its characteristics.
    /// </summary>
    public ModelRecommendation GetRecommendation(GgufModelInfo model)
    {
        var settings = InferSettings(model);
        var reason = BuildReason(model, settings);
        return new ModelRecommendation(model.Id, model.Name, model.Architecture, model.Quantization,
            model.ContextLength, model.FileSizeBytes, settings, reason);
    }

    /// <summary>
    /// Gets recommended settings for a model by its name (quick lookup for models without full metadata).
    /// </summary>
    public ModelRecommendation GetRecommendationByName(string modelName, long fileSizeBytes)
    {
        var model = new GgufModelInfo(
            Id: "unknown",
            Name: modelName,
            Architecture: InferArchitecture(modelName),
            Quantization: InferQuantization(modelName),
            ContextLength: null,
            EmbeddingDim: null,
            FileSizeBytes: fileSizeBytes,
            FilePath: "",
            ChatTemplate: null,
            Description: null,
            LastUsed: null,
            UsageCount: null);

        return GetRecommendation(model);
    }

    private static RecommendedSettings InferSettings(GgufModelInfo model)
    {
        // Infer parameter count from file size (rough estimate)
        var sizeGB = (double)model.FileSizeBytes / (1024.0 * 1024 * 1024);
        var paramEstimate = InferParameterCount(model.Name, sizeGB);

        // Check for specific model optimizations
        var isSmallModel = paramEstimate < 3_000_000_000L; // < 3B parameters
        var isMediumModel = paramEstimate >= 3_000_000_000L && paramEstimate < 70_000_000_000L;
        var isLargeModel = paramEstimate >= 70_000_000_000L;

        // Check for specific model names
        var name = (model.Name ?? "").ToLowerInvariant();
        var isQwen = name.Contains("qwen") || name.Contains("huihui");
        var isLlama = name.Contains("llama");
        var isMistral = name.Contains("mistral");
        var isGrok = name.Contains("grok");
        var isDeepSeek = name.Contains("deepseek");

        // GPU layers based on parameter count and model type
        var gpuLayers = isSmallModel
            ? 99 // Small models: offload all layers to GPU
            : isQwen && paramEstimate >= 20_000_000_000L
                ? 60 // Qwen 35B: moderate GPU offload for CUDA
                : isMediumModel
                    ? 60
                    : isLargeModel ? 30 : 99;

        // Context size based on model architecture
        var ctxSize = isSmallModel
            ? 8192
            : isQwen ? 32768 // Qwen models support larger context
            : isMediumModel ? 16384 : 32768;

        // Batch size
        var batchSize = isSmallModel
            ? 512
            : isMediumModel ? 1024 : 2048;

        // Thread count based on CPU cores
        var threads = Environment.ProcessorCount;
        if (isLargeModel && threads > 8)
            threads = threads / 2; // Large models benefit from fewer threads

        // Feature flags
        var flashAttention = isQwen || isLlama || isMistral || isDeepSeek;
        var kvOffload = isMediumModel || isLargeModel;
        var mmap = true;
        var mlock = isSmallModel; // Small models benefit from memory lock
        var embedding = IsEmbeddingModel(name, model.Architecture);
        var reranking = IsRerankingModel(name);
        var pooling = embedding ? "cls" : (reranking ? "rank" : null);

        return new RecommendedSettings(
            GpuLayers: gpuLayers,
            ContextSize: ctxSize,
            BatchSize: batchSize,
            Threads: threads,
            FlashAttention: flashAttention,
            KvOffload: kvOffload,
            Mmap: mmap,
            Mlock: mlock,
            Pooling: pooling,
            Embedding: embedding,
            Reranking: reranking);
    }

    private static string BuildReason(GgufModelInfo model, RecommendedSettings settings)
    {
        var parts = new List<string>();
        var paramEstimate = InferParameterCount(model.Name, (double)model.FileSizeBytes / (1024.0 * 1024 * 1024));
        parts.Add($"~{paramEstimate / 1_000_000_000}B parameters");

        if (settings.GpuLayers >= 90)
            parts.Add("full GPU offload");
        else if (settings.GpuLayers >= 50)
            parts.Add("partial GPU offload");

        if (model.Quantization != null)
            parts.Add($"{model.Quantization} quantized");

        if (settings.Embedding)
            parts.Add("embedding model");
        else if (settings.Reranking)
            parts.Add("reranking model");

        return $"Recommended for {string.Join(", ", parts)}";
    }

    private static long InferParameterCount(string name, double sizeGB)
    {
        // Quick heuristic: GGUF models are roughly 2 bytes per parameter (f16)
        // Quantized models are less. Q4_K_M is roughly 0.5 bytes per parameter.
        var quantFactor = 1.0;
        var q = (name ?? "").ToLowerInvariant();
        if (q.Contains("q2_")) quantFactor = 0.25;
        else if (q.Contains("q3_")) quantFactor = 0.35;
        else if (q.Contains("q4_")) quantFactor = 0.5;
        else if (q.Contains("q5_")) quantFactor = 0.65;
        else if (q.Contains("q6_")) quantFactor = 0.75;
        else if (q.Contains("q8_")) quantFactor = 0.9;
        else if (q.Contains("bf16") || q.Contains("f16")) quantFactor = 1.0;

        return (long)(sizeGB / quantFactor * 1024 * 1024 * 1024 / 2);
    }

    private static string? InferArchitecture(string name)
    {
        if (name == null) return null;
        var n = name.ToLowerInvariant();
        if (n.Contains("llama")) return "llama";
        if (n.Contains("mistral")) return "mistral";
        if (n.Contains("phi")) return "phi";
        if (n.Contains("gemma")) return "gemma";
        if (n.Contains("qwen")) return "qwen";
        if (n.Contains("deepseek")) return "deepseek";
        if (n.Contains("vicuna")) return "vicuna";
        if (n.Contains("zephyr")) return "zephyr";
        return null;
    }

    private static string? InferQuantization(string name)
    {
        if (name == null) return null;
        var n = name.ToLowerInvariant();
        if (n.Contains("q8_0")) return "Q8_0";
        if (n.Contains("q6_")) return "Q6_K";
        if (n.Contains("q5_")) return "Q5_K_M";
        if (n.Contains("q4_")) return "Q4_K_M";
        if (n.Contains("q3_")) return "Q3_K_M";
        if (n.Contains("q2_")) return "Q2_K";
        if (n.Contains("bf16")) return "BF16";
        if (n.Contains("f16")) return "F16";
        if (n.Contains("f32")) return "F32";
        return null;
    }

    private static bool IsEmbeddingModel(string name, string? arch)
    {
        if (name == null) return false;
        var n = name.ToLowerInvariant();
        return n.Contains("embedding") || n.Contains("bge") || n.Contains("nomic") ||
               n.Contains("mxbai") || n.Contains("e5");
    }

    private static bool IsRerankingModel(string name)
    {
        if (name == null) return false;
        var n = name.ToLowerInvariant();
        return n.Contains("rerank") || n.Contains("bge-rerank") || n.Contains("jina");
    }
}