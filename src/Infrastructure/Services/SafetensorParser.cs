using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Parser for safetensors model files, extracting header metadata including tensor shapes and dtypes.
/// Safetensors format specification:
/// - Header size (8 bytes, little-endian uint64): size of the JSON header in bytes
/// - JSON header: contains "__metadata__" (optional) and per-tensor info
///   Each tensor entry: { "dtype": str, "shape": [int], "data_offsets": [start, end] }
/// - Raw tensor data follows the header
/// 
/// Supported dtype strings: u8, i8, u16, i16, u32, i32, u64, i64, f16, f32, f64, b16, bf16, bool, etc.
/// </summary>
public class SafetensorParser : IDisposable
{
    private const ulong MaxHeaderSizeUlong = 1024UL * 1024UL * 10UL; // 10MB max header (safety limit)
    private readonly ILogger<SafetensorParser>? _logger;

    /// <summary>
    /// Mapping of safetensors dtype strings to .NET type names for parameter estimation.
    /// </summary>
    private static readonly Dictionary<string, int> DtypeByteSizeMap = new()
    {
        { "u8", 1 },
        { "i8", 1 },
        { "u16", 2 },
        { "i16", 2 },
        { "u32", 4 },
        { "i32", 4 },
        { "u64", 8 },
        { "i64", 8 },
        { "f16", 2 },
        { "f32", 4 },
        { "f64", 8 },
        { "b16", 2 }, // bfloat16 = 2 bytes
        { "bf16", 2 }, // alias for bfloat16
        { "bool", 1 }
    };

    public SafetensorParser(ILogger<SafetensorParser>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses the header of a safetensors file and extracts metadata without loading tensors.
    /// </summary>
    public async Task<SafetensorsHeaderInfo?> ParseHeaderAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            _logger?.LogWarning("Safetensors file not found: {FilePath}", filePath);
            return null;
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length < 8) // Minimum header size is 8 bytes for the length field
        {
            _logger?.LogWarning("File too small to be safetensors: {FilePath} ({Size} bytes)", filePath, fileInfo.Length);
            return null;
        }

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            // Read header size (8 bytes, little-endian uint64)
            var headerSizeBytes = new byte[8];
            if (await stream.ReadAsync(headerSizeBytes, 0, 8, cancellationToken) != 8)
                return null;

            var headerSize = BinaryPrimitives.ReadUInt64LittleEndian(headerSizeBytes);

            // Safety limit for header size
            if (headerSize > MaxHeaderSizeUlong || headerSize > (ulong)(fileInfo.Length - 8))
            {
                _logger?.LogWarning("Invalid safetensors header size: {Size} in {FilePath}", headerSize, filePath);
                return null;
            }

            // Read the JSON header
            var headerBuffer = new byte[headerSize];
            if (await stream.ReadAsync(headerBuffer, 0, (int)headerSize, cancellationToken) != (int)headerSize)
                return null;

            var headerJson = Encoding.UTF8.GetString(headerBuffer);

            // Parse the JSON header
            using var jsonDoc = JsonDocument.Parse(headerJson);
            var root = jsonDoc.RootElement;

