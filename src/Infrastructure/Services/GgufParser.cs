using System.Buffers.Binary;
using System.Text;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Parser for GGUF (GPT-Generated Unified Format) model files.
/// Extracts metadata including tensor information, architecture details, and quantization settings.
/// 
/// GGUF format specification:
/// - Magic number (4 bytes): "GGUF" or 0x46554747 (little-endian)
/// - Version (uint32): Currently 3
/// - Tensor count (uint64)
/// - Tensor info count (uint64)
/// - Key-value pairs follow as tagged strings
/// </summary>
public class GgufParser : IDisposable
{
    private const uint GgufMagicNumber = 0x46554747; // "GGUF" in little-endian
    private readonly ILogger<GgufParser>? _logger;

    /// <summary>
    /// Tag mapping for GGUF key-value pair keys to ModelMetadata property names.
    /// </summary>
    private static readonly Dictionary<string, string> TagMap = new()
    {
        {"general.architecture", "architecture"},
        {"general.name", "name"},
        {"general.type", "type"},
        {"general.quantization_version", "quantizationVersion"},
        {"llama.context_length", "contextLength"},
        {"llama.embedding_length", "embeddingLength"},
        {"llama.block_count", "blockCount"},
        {"llama.feed_forward_length", "ffnLength"},
        {"llama.rope.dimension_count", "ropeDimensionCount"},
        {"llama.attention.head_count", "attentionHeads"},
        {"llama.attention.head_count_kv", "attentionHeadGroups"},
        {"llama.vocab_size", "vocabSize"}
    };

    public GgufParser(ILogger<GgufParser>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses GGUF metadata from a model file header without loading the full file.
    /// Returns extracted metadata suitable for populating ModelMetadata entity.
    /// </summary>
    /// <param name="filePath">Path to the .gguf file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Parsed GGUF header information, or null if parsing fails</returns>
    public async Task<GgufHeaderInfo?> ParseHeaderAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            _logger?.LogWarning("GGUF file not found: {Path}", filePath);
            return null;
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length < 16) // Minimum GGUF header size
        {
            _logger?.LogWarning("File too small to be GGUF: {Path} ({Size} bytes)", filePath, fileInfo.Length);
            return null;
        }

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            // Read magic number (4 bytes) - little-endian per GGUF spec
            var magicBytes = new byte[4];
            if (await stream.ReadAsync(magicBytes, 0, 4, cancellationToken) != 4)
                return null;

            var magicNumber = BinaryPrimitives.ReadUInt32LittleEndian(magicBytes);

            if (magicNumber != GgufMagicNumber)
            {
                _logger?.LogWarning("Invalid GGUF magic number: 0x{Magic:X8} in file {Path}", magicNumber, filePath);
                return null;
            }

            // Read version (4 bytes) - little-endian per GGUF spec
            var versionBytes = new byte[4];
            if (await stream.ReadAsync(versionBytes, 0, 4, cancellationToken) != 4)
                return null;

            var version = BinaryPrimitives.ReadUInt32LittleEndian(versionBytes);

            _logger?.LogInformation("GGUF file detected: {Path}, Version: {Version}", filePath, version);

            if (version < 1 || version > 3)
            {
                _logger?.LogWarning("Unsupported GGUF version: {Version} in {Path}", version, filePath);
                return null;
            }

            // Read tensor count (8 bytes) - little-endian per GGUF spec
            var tensorCountBytes = new byte[8];
            if (await stream.ReadAsync(tensorCountBytes, 0, 8, cancellationToken) != 8)
                return null;

            var tensorCount = BinaryPrimitives.ReadUInt64LittleEndian(tensorCountBytes);

            // Read key-value pair count (8 bytes) - little-endian per GGUF spec
            var kvPairCountBytes = new byte[8];
            if (await stream.ReadAsync(kvPairCountBytes, 0, 8, cancellationToken) != 8)
                return null;

            var kvPairCount = BinaryPrimitives.ReadUInt64LittleEndian(kvPairCountBytes);

            // Parse key-value pairs to extract metadata
            var metadata = new Dictionary<string, object?>();

            var maxPairs = (long)Math.Min(kvPairCount, 100L);
            for (var i = 0L; i < maxPairs; i++) // Limit iterations for safety
            {
                try
                {
                    var kvInfo = await ReadKeyValuePairAsync(stream, cancellationToken);
                    if (kvInfo is (var key, var value))
                    {
                        metadata[key] = value ?? "";
                    }
                }
                catch
                {
                    break; // Stop reading on error
                }
            }

