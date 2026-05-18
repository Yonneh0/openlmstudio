namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Represents metadata extracted from a GGUF model file (text generation models).
/// Contains information about the model architecture, quantization, and capabilities.
/// </summary>
public class ModelMetadata : IDisposable
{
    /// <summary>
    /// Unique identifier for the model (typically filename without extension).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name of the model.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Full file path to the GGUF model file.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Model architecture type (e.g., "llama", "mistral", "gpt2").
    /// </summary>
    public string Architecture { get; set; } = string.Empty;

    /// <summary>
    /// Quantization format used (e.g., "Q4_0", "Q4_1", "F16", "Q8_0").
    /// </summary>
    public string Quantization { get; set; } = string.Empty;

    /// <summary>
    /// Tensor data type of the model weights.
    /// </summary>
    public string TensorDataType { get; set; } = string.Empty;

    /// <summary>
    /// Maximum context length supported by the model in tokens.
    /// </summary>
    public int ContextLength { get; set; }

    /// <summary>
    /// Size of the vocabulary used by the model.
    /// </summary>
    public int VocabularySize { get; set; }

    /// <summary>
    /// Number of attention heads in each transformer layer.
    /// </summary>
    public int AttentionHeads { get; set; }

    /// <summary>
    /// Number of key-value heads for grouped query attention.
    /// </summary>
    public int AttentionHeadGroups { get; set; }

    /// <summary>
    /// Number of transformer blocks in the model.
    /// </summary>
    public int TransformerLayers { get; set; }

    /// <summary>
    /// Embedding dimension size for the model.
    /// </summary>
    public int EmbeddingLength { get; set; }

    /// <summary>
    /// Feed-forward layer dimension size.
    /// </summary>
    public int FfnLength { get; set; }

    /// <summary>
    /// RoPE embedding dimension count for positional encoding.
    /// </summary>
    public int RopeDimensionCount { get; set; }

    /// <summary>
    /// Whether the model supports GPU acceleration.
    /// </summary>
    public bool GpuSupportAvailable { get; set; }

    /// <summary>
    /// File size in bytes on disk.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Last modified timestamp (UTC).
    /// </summary>
    public DateTime? LastModified { get; set; }

    /// <summary>
    /// Model type classification (e.g., "chat", "completion", "embedder").
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Version or revision identifier from the GGUF header.
    /// </summary>
    public int QuantizationVersion { get; set; }

    /// <summary>
    /// Model format/quantization type (e.g., "Q4_0", "Q8_0", "F16") - alias for Quantization.
    /// </summary>
    [Obsolete("Use Quantization instead. Kept for OpenAI API compatibility.")]
    public string Format => Quantization;

    /// <summary>
    /// Whether this model is currently loaded and available for inference.
    /// </summary>
    public bool IsActive { get; set; }

    public void Dispose() { /* No unmanaged resources */ }
}

/// <summary>
/// Represents metadata extracted from a safetensors model file (image generation, diffusion, VAE, LoRA, embeddings).
/// Contains tensor shapes, dtype information, and format variant details.
/// </summary>
public class MultiModalModelMetadata : IDisposable
{
    /// <summary>
    /// Unique identifier for the model (typically filename without extension).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name of the model.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// File path to the primary safetensors file or directory containing sharded files.
    /// For single-file models, points to the .safetensors file.
    /// For sharded models, points to the index file (e.g., model.safetensors.index.json).
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// The type of multi-modal model (image generation, diffusion, VAE, LoRA, embedding).
    /// </summary>
    public ModelType ModelType { get; set; } = ModelType.ImageGeneration;

    /// <summary>
    /// The file format variant (Gguf for GGUF files, Safetensors for safetensors, Onnx for ONNX models).
    /// </summary>
    public ModelFormat Format { get; set; } = ModelFormat.Safetensors;

