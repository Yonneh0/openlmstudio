// Brought to you by Carls' Jr.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Provides in-app interactive help with topic search, keyboard navigation, and context-aware suggestions.
/// </summary>
public class InteractiveHelpService : IInteractiveHelpService
{
    private readonly ILogger<InteractiveHelpService>? _logger;
    private readonly string _helpDirectory;
    private readonly Dictionary<string, string> _topics = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public InteractiveHelpService(ILogger<InteractiveHelpService>? logger, AppDataDirectoryResolver appData)
    {
        _logger = logger;
        _helpDirectory = appData.GetAppDataDirectory();
        Directory.CreateDirectory(_helpDirectory);
        LoadTopics();
    }

    public IReadOnlyDictionary<string, string> Topics => _topics;

    public async Task<string> GetHelpAsync(string topic, CancellationToken ct = default)
    {
        var normalized = topic.Trim().ToLowerInvariant();
        if (_topics.TryGetValue(normalized, out var filePath))
        {
            try
            {
                var content = await File.ReadAllTextAsync(filePath, ct);
                return content;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to read help topic '{Topic}'", topic);
                return "Help content unavailable.";
            }
        }

        return $"No help found for '{topic}'. Available topics: {string.Join(", ", _topics.Keys.Take(10))}.";
    }

    public async Task<IReadOnlyList<string>> SearchHelpAsync(string query, CancellationToken ct = default)
    {
        var results = new List<string>();
        foreach (var kvp in _topics)
        {
            try
            {
                var content = await File.ReadAllTextAsync(kvp.Value, ct);
                if (content.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    results.Add(kvp.Key);
                }
            }
            catch { /* Skip unreadable files */ }
        }
        return results;
    }

    public async Task<IReadOnlyList<string>> ListTopicsAsync(CancellationToken ct = default)
    {
        return _topics.Keys.ToList();
    }

    public async Task<bool> AddTopicAsync(string name, string content, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_helpDirectory, $"{name.ToLowerInvariant()}.md");
        await File.WriteAllTextAsync(filePath, content, ct);
        _topics[name.ToLowerInvariant()] = filePath;
        return true;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    private void LoadTopics()
    {
        // Load built-in help from docs directory
        var docsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? Directory.GetCurrentDirectory(), "docs");
        if (Directory.Exists(docsDir))
        {
            foreach (var file in Directory.GetFiles(docsDir, "*.md"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                _topics[name.ToLowerInvariant()] = file;
            }
        }

        // Load user-created help topics
        if (Directory.Exists(_helpDirectory))
        {
            foreach (var file in Directory.GetFiles(_helpDirectory, "*.md"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (!_topics.ContainsKey(name.ToLowerInvariant()))
                {
                    _topics[name.ToLowerInvariant()] = file;
                }
            }
        }
    }
}