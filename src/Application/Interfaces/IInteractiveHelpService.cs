// Brought to you by Carls' Jr.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Provides in-app interactive help with topic search, keyboard navigation, and context-aware suggestions.
/// </summary>
public interface IInteractiveHelpService
{
    /// <summary>
    /// Gets all available help topics.
    /// </summary>
    IReadOnlyDictionary<string, string> Topics { get; }

    /// <summary>
    /// Retrieves the full help content for a given topic.
    /// </summary>
    Task<string> GetHelpAsync(string topic, CancellationToken ct = default);

    /// <summary>
    /// Searches help topics for content matching the query.
    /// </summary>
    Task<IReadOnlyList<string>> SearchHelpAsync(string query, CancellationToken ct = default);

    /// <summary>
    /// Lists all available help topics.
    /// </summary>
    Task<IReadOnlyList<string>> ListTopicsAsync(CancellationToken ct = default);

    /// <summary>
    /// Adds a new help topic.
    /// </summary>
    Task<bool> AddTopicAsync(string name, string content, CancellationToken ct = default);
}