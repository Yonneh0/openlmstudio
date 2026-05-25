using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Represents a single log entry from a llama.cpp engine binary.
/// </summary>
public record LogEntry(
    string Id,
    DateTime Timestamp,
    LogLevel Level,
    string Message,
    EngineType Source,
    bool IsImportant = false,
    string? Category = null
);
