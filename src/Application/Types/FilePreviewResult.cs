namespace OpenLMStudio.Application.Types;

/// <summary>
/// Result of previewing a text file's contents.
/// </summary>
public record FilePreviewResult(
    string Content,
    int TotalLines,
    string? Language,
    string FilePath);