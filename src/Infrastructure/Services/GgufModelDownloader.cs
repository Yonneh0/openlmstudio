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
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
    }

    /// <summary>
    /// Discovers all GGUF models in the download directory and subdirectories.
    /// Periodically checks the cancellation token to allow early exit.
    /// </summary>
    public async Task<IReadOnlyList<GgufModelInfo>> DiscoverModelsAsync(CancellationToken ct = default)
    {
        var models = new List<GgufModelInfo>();
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
        var safeRepoId = repoId.Replace("/", "_");
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
        var modelName = parts.Length > 1 ? string.Join("-", parts.Take(parts.Length - 1)) : name;
        var quantization = parts.Length > 0 ? parts[^1] : null;

        var header = await _ggufParser.ParseHeaderAsync(filePath).ConfigureAwait(false);
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

        lock (_lock)
            _modelCache[name] = info;

        return info;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient.Dispose();
    }
}