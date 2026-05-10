using Aonik.SharedKernel.Abstractions.Ai;

namespace Aonik.Voice.Endpoints.Admin;

/// <summary>
/// Admin / wire DTOs for the Voice & Speech settings page. Records mirror the
/// existing TTS settings shape so the front-end patterns translate one-for-one.
/// </summary>
public sealed record VoiceProviderSettingsResponse(
    bool Enabled,
    string Kind,
    string? RecipeId,
    ChainedVoiceSettingsResponse? Chained);

public sealed record ChainedVoiceSettingsResponse(
    SttSettingsResponse Stt,
    TtsSettingsResponse Tts,
    VadSettingsResponse Vad,
    bool TranscriptionFilter,
    bool SentenceAggregator);

public sealed record SttSettingsResponse(string Vendor, string? Model);
public sealed record TtsSettingsResponse(string Vendor, string? VoiceId, string? ModelId);
public sealed record VadSettingsResponse(string Kind, int? StopMs);

/// <summary>
/// Update payload — request shape for <c>PUT /tenant/settings/voice</c>. Same
/// fields as the response so the front-end can round-trip the same model.
/// </summary>
public sealed record VoiceProviderSettingsUpdateRequest(
    bool Enabled,
    string Kind,
    string? RecipeId,
    ChainedVoiceSettingsResponse? Chained);

/// <summary>
/// One v1-shipped recipe preset surfaced by <c>GET /tenant/settings/voice/recipes</c>.
/// Hand-curated, not loaded from a config file in v1.
/// </summary>
public sealed record VoiceRecipeResponse(
    string Id,
    string Name,
    string Description,
    string CostRanking,
    string LatencyTarget,
    bool Implemented,
    VoiceProviderSettingsResponse Settings);

/// <summary>
/// One available voice for a provider. Returned by
/// <c>GET /tenant/settings/voice/voices</c> so the front-end can populate the
/// voice picker.
/// </summary>
public sealed record VoiceOptionResponse(
    string Id,
    string Name,
    string? Description);

/// <summary>
/// Body for <c>POST /tenant/settings/voice/preview</c>. Provider-specific fields are optional; only
/// what the chosen provider actually needs gets read.
/// </summary>
public sealed record VoicePreviewRequest(
    string Text,
    string Provider,
    string VoiceId,
    string? ModelId,
    string? Region);

/// <summary>
/// Body for <c>POST /tenant/settings/voice/preview-stt</c>. Audio is uploaded as multipart along
/// with these fields; sample rate is required so the server can configure the engine to match.
/// </summary>
public sealed record SttPreviewMetadata(
    string Provider,
    string? Model,
    string? Language,
    string? Region,
    int? SampleRate);

/// <summary>Response for the STT preview endpoint.</summary>
public sealed record SttPreviewResponse(string Text, string? Language);

public sealed record VoiceProviderCredentialResponse(
    string Provider,
    bool HasHostCredential,
    bool HasTenantOverride,
    string EffectiveSource);

public sealed record VoiceProviderCredentialUpdateRequest(
    string? ApiKey,
    bool ClearStoredValue);

internal static class VoiceSettingsMappings
{
    public static VoiceProviderSettingsResponse ToResponse(VoiceProviderConfiguration config)
        => new(
            Enabled: config.Enabled,
            Kind: KindToWire(config.Kind),
            RecipeId: config.RecipeId,
            Chained: config.Chained is null ? null : ToResponse(config.Chained));

    public static ChainedVoiceSettingsResponse ToResponse(ChainedVoiceConfiguration chained)
        => new(
            Stt: new SttSettingsResponse(chained.Stt.Vendor, chained.Stt.Model),
            Tts: new TtsSettingsResponse(chained.Tts.Vendor, chained.Tts.VoiceId, chained.Tts.ModelId),
            Vad: new VadSettingsResponse(chained.Vad.Kind, chained.Vad.StopMs),
            TranscriptionFilter: chained.TranscriptionFilter,
            SentenceAggregator: chained.SentenceAggregator);

    public static VoiceProviderConfiguration FromUpdate(VoiceProviderSettingsUpdateRequest update)
        => new(
            Enabled: update.Enabled,
            Kind: KindFromWire(update.Kind),
            RecipeId: update.RecipeId,
            Chained: update.Chained is null ? null : new ChainedVoiceConfiguration(
                Stt: new SttSettings(update.Chained.Stt.Vendor, update.Chained.Stt.Model),
                Tts: new TtsSettings(update.Chained.Tts.Vendor, update.Chained.Tts.VoiceId, update.Chained.Tts.ModelId),
                Vad: new VadSettings(update.Chained.Vad.Kind, update.Chained.Vad.StopMs),
                TranscriptionFilter: update.Chained.TranscriptionFilter,
                SentenceAggregator: update.Chained.SentenceAggregator));

    private static string KindToWire(VoiceProviderKind kind) => kind switch
    {
        VoiceProviderKind.Chained => "chained",
        VoiceProviderKind.VoiceLive => "voice-live",
        VoiceProviderKind.OpenAiRealtime => "openai-realtime",
        VoiceProviderKind.AzureOpenAiRealtime => "azure-openai-realtime",
        _ => kind.ToString().ToLowerInvariant(),
    };

    private static VoiceProviderKind KindFromWire(string? kind) => (kind ?? "chained").ToLowerInvariant() switch
    {
        "chained" => VoiceProviderKind.Chained,
        "voice-live" => VoiceProviderKind.VoiceLive,
        "openai-realtime" => VoiceProviderKind.OpenAiRealtime,
        "azure-openai-realtime" => VoiceProviderKind.AzureOpenAiRealtime,
        _ => VoiceProviderKind.Chained,
    };
}
