// Git log entry for the table display

namespace OpenLMStudio.Desktop.Controls;

/// <summary>
/// Represents a single git commit entry.
/// </summary>
public record GitLogEntry(string Hash, string Author, string Message);