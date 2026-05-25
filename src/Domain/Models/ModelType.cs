namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Defines the type of an AI model, supporting text generation, image generation, diffusion, VAE, LoRA adapters, and embeddings.
/// </summary>
public enum ModelType
{
    /// <summary>
    /// Text generation model (e.g., LLaMA, Mistral) - typically GGUF format.
    /// </summary>
    TextGeneration = 0,

    /// <summary>
    /// Image generation model using diffusion (e.g., Stable Diffusion, Flux) - typically safetensors format.
    /// </summary>
    ImageGeneration = 1,

    /// <summary>
    /// Pure diffusion model for latent space operations - typically safetensors format.
    /// </summary>
    Diffusion = 2,

    /// <summary>
    /// VAE (Variational Autoencoder) model for encoding/decoding latent representations - typically safetensors format.
    /// </summary>
    Vae = 3,

    /// <summary>
    /// LoRA (Low-Rank Adaptation) adapter for fine-tuning models on-the-fly - typically safetensors format.
    /// </summary>
    Lora = 4,

    /// <summary>
    /// Text/image embedding model for vector representations - typically safetensors format.
    /// </summary>
    Embedding = 5
}

/// <summary>
/// Defines the file format of a model file.
/// </summary>
public enum ModelFormat
{
    /// <summary>
    /// GGUF (GPT-Generated Unified Format) - used for text generation models.
    /// </summary>
    Gguf,

    /// <summary>
    /// Safetensors - used for image generation, diffusion, VAE, LoRA, and embedding models.
    /// Supports both single-file and multi-file sharded formats.
    /// </summary>
    Safetensors,

    /// <summary>
    /// ONNX (Open Neural Network Exchange) - alternative inference format.
    /// </summary>
    Onnx
}

/// <summary>
/// Defines the LoRA adapter format variant for safetensors-based adapters.
/// </summary>
public enum LoraFormatVariant
{
    /// <summary>
    /// Standard LoRA format (rank decomposition).
    /// </summary>
    LoRa,

    /// <summary>
    /// LoHa - High-order low-rank adaptation (uses Hadamard transform).
    /// </summary>
    LoHa,

    /// <summary>
    /// LoKr - Kronecker product adaptation (allows rank sharing across dimensions).
    /// </summary>
    LoKr
}