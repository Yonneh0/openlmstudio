using OpenLMStudio.Application.Types;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Service for downloading and managing voice models (whisper.cpp, Silero VAD, custom wake word).
/// </summary>
public interface IVoiceModelDownloader
{
    /// <summary>
    /// Downloads a voice model if it's not already available.
    /// </summary>
    Task DownloadModelAsync(VoiceModelType modelType);

    /// <summary>
    /// Gets the local path to a downloaded model.
    /// </summary>
    string GetModelPath(VoiceModelType modelType);

    /// <summary>
    /// Checks if a model is available locally.
    /// </summary>
    bool IsModelAvailable(VoiceModelType modelType);

    /// <summary>
    /// Gets information about a model.
    /// </summary>
    Task<VoiceModelInfo?> GetModelInfoAsync(VoiceModelType modelType);

    /// <summary>
    /// Downloads all missing voice models.
    /// </summary>
    Task DownloadAllMissingModelsAsync(CancellationToken ct = default);

    /// <summary>
    /// Verifies the integrity of a downloaded model (SHA256 check).
    /// </summary>
    Task<bool> VerifyModelIntegrityAsync(VoiceModelType modelType);
}