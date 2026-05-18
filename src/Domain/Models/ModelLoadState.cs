namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Represents the lifecycle state of a loaded model instance in memory.
/// </summary>
public enum ModelLoadState
{
    /// <summary>
    /// Model file exists on disk but is not currently loaded into GPU/CPU memory.
    /// </summary>
    Unloaded,

    /// <summary>
    /// Model is being loaded from disk into memory.
    /// </summary>
    Loading,

    /// <summary>
    /// Model has been successfully loaded and is ready to generate completions.
    /// </summary>
    Loaded,

    /// <summary>
    /// Model is in the process of unloading from memory.
    /// </summary>
    Unloading
}

/// <summary>
/// Represents a model instance that has been loaded into GPU/CPU memory for inference.
/// </summary>
public class LoadedModelInstance : IDisposable
{
    private bool _disposed;

    public string ModelId { get; set; } = string.Empty;
    public ModelMetadata Metadata { get; set; } = null!;
    public ModelLoadState State { get; set; } = ModelLoadState.Unloaded;

    /// <summary>
    /// GPU memory usage in MB for this model instance.
    /// </summary>
    public int GpuMemoryUsageMB { get; set; }

    /// <summary>
    /// Number of tokens processed (prompt + completion).
    /// </summary>
    public long TotalTokensProcessed { get; set; }

    /// <summary>
    /// When the model was loaded into memory.
    /// </summary>
    public DateTime LoadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// GPU context handle for llama.cpp native binding.
    /// </summary>
    public IntPtr GpuContextHandle { get; set; } = IntPtr.Zero;

    /// <summary>
    /// Inference context handle for llama.cpp native binding.
    /// </summary>
    public IntPtr InferenceContextHandle { get; set; } = IntPtr.Zero;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Release GPU memory and inference context handles
        GpuContextHandle = IntPtr.Zero;
        InferenceContextHandle = IntPtr.Zero;
    }
}