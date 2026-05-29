namespace OpenLMStudio.Application.Types;

/// <summary>
/// Types of voice models that can be downloaded and used.
/// </summary>
public enum VoiceModelType
{
    WhisperTiny,
    WhisperBase,
    WhisperSmall,
    WhisperMedium,
    WhisperLarge,
    WhisperQuantized,
    SileroVad,
    CustomWakeWord
}

/// <summary>
/// Information about a voice model (download URL, size, platform, etc.).
/// </summary>
public record VoiceModelInfo(
    VoiceModelType ModelType,
    string Name,
    string Version,
    long SizeBytes,
    string DownloadUrl,
    string Sha256,
    string Platform,
    string LocalPath);

/// <summary>
/// A voice command with matched phrase and parameters.
/// </summary>
public record VoiceCommand(
    string RawText,
    string MatchedPhrase,
    string Action,
    Dictionary<string, string> Parameters,
    DateTime Timestamp,
    bool IsExactMatch,
    bool IsSystemAIGenerated);

/// <summary>
/// A generated voice script with steps.
/// </summary>
public record VoiceScript(
    string RawInput,
    string GeneratedScript,
    List<ScriptStep> Steps,
    double Confidence);

/// <summary>
/// A single step in a voice script.
/// </summary>
public record ScriptStep(
    string Tool,
    Dictionary<string, string> Parameters,
    string Description);

/// <summary>
/// Event args for voice command received events.
/// </summary>
public class VoiceCommandReceivedEventArgs : EventArgs
{
    public VoiceCommand Command { get; }
    public bool IsProcessing { get; set; }

    public VoiceCommandReceivedEventArgs(VoiceCommand command)
    {
        Command = command;
        IsProcessing = false;
    }
}