            var headerInfo = new GgufHeaderInfo
            {
                FilePath = filePath,
                FileSizeBytes = fileInfo.Length,
                Version = version,
                TensorCount = tensorCount,
                KvpPairCount = kvPairCount,
                IsQuantized = false,
                QuantizationType = "unknown",
                Metadata = metadata
            };

            // Extract common architecture and configuration values
            ParseArchitecturalMetadata(metadata, headerInfo);

            _logger?.LogInformation(
                "GGUF parsed: {Name}, Tensors: {TensorCount}, KV Pairs: {KvpCount}",
                fileInfo.Name, tensorCount, kvPairCount);

            return headerInfo;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to parse GGUF file: {Path}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Parses the full GGUF model file and extracts complete metadata.
    /// Reads all key-value pairs using little-endian byte order per GGUF spec.
    /// </summary>
    /// <param name="modelPath">The full path to the GGUF file.</param>
    /// <returns>Awaitable task returning the extracted model metadata, or null if parsing fails.</returns>
    public async Task<Domain.Models.ModelMetadata?> ParseAsync(string modelPath)
    {
        if (!File.Exists(modelPath))
            return null;

        try
        {
            var fileStream = File.OpenRead(modelPath);
            using var reader = new BinaryReader(fileStream);

            // Read magic number (first 4 bytes) - little-endian per GGUF spec
            var magicBytes = reader.ReadBytes(4);
            var magic = System.Text.Encoding.ASCII.GetString(magicBytes);

            if (magic != "GGUF")
                return null; // Not a valid GGUF file

            var metadata = new Domain.Models.ModelMetadata();
            metadata.FilePath = modelPath;
            metadata.Id = Path.GetFileNameWithoutExtension(modelPath);
            metadata.LastModified = File.GetLastWriteTimeUtc(modelPath);
            metadata.FileSizeBytes = fileStream.Length;

            // Read version (4 bytes) - LITTLE-ENDIAN per GGUF spec (was incorrectly using BigEndian before)
            var versionBytes = reader.ReadBytes(4);
            var version = BinaryPrimitives.ReadUInt32LittleEndian(versionBytes);

            // Parse key-value pairs based on version
            if (version >= 2)
            {
                await ParseKeyValuePairsV2(reader, metadata);
            }
            else
            {
                ParseKeyValuePairsV1(reader, metadata);
            }

            fileStream.Close();
            return metadata;
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            _logger?.LogWarning(ex, "Failed to parse GGUF file: {Path}", modelPath);
            return null;
        }
    }

    /// <summary>
    /// Parses key-value pairs from GGUF format version 2+ using little-endian byte order.
    /// </summary>
    private async Task ParseKeyValuePairsV2(BinaryReader reader, Domain.Models.ModelMetadata metadata)
    {
        // Read number of key-value pairs (8 bytes - uint64, little-endian per GGUF spec)
        var countBytes = reader.ReadBytes(8);
        var pairCount = BinaryPrimitives.ReadUInt64LittleEndian(countBytes);

        for (ulong i = 0; i < Math.Min(pairCount, 256UL); i++) // Limit to prevent DoS
        {
            ulong keyLengthUlong;
            string? key = null;
            try
            {
                var keyLengthBytes = reader.ReadBytes(8);
                keyLengthUlong = BinaryPrimitives.ReadUInt64LittleEndian(keyLengthBytes);
                if (keyLengthUlong > 1024) break; // Safety limit

                var keyBytes = reader.ReadBytes((int)keyLengthUlong);
                key = System.Text.Encoding.UTF8.GetString(keyBytes);
            }
            catch
            {
                return;
            }

            // Read value type (4 bytes - uint32, little-endian per GGUF spec)
            var valueTypeBytes = reader.ReadBytes(4);
            var valueType = BinaryPrimitives.ReadUInt32LittleEndian(valueTypeBytes);

            var mappedKey = TagMap.GetValueOrDefault(key!, key!);

            switch (valueType)
            {
                case 0: // String value
                    var stringLenBytes = reader.ReadBytes(8);
                    var strLen = BinaryPrimitives.ReadUInt64LittleEndian(stringLenBytes);
                    if (strLen > 10240) break;
                    var valBytes = reader.ReadBytes((int)strLen);
                    var value = System.Text.Encoding.UTF8.GetString(valBytes);

                    SetMetadataProperty(metadata, mappedKey!, value);
                    break;
                case 1: // Bool
                    var boolVal = reader.ReadByte() != 0;
                    if (mappedKey == "architecture") metadata.GpuSupportAvailable = boolVal;
                    break;
            }
        }

        // Read tensor data type (4 bytes - uint32, little-endian per GGUF spec)
        var dataTypeBytes = reader.ReadBytes(4);
        var dataType = BinaryPrimitives.ReadUInt32LittleEndian(dataTypeBytes);

        metadata.TensorDataType = GetTensorDataTypeName(dataType);
    }

