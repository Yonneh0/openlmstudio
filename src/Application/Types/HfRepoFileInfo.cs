namespace OpenLMStudio.Application.Types;

/// <summary>
/// Contains information about a file in a HuggingFace repository.
/// Shared between Application interface and Infrastructure implementation.
/// </summary>
public class HfRepoFileInfo
{
    /// <summary>The relative path of the file within the repository.</summary>
    public required string Path { get; init; }

    /// <summary>The size of the file in bytes, if available from the API.</summary>
    public long Size { get; set; }

    /// <summary>The URL to download the LFS blob, if this is a large-file model (GGUF/safetensors).</summary>
    public string? BlobUrl { get; init; }

    /// <summary>Whether this file is stored in HuggingFace LFS (Large File Storage) and requires special handling.</summary>
    public bool IsLfsFile { get; init; }
}