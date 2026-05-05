using Aonik.Agents.Contracts.Agui;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions.Ai;

namespace Aonik.Agents.Services;

/// <summary>
/// Implements pre-flight voice-mode validation for the AG-UI streaming
/// endpoint. Owns the previously inline checks that decided "is voice
/// mode safe to commit to before we open the SSE stream?". The endpoint
/// is left to deal only with HTTP concerns and the protocol translation.
/// </summary>
internal sealed class AguiVoiceModeValidator : IAguiVoiceModeValidator
{
    private readonly IStreamingTextToSpeechService? _streamingTts;
    private readonly ITenantTextToSpeechSettingsService? _ttsSettings;

    public AguiVoiceModeValidator(
        IStreamingTextToSpeechService? streamingTts = null,
        ITenantTextToSpeechSettingsService? ttsSettings = null)
    {
        _streamingTts = streamingTts;
        _ttsSettings = ttsSettings;
    }

    public async Task<AguiVoiceModeValidationResult> ValidateAsync(
        AguiRunInput input,
        CancellationToken cancellationToken = default)
    {
        if (!input.VoiceMode)
            return AguiVoiceModeValidationResult.NotRequested;

        // Voice mode requires both the synth provider and the tenant
        // settings boundary. If either is unwired the deployment doesn't
        // support voice — surface a clear 400 instead of a half-stream.
        if (_streamingTts is null || _ttsSettings is null)
        {
            return AguiVoiceModeValidationResult.Failure(
                "voice_mode_unavailable",
                "Voice mode is not supported in this deployment.");
        }

        var requestedAbstractFormat = input.AudioFormat ?? AudioFormatNegotiation.DefaultAbstractFormat;
        if (!AudioFormatNegotiation.IsKnownAbstractFormat(requestedAbstractFormat))
        {
            return AguiVoiceModeValidationResult.Failure(
                "invalid_audio_format",
                $"Unsupported audioFormat '{input.AudioFormat}'. Use one of: mp3, opus, wav.");
        }

        var settings = await _ttsSettings.GetCurrentAsync(cancellationToken);
        if (!settings.Enabled)
        {
            return AguiVoiceModeValidationResult.Failure(
                "voice_mode_disabled",
                "Text-to-speech is disabled for this tenant; voice mode is unavailable.");
        }

        var providerFormat = AudioFormatNegotiation.MapToProviderFormat(
            settings.DefaultProfile.Provider, requestedAbstractFormat);
        if (providerFormat is null)
        {
            return AguiVoiceModeValidationResult.Failure(
                "unsupported_audio_format",
                $"Provider '{settings.DefaultProfile.Provider}' does not support audioFormat '{requestedAbstractFormat}' for voice-mode AGUI.");
        }

        var audioMime = AudioFormatNegotiation.MapAbstractToMime(requestedAbstractFormat);

        return AguiVoiceModeValidationResult.Success(
            new AguiVoiceModeContext(providerFormat, requestedAbstractFormat, audioMime));
    }
}