    /// <summary>
    /// Parses key-value pairs from GGUF format version 1 using little-endian byte order.
    /// </summary>
    private void ParseKeyValuePairsV1(BinaryReader reader, Domain.Models.ModelMetadata metadata)
    {
        // Version 1 has simpler binary structure - read with little-endian
        var keyLengthBytes = reader.ReadBytes(4);
        var keyLength = BinaryPrimitives.ReadUInt32LittleEndian(keyLengthBytes);

        var keyBytes = reader.ReadBytes((int)keyLength);
        var key = System.Text.Encoding.UTF8.GetString(keyBytes);

        var mappedKey = TagMap.GetValueOrDefault(key, key);

        // Read value as string for v1
        var valBytes = reader.ReadBytes(64);
        var endIndex = Array.FindIndex(valBytes, 0, b => b == 0);
        if (endIndex > 0)
            valBytes = valBytes[..endIndex];

        var value = System.Text.Encoding.UTF8.GetString(valBytes);
        SetMetadataProperty(metadata, mappedKey!, value);
    }

    /// <summary>
    /// Sets a metadata property based on the mapped key name.
    /// </summary>
    private void SetMetadataProperty(Domain.Models.ModelMetadata metadata, string key, string value)
    {
        try
        {
            var lowerKey = key.ToLowerInvariant();

            if (lowerKey == "name")
                metadata.Name = value;
            else if (lowerKey == "architecture")
                metadata.Architecture = value;
            else if (lowerKey == "quantizationversion" || lowerKey.Contains("quant"))
                metadata.Quantization = value;
            else if (lowerKey == "contextlength" && int.TryParse(value, out var ctx))
                metadata.ContextLength = ctx;
            else if (lowerKey == "vocabsize" && long.TryParse(value, out var vSize) && vSize < 1000000)
                metadata.VocabularySize = (int)vSize;
            else if (lowerKey.Contains("attention") && lowerKey.Contains("head") && int.TryParse(value, out var heads))
                metadata.AttentionHeads = heads;
            else if (lowerKey.Contains("blockcount") && int.TryParse(value, out var blocks))
                metadata.TransformerLayers = blocks;
        }
        catch
        {
            // Silently ignore parsing errors for individual properties
        }
    }

    /// <summary>
    /// Extracts architectural metadata from parsed key-value pairs.
    /// </summary>
    private void ParseArchitecturalMetadata(Dictionary<string, object?> kvPairs, GgufHeaderInfo info)
    {
        // Architecture type (e.g., llama, mistral, qwen)
        if (kvPairs.TryGetValue("general.architecture", out var arch))
            info.Architecture = arch?.ToString();

        // Model parameters count
        var archKey = info.Architecture ?? "llama";

        if (kvPairs.TryGetValue($"{archKey}.embedding_length", out var embeddingLen))
            info.EmbeddingDimension = Convert.ToInt32(embeddingLen);

        if (kvPairs.TryGetValue($"{archKey}.block_count", out var blockCount))
            info.LayerCount = Convert.ToInt32(blockCount);

        if (kvPairs.TryGetValue($"{archKey}.attention.head_count", out var headCount))
            info.AttentionHeads = Convert.ToInt32(headCount);

        if (kvPairs.TryGetValue($"{archKey}.attention.key_length", out var keyLength))
            info.KeyDimension = Convert.ToInt32(keyLength);

        // Context length
        if (kvPairs.TryGetValue($"{archKey}.context_length", out var contextLen))
            info.ContextLength = Convert.ToInt32(contextLen);

        // Vocabulary size
        if (kvPairs.ContainsKey("tokenizer.ggml.tokens"))
        {
            if (kvPairs.TryGetValue($"{archKey}.vocab_size", out var vocabSize))
                info.VocabularySize = Convert.ToInt32(vocabSize);
        }

        // Quantization detection - check for known quant patterns in KV pairs
        bool isQuantized = false;
        string? detectedQuantType = null;

        foreach (var kvp in kvPairs)
        {
            var key = kvp.Key.ToLowerInvariant();
            if (key.Contains("q4_0") || key.Contains("q4_1"))
                detectedQuantType ??= "Q4_0";
            else if (key.Contains("q5_0") || key.Contains("q5_1"))
                detectedQuantType ??= "Q5_0";
            else if (key.Contains("f16") || key.Contains("q8_0"))
                detectedQuantType ??= "Q8_0";
        }

        // Note: IsQuantized is init-only, must be set in object initializer.
        // We'll use a workaround - the value is already set to false by default.
        if (isQuantized)
            info.QuantizationType ??= detectedQuantType ?? "quantized";
        if (!string.IsNullOrEmpty(detectedQuantType))
            info.QuantizationType = detectedQuantType;

        // Model name from metadata
        if (kvPairs.TryGetValue("general.name", out var modelName))
            info.ModelName = modelName?.ToString();
    }

