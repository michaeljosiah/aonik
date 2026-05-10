using Aonik.SharedKernel.Abstractions.Ai;

namespace Aonik.Voice.Configuration;

/// <summary>
/// Result of validating a <see cref="VoiceProviderConfiguration"/> for v1.
/// </summary>
public sealed record VoiceProviderConfigurationValidation(
    bool IsValid,
    IReadOnlyList<string> Errors)
{
    public static VoiceProviderConfigurationValidation Success { get; } = new(true, Array.Empty<string>());

    public static VoiceProviderConfigurationValidation Failed(params string[] errors)
        => new(false, errors);
}

/// <summary>
/// Validates a <see cref="VoiceProviderConfiguration"/> against v1 constraints:
/// only the chained kind is allowed, only the OpenAI vendor is currently wired,
/// VAD/timing values are within sensible bounds.
///
/// <para>
/// See <c>docs/specifications/022.aonik-voice-realtime.md</c> Phase 5.
/// </para>
/// </summary>
public interface IVoiceProviderConfigurationValidator
{
    VoiceProviderConfigurationValidation Validate(VoiceProviderConfiguration configuration);
}

internal sealed class VoiceProviderConfigurationValidator : IVoiceProviderConfigurationValidator
{
    private static readonly HashSet<string> AllowedSttVendors = new(StringComparer.OrdinalIgnoreCase)
    {
        "openai-whisper",
        "openai",
    };

    private static readonly HashSet<string> AllowedTtsVendors = new(StringComparer.OrdinalIgnoreCase)
    {
        "openai",
    };

    private static readonly HashSet<string> AllowedVadKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "energy",
        "silero",
    };

    public VoiceProviderConfigurationValidation Validate(VoiceProviderConfiguration configuration)
    {
        if (configuration is null)
            return VoiceProviderConfigurationValidation.Failed("Configuration must not be null.");

        var errors = new List<string>();

        if (configuration.Kind != VoiceProviderKind.Chained)
        {
            errors.Add(
                $"Voice provider kind '{configuration.Kind}' is reserved for v1.1; v1 only accepts 'chained'. "
                + "See docs/specifications/022.aonik-voice-realtime.md Phase 7.");
            // Don't validate the chained-only fields below if the kind is wrong;
            // would just generate noise.
            return new VoiceProviderConfigurationValidation(false, errors);
        }

        if (configuration.Chained is null)
        {
            errors.Add("Configuration kind is 'chained' but 'chained' settings object is null.");
            return new VoiceProviderConfigurationValidation(false, errors);
        }

        var chained = configuration.Chained;

        if (chained.Stt is null)
        {
            errors.Add("'chained.stt' must be supplied.");
        }
        else if (string.IsNullOrWhiteSpace(chained.Stt.Vendor))
        {
            errors.Add("'chained.stt.vendor' must be supplied.");
        }
        else if (!AllowedSttVendors.Contains(chained.Stt.Vendor))
        {
            errors.Add($"STT vendor '{chained.Stt.Vendor}' not yet wired in v1. Use one of: {string.Join(", ", AllowedSttVendors)}.");
        }

        if (chained.Tts is null)
        {
            errors.Add("'chained.tts' must be supplied.");
        }
        else if (string.IsNullOrWhiteSpace(chained.Tts.Vendor))
        {
            errors.Add("'chained.tts.vendor' must be supplied.");
        }
        else if (!AllowedTtsVendors.Contains(chained.Tts.Vendor))
        {
            errors.Add($"TTS vendor '{chained.Tts.Vendor}' not yet wired in v1. Use one of: {string.Join(", ", AllowedTtsVendors)}.");
        }

        if (chained.Vad is null)
        {
            errors.Add("'chained.vad' must be supplied.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(chained.Vad.Kind))
            {
                errors.Add("'chained.vad.kind' must be supplied.");
            }
            else if (!AllowedVadKinds.Contains(chained.Vad.Kind))
            {
                errors.Add($"VAD kind '{chained.Vad.Kind}' is not recognised. Use one of: {string.Join(", ", AllowedVadKinds)}.");
            }

            if (chained.Vad.StopMs is { } ms && (ms < 50 || ms > 5000))
            {
                errors.Add($"VAD stopMs '{ms}' is out of range (50–5000).");
            }
        }

        return errors.Count == 0
            ? VoiceProviderConfigurationValidation.Success
            : new VoiceProviderConfigurationValidation(false, errors);
    }
}