    /// <summary>
    /// For LoRA adapters: the variant format (LoRa, LoHa, LoKr).
    /// Null for non-adapter models.
    /// </summary>
    public LoraFormatVariant? LoraFormatVariant { get; set; }

    /// <summary>
    /// For LoRA adapters: the rank of the adapter (e.g., 4, 8, 16, 32).
    /// Null for non-adapter models.
    /// </summary>
    public int? Rank { get; set; }

    /// <summary>
    /// For LoRA adapters: list of target modules this adapter applies to (e.g., ["q_proj", "v_proj"]).
    /// Null for non-adapter models.
    /// </summary>
    public IReadOnlyList<string>? LoraTargetModules { get; set; }

    /// <summary>
    /// For LoRA adapters: the scaling factor applied to weights (alpha / rank).
    /// Null for non-adapter models.
    /// </summary>
    public double? ScalingFactor { get; set; }

    /// <summary>
    /// For image generation/diffusion: the base model this adapter is designed for (e.g., "sdxl", "sd15").
    /// Null for non-adapter models.
    /// </summary>
    public string? CompatibleBaseModel { get; set; }

    /// <summary>
    /// For LoRA adapters: whether the weights should be merged into the base model or applied dynamically.
    /// Null for non-adapter models.
    /// </summary>
    public bool? MergeOnLoad { get; set; }

    /// <summary>
    /// For image generation/diffusion: the pipeline type (Stable Diffusion, Flux, etc.).
    /// Null for non-image-generation models.
    /// </summary>
    public string? PipelineType { get; set; }

    /// <summary>
    /// Default resolution in pixels for this model's output images.
    /// Null if not applicable or unknown.
    /// </summary>
    public int? DefaultResolution { get; set; }

    /// <summary>
    /// Number of diffusion steps used during training (typical default: 1000).
    /// Null if not applicable or unknown.
    /// </summary>
    public int? TrainingSteps { get; set; }

    /// <summary>
    /// List of tensor names and their shapes from the safetensors header.
    /// Key = tensor name, Value = shape (e.g., [768, 4096] for a linear layer).
    /// </summary>
    public Dictionary<string, long[]> TensorShapes { get; set; } = new();

    /// <summary>
    /// List of tensor names and their data types from the safetensors header.
    /// Key = tensor name, Value = dtype string (e.g., "f32", "f16", "i32").
    /// </summary>
    public Dictionary<string, string> TensorDtypes { get; set; } = new();

    /// <summary>
    /// For sharded models: list of file paths to all weight files.
    /// Null for single-file models.
    /// </summary>
    public IReadOnlyList<string>? ShardedFiles { get; set; }

    /// <summary>
    /// Total number of parameters in the model (estimated from tensor shapes).
    /// Null if calculation is not possible.
    /// </summary>
    public long? ParameterCount { get; set; }

    /// <summary>
    /// File size in bytes on disk (for single-file models) or sum of all shard sizes for sharded models.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// SHA256 hash of the file content, if available. Used for integrity verification after download.
    /// Populated by the download manager post-download validation.
    /// </summary>
    public string? Sha256Hash { get; set; }

    /// <summary>
    /// MD5 hash (legacy fallback). Less secure than SHA256 but commonly available in HuggingFace manifests.
    /// Populated by the download manager post-download validation.
    /// </summary>
    public string? Md5Hash { get; set; }

    /// <summary>
    /// Last modified timestamp (UTC).
    /// </summary>
    public DateTime? LastModified { get; set; }

    /// <summary>
    /// Model version or revision identifier from the repository.
    /// Null if not available in the source metadata.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Whether this model is currently loaded and available for inference.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// For LoRA adapters: the current weight multiplier (user can adjust at runtime).
    /// Default is 1.0 - multiply by negative to invert, by >1.0 for stronger effect, by <1.0 for weaker.
    /// Null for non-adapter models.
    /// </summary>
    public double CurrentWeight { get; set; } = 1.0;

    public void Dispose() { /* No unmanaged resources */ }
}