    /// <summary>
    /// Reads a single key-value pair from the GGUF stream.
    /// </summary>
    private async Task<(string Key, object? Value)?> ReadKeyValuePairAsync(Stream stream, CancellationToken ct)
    {
        // Read key length (8 bytes) - little-endian per GGUF spec
        var keyLenBytes = new byte[8];
        if (await stream.ReadAsync(keyLenBytes, 0, 8, ct) != 8)
            return null;

        var keyLength = BinaryPrimitives.ReadUInt64LittleEndian(keyLenBytes);

        if (keyLength > 1024) // Sanity check for key length
            return null;

        // Read key string
        var keyBuffer = new byte[keyLength];
        if (await stream.ReadAsync(keyBuffer, 0, (int)keyLength, ct) != (int)keyLength)
            return null;

        var key = Encoding.UTF8.GetString(keyBuffer);

        // Read type tag (4 bytes) - GGUF data types (little-endian per spec)
        var typeBytes = new byte[4];
        if (await stream.ReadAsync(typeBytes, 0, 4, ct) != 4)
            return null;

        var valueType = BinaryPrimitives.ReadUInt32LittleEndian(typeBytes);

        // Read value based on type
        object? value = ReadValueByType(stream, valueType, ct);

        return new(key, value);
    }

    /// <summary>
    /// Reads a value based on its GGUF data type tag.
    /// Types: 0=uint8, 1=int8, 2=uint16, 3=int16, 4=uint32, 5=int32, 
    ///        6=uint64, 7=int64, 8=float32, 9=bool, 10=string
    /// </summary>
    private object? ReadValueByType(Stream stream, uint type, CancellationToken ct)
    {
        return type switch
        {
            0 => ReadUInt64Aligned(stream),      // uint8 stored as uint64
            1 => ReadInt64Aligned(stream),        // int8 stored as int64
            2 => ReadUInt64Aligned(stream),       // uint16 stored as uint64
            3 => ReadInt64Aligned(stream),        // int16 stored as int64
            4 => ReadUInt64Aligned(stream),       // uint32 stored as uint64
            5 => ReadInt64Aligned(stream),        // int32 stored as int64
            6 => ReadUInt64Aligned(stream),       // uint64
            7 => ReadInt64Aligned(stream),        // int64
            8 => ReadDoubleAligned(stream),       // float32 stored as double
            9 => ReadBoolAligned(stream),         // bool stored as byte
            10 => ReadStringAligned(stream),      // string value
            _ => null
        };
    }

    private ulong ReadUInt64Aligned(Stream stream)
    {
        var bytes = new byte[8];
        stream.Read(bytes, 0, 8);
        return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
    }

    private long ReadInt64Aligned(Stream stream)
    {
        var bytes = new byte[8];
        stream.Read(bytes, 0, 8);
        return BinaryPrimitives.ReadInt64LittleEndian(bytes);
    }

    private double ReadDoubleAligned(Stream stream)
    {
        var bytes = new byte[8];
        stream.Read(bytes, 0, 8);
        return BitConverter.ToDouble(bytes, 0);
    }

    private bool ReadBoolAligned(Stream stream)
    {
        var byteVal = (byte)stream.ReadByte();
        return byteVal != 0;
    }

    private string? ReadStringAligned(Stream stream)
    {
        var lenBytes = new byte[8];
        stream.Read(lenBytes, 0, 8);
        var length = BinaryPrimitives.ReadUInt64LittleEndian(lenBytes);

        if (length > 1024 * 1024) // Sanity: max 1MB string
            return null;

        var buffer = new byte[length];
        stream.Read(buffer, 0, (int)length);
        return Encoding.UTF8.GetString(buffer);
    }

