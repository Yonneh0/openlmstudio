using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Domain.Models.LLamaCpp;
using System.Text.Json;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Downloads GGUF models from HuggingFace and discovers local GGUF files.
/// Manages a cache of discovered models and provides download functionality.
/// </summary>
public class GgufModelDownloader : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GgufModelDownloader> _logger;
    private readonly IModelRepository _modelRepository;
    private readonly GgufParser _ggufParser;
    private readonly string _downloadDirectory;
    private readonly ModelRecommendationService _recommendationService;
    private readonly object _lock = new();
    private readonly Dictionary<string, GgufModelInfo> _modelCache = new();
    private bool _disposed;

    public GgufModelDownloader(
        ILogger<GgufModelDownloader> logger,
        IModelRepository modelRepository,
        GgufParser ggufParser,
        ModelRecommendationService recommendationService,
        string? downloadDirectory = null)
    {
        _logger = logger;
        _modelRepository = modelRepository;
        _ggufParser = ggufParser;
        _recommendationService = recommendationService;
        _downloadDirectory = downloadDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenLMStudio", "models");
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    }

    /// <summary>
    /// Discovers all GGUF models in the download directory and subdirectories.
    /// Periodically checks the cancellation token to allow early exit.
    /// </summary>
    public async Task<IReadOnlyList<GgufModelInfo>> DiscoverModelsAsync(CancellationToken ct = default)
    {
        var models = new List<GgufModelInfo>();

        // Handle missing download directory gracefully
        if (!Directory.Exists(_downloadDirectory))
        {
            Directory.CreateDirectory(_downloadDirectory);
            _logger.LogDebug("Created missing download directory: {Directory}", _downloadDirectory);
            return models;
        }

        var ggufFiles = Directory.GetFiles(_downloadDirectory, "*.gguf", SearchOption.AllDirectories);

        foreach (var file in ggufFiles)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var info = await ParseModelInfoAsync(file, ct).ConfigureAwait(false);
                if (info != null)
                    models.Add(info);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse GGUF file: {File}", file);
            }
        }

        return models;
    }

    /// <summary>
    /// Downloads a model from HuggingFace.
    /// Uses a unique filename that includes the repo ID to avoid collisions when multiple repos have the same file name.
    /// </summary>
    public async Task<GgufModelInfo> DownloadFromHuggingFaceAsync(
        string repoId,
        string? fileName = null,
        string? revision = null,
        CancellationToken ct = default)
    {
        var url = revision != null
            ? $"https://huggingface.co/{repoId}/resolve/{revision}/{fileName}"
            : $"https://huggingface.co/{repoId}/resolve/main/{fileName}";

        // Use a unique filename that includes the repo ID to avoid collisions
        var safeRepoId = repoId.Replace("/", "_").Replace("\\", "_");
        var localFileName = fileName ?? Path.GetFileName(url);
        var localPath = Path.Combine(_downloadDirectory, $"{safeRepoId}_{localFileName}");
        await Task.Yield(); // Ensure async continuation

        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

        _logger.LogInformation("Downloading {FileName} from {RepoId}", fileName, repoId);

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var fileStream = File.Create(localPath);
        await stream.CopyToAsync(fileStream, ct).ConfigureAwait(false);

        var info = await ParseModelInfoAsync(localPath, ct).ConfigureAwait(false);
        if (info == null)
            throw new InvalidOperationException($"Failed to parse downloaded model: {localPath}");

        _logger.LogInformation("Downloaded model: {Name} ({Size} bytes)", info.Name, info.FileSizeBytes);
        return info;
    }

    /// <summary>
    /// Gets the recommendation for a model.
    /// </summary>
    public ModelRecommendation GetRecommendation(GgufModelInfo model)
    {
        return _recommendationService.GetRecommendation(model);
    }

    /// <summary>
    /// Gets the recommendation for a model by name.
    /// </summary>
    public ModelRecommendation GetRecommendationByName(string modelName, long fileSizeBytes)
    {
        return _recommendationService.GetRecommendationByName(modelName, fileSizeBytes);
    }

    /// <summary>
    /// Updates the last-used timestamp for a model.
    /// </summary>
    public async Task UpdateLastUsedAsync(string modelId)
    {
        lock (_lock)
        {
            if (_modelCache.TryGetValue(modelId, out var info))
            {
                _modelCache[modelId] = info with { LastUsed = DateTime.UtcNow, UsageCount = (info.UsageCount ?? 0) + 1 };
            }
        }
    }

    private async Task<GgufModelInfo?> ParseModelInfoAsync(string filePath, CancellationToken ct)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        var parts = name.Split('-', StringSplitOptions.RemoveEmptyEntries);

        // Infer quantization from filename patterns (more robust than simple last-part check)
        var quantization = InferQuantizationFromFilename(filePath);

        // Model name is everything except the quantization suffix
        var modelName = InferModelNameFromFilename(filePath, quantization);

        var header = await _ggufParser.ParseHeaderAsync(filePath, ct).ConfigureAwait(false);
        var fileSize = new FileInfo(filePath).Length;

        var chatTemplate = header?.Metadata.GetValueOrDefault("chat_template")?.ToString();
        var architecture = header?.Architecture;
        var contextLength = header?.ContextLength;

        var info = new GgufModelInfo(
            Id: name,
            Name: modelName,
            Architecture: architecture,
            Quantization: quantization,
            ContextLength: contextLength,
            EmbeddingDim: header?.EmbeddingDimension,
            FileSizeBytes: fileSize,
            FilePath: filePath,
            ChatTemplate: chatTemplate,
            Description: header?.ModelName,
            LastUsed: null,
            UsageCount: null);

        _logger.LogDebug("Parsed model info: {Id} (name={Name}, quant={Quant}, arch={Arch})", name, modelName, quantization, architecture);

        lock (_lock)
            _modelCache[name] = info;

        return info;
    }

    /// <summary>
    /// Infers the quantization type from the filename using pattern matching.
    /// More robust than simple last-part splitting.
    /// </summary>
    private static string? InferQuantizationFromFilename(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();

        // Known quantization patterns — order matters: check longer patterns first
        var quantPatterns = new[]
        {
            "q8_0", "q8_1", "q8_0_q4_0", "q8_0_q4_1",
            "q6_k", "q6_k_l",
            "q5_k_m", "q5_k_s", "q5_k_l", "q5_0", "q5_1",
            "q4_k_m", "q4_k_s", "q4_k_l", "q4_0", "q4_1",
            "q3_k_m", "q3_k_s", "q3_k_l",
            "q2_k", "q2_k_l",
            "q2_xxs", "q2_xs", "q2_xl", "q2_xm",
            "q3_xxs", "q3_xs",
            "q2_s", "q2_l",
            "q2_m",
            "bf16",
            "f32", "f16", "f16_f16", "f16_k",
            "i8", "i4", "iq2_xs", "iq2_xxs", "iq2_s", "iq2_m", "iq2_l",
            "iq3_xxs", "iq3_xs", "iq3_s", "iq3_l",
            "iq1_s", "iq4_xs", "iq4_xl", "iq4_nl", "iq4_nxl",
            "iq1_s", "iq1_nxl",
        };

        foreach (var pattern in quantPatterns)
        {
            if (name.Contains(pattern))
                return pattern.ToUpperInvariant();
        }

        return null;
    }

    /// <summary>
    /// Infers the model name by stripping the quantization suffix from the filename.
    /// </summary>
    private static string InferModelNameFromFilename(string filePath, string? quantization)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);

        if (string.IsNullOrEmpty(quantization))
            return name;

        var quantLower = quantization.ToLowerInvariant();

        // Remove quantization suffix from the end of the name
        foreach (var pattern in new[]
        {
            "_q8_0", "_q8_1", "_q6_k", "_q5_k_m", "_q5_k_s", "_q4_k_m", "_q4_k_s",
            "_q4_0", "_q4_1", "_q3_k_m", "_q3_k_s", "_q2_k", "_q2_s",
            "_bf16", "_f32", "_f16", "_f16_f16",
            "_iq2_xs", "_iq2_xxs", "_iq2_s", "_iq2_m", "_iq2_l",
            "_iq3_xxs", "_iq3_xs", "_iq3_s",
            "_iq1_s", "_iq4_xs", "_iq4_nl",
        })
        {
            var suffix = pattern.ToLowerInvariant();
            if (name.EndsWith(suffix))
                return name[..^suffix.Length];
        }

        // Fallback: try splitting on '-' and removing the last segment
        var parts = name.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1)
        {
            var lastPart = parts[^1].ToLowerInvariant();
            if (IsLikelyQuantization(lastPart))
                return string.Join("-", parts.Take(parts.Length - 1));
        }

        return name;
    }

    private static bool IsLikelyQuantization(string part)
    {
        var lower = part.ToLowerInvariant();
        return lower.StartsWith("q") || lower.StartsWith("iq") || lower.StartsWith("f") || lower.StartsWith("b");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient.Dispose();
    }
}