            // Extract tensor metadata from header
            var tensorsMetadata = new Dictionary<string, TensorMetadata>();
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name == "__metadata__")
                    continue; // Skip the global metadata section - handle separately

                try
                {
                    var tensorName = property.Name;
                    var dtypeString = property.Value.GetProperty("dtype").GetString();
                    var shapeArray = new long[property.Value.GetProperty("shape").GetArrayLength()];

                    for (var i = 0; i < shapeArray.Length; i++)
                        shapeArray[i] = property.Value.GetProperty("shape")[i].GetInt64();

                    var startOffset = property.Value.GetProperty("data_offsets")[0].GetInt64();
                    var endOffset = property.Value.GetProperty("data_offsets")[1].GetInt64();

                    var dtypeOrDefault = dtypeString ?? "unknown";
                    tensorsMetadata[tensorName] = new TensorMetadata
                    {
                        Name = tensorName,
                        Dtype = dtypeOrDefault,
                        Shape = shapeArray,
                        StartOffset = startOffset,
                        EndOffset = endOffset,
                        NumElements = shapeArray.Aggregate(1L, (a, b) => a * b),
                        ByteSize = SafetensorParser.GetByteSizeForDtype(dtypeOrDefault)
                    };
                }
                catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
                {
                    _logger?.LogWarning(ex, "Failed to parse tensor metadata for: {TensorName} in {FilePath}",
                        property.Name, filePath);
                }
            }

            // Extract global __metadata__ if present
            var globalMetadata = new Dictionary<string, string>();
            if (root.TryGetProperty("__metadata__", out var metadataElement))
            {
                foreach (var prop in metadataElement.EnumerateObject())
                {
                    try
                    {
                        globalMetadata[prop.Name] = prop.Value.GetString() ?? "";
                    }
                    catch
                    {
                        // Skip non-string values in __metadata__
                    }
                }
            }

            var headerInfo = new SafetensorsHeaderInfo
            {
                FilePath = filePath,
                FileSizeBytes = fileInfo.Length,
                HeaderSize = (long)headerSize,
                IsSharded = root.TryGetProperty("__metadata__", out _), // Sharded if has metadata section
                GlobalMetadata = globalMetadata,
                TensorsMetadata = tensorsMetadata,
                TotalTensorCount = tensorsMetadata.Count,
                EstimatedTotalParameters = 0 // Will be calculated by the caller from tensor shapes and dtypes
            };

            // Calculate total parameters (sum of all tensor elements / 2 for weight matrices)
            var totalElements = 0L;
            foreach (var tensor in tensorsMetadata.Values)
            {
                totalElements += tensor.NumElements;
            }
            // Rough estimate: weight tensors contribute roughly NumElements/1 to parameter count each
            headerInfo.EstimatedTotalParameters = totalElements > 0 ? totalElements / 2 : 0;

            _logger?.LogInformation(
                "Safetensors parsed: {FilePath}, Tensors: {TensorCount}, Total elements: {TotalElements}",
                filePath, tensorsMetadata.Count, totalElements);

            return headerInfo;
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or JsonException)
        {
            _logger?.LogError(ex, "Failed to parse safetensors file: {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Parses a sharded model index JSON file (e.g., model.safetensors.index.json).
    /// Returns information about all shards that need to be loaded together.
    /// </summary>
    public async Task<ShardedModelIndex?> ParseIndexAsync(string indexPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(indexPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(indexPath, cancellationToken);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // The index should have "weight_map" containing tensor_name -> filename mappings
            if (!root.TryGetProperty("weight_map", out var weightMapElement))
            {
                _logger?.LogWarning("Invalid sharded model index - missing 'weight_map': {FilePath}", indexPath);
                return null;
            }

            // Group tensors by shard file
            var shardFiles = new HashSet<string>();
            foreach (var prop in weightMapElement.EnumerateObject())
            {
                try
                {
                    var fileName = prop.Value.GetString();
                    if (!string.IsNullOrEmpty(fileName))
                        shardFiles.Add(fileName);
                }
                catch { /* Skip invalid entries */ }
            }

            // Resolve full paths for each shard file relative to index location
            var baseDirectory = Path.GetDirectoryName(indexPath) ?? "";
            var resolvedShardFiles = new List<string>();
            foreach (var fileName in shardFiles)
            {
                var fullPath = Path.Combine(baseDirectory, fileName);
                if (File.Exists(fullPath))
                    resolvedShardFiles.Add(fullPath);
                else
                    _logger?.LogWarning("Shard file not found: {FilePath}", fullPath);
            }

            return new ShardedModelIndex
            {
                IndexPath = indexPath,
                BaseDirectory = baseDirectory,
                ShardFileNames = shardFiles.ToList(),
                ResolvedShardFiles = resolvedShardFiles,
                IsComplete = shardFiles.SetEquals(resolvedShardFiles) // Check if all referenced shards exist
            };
        }
        catch (Exception ex) when (ex is System.IO.IOException or JsonException)
        {
            _logger?.LogError(ex, "Failed to parse sharded model index: {FilePath}", indexPath);
            return null;
        }
    }

    /// <summary>
    /// Computes SHA256 hash of a file for download integrity verification.
    /// </summary>
    public static async Task<string?> ComputeSha256HashAsync(string filePath, CancellationToken cancellationToken = default, ILogger? logger = null)
    {
        try
        {
            using var sha256 = SHA256.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);

            // Convert to hex string (lowercase)
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            logger?.LogWarning(ex, "Failed to compute SHA256 hash for: {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Computes MD5 hash of a file (legacy fallback for HuggingFace manifest compatibility).
    /// </summary>
    public static async Task<string?> ComputeMd5HashAsync(string filePath, CancellationToken cancellationToken = default, ILogger? logger = null)
    {
        try
        {
            using var md5 = MD5.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var hashBytes = await md5.ComputeHashAsync(stream, cancellationToken);

            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            logger?.LogWarning(ex, "Failed to compute MD5 hash for: {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Validates that a downloaded safetensors file has valid header integrity.
    /// </summary>
    public async Task<bool> ValidateHeaderAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var result = await ParseHeaderAsync(filePath, cancellationToken);
        return result != null && result.TotalTensorCount > 0;
    }

    /// <summary>
    /// Gets the tensor metadata from a safetensors file (convenience wrapper around ParseHeaderAsync).
    /// Returns null if the file cannot be parsed.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, TensorMetadata>?> GetTensorMetadataAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var header = await ParseHeaderAsync(filePath, cancellationToken);
        return header?.TensorsMetadata;
    }

    /// <summary>
    /// Gets the number of bytes per element for a given dtype string.
    /// </summary>
    public static int GetByteSizeForDtype(string dtype)
    {
        if (string.IsNullOrEmpty(dtype))
            return 0;

        DtypeByteSizeMap.TryGetValue(dtype.ToLowerInvariant(), out var size);
        return size;
    }

    public void Dispose()
    {
        // No unmanaged resources to clean up
    }
}

/// <summary>
/// Contains parsed header information from a safetensors model file.
/// </summary>
public class SafetensorsHeaderInfo : IDisposable
{
    /// <summary>
    /// File path to the safetensors model.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Size of the model file in bytes.
    /// </summary>
    public long FileSizeBytes { get; init; }

    /// <summary>
    /// Size of the JSON header in bytes (excluding the 8-byte length field).
    /// </summary>
    public long HeaderSize { get; init; }

    /// <summary>
    /// Number of tensors defined in this model.
    /// </summary>
    public int TotalTensorCount { get; init; }

    /// <summary>
    /// Whether this is a sharded model (has weight_map reference).
    /// </summary>
    public bool IsSharded { get; init; }

    /// <summary>
    /// Estimated total parameter count from tensor shapes.
    /// </summary>
    public long EstimatedTotalParameters { get; set; }

    /// <summary>
    /// Metadata about each tensor (name, dtype, shape, offsets).
    /// Key = tensor name.
    /// </summary>
    public Dictionary<string, TensorMetadata> TensorsMetadata { get; set; } = new();

    /// <summary>
    /// Global model metadata (__metadata__ section in the header).
    /// Typically contains "model_type", "dtype", etc.
    /// </summary>
    public Dictionary<string, string> GlobalMetadata { get; set; } = new();

    public void Dispose() { /* No unmanaged resources */ }
}

/// <summary>
/// Contains metadata about a single tensor within a safetensors model file.
/// </summary>
public class TensorMetadata : IDisposable
{
    /// <summary>
    /// Name of the tensor (e.g., "model.layers.0.self_attn.q_proj.weight").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Data type of the tensor (e.g., "f32", "f16", "i8").
    /// </summary>
    public required string Dtype { get; set; }

    /// <summary>
    /// Shape dimensions of the tensor.
    /// </summary>
    public required long[] Shape { get; set; }

    /// <summary>
    /// Byte offset in file where this tensor's data begins (relative to header end).
    /// </summary>
    public long StartOffset { get; set; }

    /// <summary>
    /// Byte offset where this tensor's data ends.
    /// </summary>
    public long EndOffset { get; set; }

    /// <summary>
    /// Total number of elements in this tensor (product of all dimensions).
    /// </summary>
    public long NumElements { get; set; }

    /// <summary>
    /// Number of bytes required to store this tensor (NumElements * dtype byte size).
    /// </summary>
    public int ByteSize { get; set; }

    public void Dispose() { /* No unmanaged resources */ }
}

/// <summary>
/// Contains information about a sharded model from its index file.
/// </summary>
public class ShardedModelIndex : IDisposable
{
    /// <summary>
    /// File path to the sharded model index JSON.
    /// </summary>
    public required string IndexPath { get; init; }

    /// <summary>
    /// Base directory containing all shard files.
    /// </summary>
    public required string BaseDirectory { get; init; }

    /// <summary>
    /// List of shard file names referenced by the weight_map (may not exist on disk).
    /// </summary>
    public IReadOnlyList<string> ShardFileNames { get; set; } = Array.Empty<string>();

    /// <summary>
    /// List of resolved full paths to existing shard files.
    /// </summary>
    public IReadOnlyList<string> ResolvedShardFiles { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Whether all referenced shard files exist on disk.
    /// </summary>
    public bool IsComplete { get; init; }

    public void Dispose() { /* No unmanaged resources */ }
}