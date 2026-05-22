namespace OpenLMStudio.Domain.Models.SystemAI;

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