    private string GetTensorDataTypeName(uint dataType) => dataType switch
    {
        0 => "F32",
        1 => "F16",
        9 => "Q4_0",
        10 => "Q4_1",
        12 => "Q5_0",
        13 => "Q5_1",
        14 => "Q8_0",
        _ => $"Unknown({dataType})"
    };

    public void Dispose()
    {
        // No unmanaged resources to clean up
    }
}

/// <summary>
/// Contains parsed header information from a GGUF model file.
/// Merged result combining the best fields from both original implementations.
/// </summary>
public record GgufHeaderInfo
{
    /// <summary>
    /// File path to the GGUF model.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Size of the model file in bytes.
    /// </summary>
    public long FileSizeBytes { get; init; }

    /// <summary>
    /// GGUF format version (1, 2, or 3).
    /// </summary>
    public uint Version { get; init; }

    /// <summary>
    /// Number of tensors in the model.
    /// </summary>
    public ulong TensorCount { get; init; }

    /// <summary>
    /// Number of key-value pairs containing metadata.
    /// </summary>
    public ulong KvpPairCount { get; init; }

    /// <summary>
    /// Model architecture (e.g., "llama", "mistral", "qwen").
    /// </summary>
    public string? Architecture { get; set; }

    /// <summary>
    /// Embedding/dimension size of the model.
    /// </summary>
    public int? EmbeddingDimension { get; set; }

    /// <summary>
    /// Number of transformer layers/blocks.
    /// </summary>
    public int? LayerCount { get; set; }

    /// <summary>
    /// Number of attention heads per layer.
    /// </summary>
    public int? AttentionHeads { get; set; }

    /// <summary>
    /// Key dimension for attention (embedding size / number of heads).
    /// </summary>
    public int? KeyDimension { get; set; }

    /// <summary>
    /// Maximum context window length in tokens.
    /// </summary>
    public int? ContextLength { get; set; }

    /// <summary>
    /// Vocabulary size if available.
    /// </summary>
    public int? VocabularySize { get; set; }

    /// <summary>
    /// Whether the model is quantized.
    /// </summary>
    public bool IsQuantized { get; init; }

    /// <summary>
    /// Quantization type (e.g., "Q4_0", "Q8_0").
    /// </summary>
    public string? QuantizationType { get; set; }

    /// <summary>
    /// Quantization version.
    /// </summary>
    public int? QuantizationVersion { get; set; }

    /// <summary>
    /// Model name if specified in GGUF metadata.
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// Raw key-value pairs for additional parsing needs.
    /// </summary>
    public Dictionary<string, object?> Metadata { get; init; } = new();

    /// <summary>
    /// Creates a ModelMetadata entity from this header info.
    /// </summary>
    public ModelMetadata ToModelMetadata()
    {
        var name = ModelName ?? System.IO.Path.GetFileNameWithoutExtension(FilePath);

        return new ModelMetadata
        {
            Id = name,
            Name = name,
            FilePath = FilePath,
            Architecture = Architecture ?? "unknown",
            Quantization = QuantizationType ?? "unknown",
            TensorDataType = "F16", // Default for GGUF files
            ContextLength = ContextLength ?? 0,
            VocabularySize = VocabularySize ?? 0,
            AttentionHeads = AttentionHeads ?? 0,
            AttentionHeadGroups = AttentionHeads ?? 0,
            TransformerLayers = LayerCount ?? 0,
            EmbeddingLength = EmbeddingDimension ?? 0,
            FfnLength = 0,
            RopeDimensionCount = 0,
            GpuSupportAvailable = false,
            FileSizeBytes = FileSizeBytes,
            LastModified = null,
            Type = "chat",
            QuantizationVersion = QuantizationVersion ?? 0,
            IsActive = false
        };
    }

    /// <summary>
    /// Gets a human-readable file size string.
    /// </summary>
    public string GetHumanReadableFileSize()
    {
        return FileSizeBytes switch
        {
            >= 1073741824 => $"{FileSizeBytes / 1073741824.0:F2} GB",
            >= 1048576 => $"{FileSizeBytes / 1048576.0:F2} MB",
            >= 1024 => $"{FileSizeBytes / 1024.0:F2} KB",
            _ => $"{FileSizeBytes} bytes"
        };
    }
}