using System.Threading;
using System.Threading.Tasks;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Service for previewing file contents in the agent sandbox.
/// Reads the first N lines of text files and provides syntax-highlighted previews for code files.
/// </summary>
public interface IFilePreviewService
{
    /// <summary>
    /// Reads the first N lines of a file and returns a preview.
    /// Returns null if the file is too large or binary.
    /// </summary>
    Task<string?> GetPreviewAsync(string filePath, int maxLines = 100, CancellationToken ct = default);
}