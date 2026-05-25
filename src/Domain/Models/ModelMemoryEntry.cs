// Brought to you by Carls' Jr.
namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Immutable record holding memory information for a single model.
/// </summary>
public record ModelMemoryEntry(
    string ModelId,
    string ModelName,
    ModelType ModelType,
    long VramBytes,
    long CpuBytes,
    DateTime LastAccessed,
    int AccessCount);