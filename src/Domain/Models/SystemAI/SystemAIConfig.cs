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
}