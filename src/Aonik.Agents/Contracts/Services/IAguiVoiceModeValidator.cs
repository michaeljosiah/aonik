using Aonik.Agents.Contracts.Agui;

namespace Aonik.Agents.Contracts.Services;

/// <summary>
/// Pre-flight validator for voice-mode AG-UI runs. All voice-mode checks
/// (TTS service availability, audio-format support, tenant TTS toggle,
/// provider-specific format mapping) MUST run before any SSE bytes are
/// written — once the response is in event-stream mode we can no longer
/// switch to a JSON 400 body.
/// </summary>
/// <remarks>
/// The endpoint hands the request to <see cref="ValidateAsync"/> and
/// either materialises the returned <see cref="AguiVoiceModeContext"/> on
/// the response or serialises the failure as a 400 JSON error.
/// </remarks>
public interface IAguiVoiceModeValidator
{
    /// <summary>
    /// Run all voice-mode preconditions. Returns either a
    /// <see cref="AguiVoiceModeValidationResult.Success"/> with a fully
    /// resolved <see cref="AguiVoiceModeContext"/> or a
    /// <see cref="AguiVoiceModeValidationResult.Failure"/> with an error
    /// code + human message for the caller to render as JSON 400.
    /// </summary>
    /// <param name="input">The inbound AG-UI run input.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AguiVoiceModeValidationResult> ValidateAsync(
        AguiRunInput input,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolved voice-mode parameters used by the streaming endpoint. Only
/// returned when validation succeeds.
/// </summary>
public sealed record AguiVoiceModeContext(
    string ProviderFormat,
    string AbstractFormat,
    string AudioMime);

/// <summary>
/// Result of <see cref="IAguiVoiceModeValidator.ValidateAsync"/>. Discriminated
/// by <see cref="IsSuccess"/>: success carries a <see cref="Context"/>;
/// failure carries an error <see cref="Code"/> + <see cref="Message"/>.
/// </summary>
public sealed record AguiVoiceModeValidationResult
{
    public bool IsSuccess { get; }
    public AguiVoiceModeContext? Context { get; }
    public string? Code { get; }
    public string? Message { get; }

    private AguiVoiceModeValidationResult(
        bool isSuccess,
        AguiVoiceModeContext? context,
        string? code,
        string? message)
    {
        IsSuccess = isSuccess;
        Context = context;
        Code = code;
        Message = message;
    }

    /// <summary>Voice mode is not requested. Caller proceeds without TTS.</summary>
    public static AguiVoiceModeValidationResult NotRequested { get; } =
        new(true, null, null, null);

    /// <summary>Voice mode is requested and validation passed.</summary>
    public static AguiVoiceModeValidationResult Success(AguiVoiceModeContext context) =>
        new(true, context, null, null);

    /// <summary>Voice mode is requested but a precondition failed.</summary>
    public static AguiVoiceModeValidationResult Failure(string code, string message) =>
        new(false, null, code, message);
}
