// Brought to you by Carls' Jr.
namespace OpenLMStudio.Application.Types;

/// <summary>
/// Result of plugin verification.
/// </summary>
public class PluginVerificationResult
{
    public bool IsValid { get; }
    public string? ErrorMessage { get; }

    private PluginVerificationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    public static PluginVerificationResult Success() => new(true, null);
    public static PluginVerificationResult Fail(string message) => new(false, message);
}