using Aonik.SharedKernel.Abstractions.Ai.Speech;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Voice.Endpoints.Admin.Speech;

/// <summary>
/// Per-vendor form schema served to the admin UI's provider edit panel. Encodes which fields
/// to render, their types, defaults, and option lists so adding a new vendor (in code) doesn't
/// require a UI rebuild — the front-end re-fetches this catalog and re-renders.
///
/// <para>
/// Post-Phase-D: fields are vendor-level only (region, default model, vendor-wide tunables).
/// Voice + model selection moved to the recipe + chat-speech forms — this catalog drives the
/// "set up the vendor" form, not the "use the vendor" form.
/// </para>
///
/// <para>
/// Not strict JSON Schema. Closer in spirit to <a href="https://jsonforms.io">JSONForms</a>:
/// each field declares its widget hint, and the renderer maps it to a typed input. Keeps the
/// schema small and the renderer simple.
/// </para>
/// </summary>
internal sealed class SpeechVendorsCatalogEndpoint : EndpointWithoutRequest<SpeechVendorsCatalogResponse>
{
    public override void Configure()
    {
        Get("/speech-vendors");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Speech vendors form catalog";
            s.Description = "Returns the per-vendor form schema the admin UI uses to render the provider edit panel. Post-Phase-D: fields are vendor-level only — voice/model selection moved to recipe + chat-speech forms.";
            s.Response(200, "Catalog");
        });
        Options(x => x.WithTags("Speech library"));
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync(SpeechVendorCatalog.BuildResponse(), ct);
}

// ── Wire DTOs ───────────────────────────────────────────────────────────────────────────

public sealed record SpeechVendorsCatalogResponse(
    IReadOnlyList<SpeechVendorDescriptor> Vendors);

public sealed record SpeechVendorDescriptor(
    /// <summary>Vendor shortcode used in <c>SpeechProvider.Vendor</c> and <c>SpeechProviderConfig</c>'s discriminator.</summary>
    string Vendor,
    /// <summary>Human-readable vendor name (e.g. "OpenAI", "Azure Speech").</summary>
    string DisplayName,
    /// <summary>Which provider type(s) this vendor supports.</summary>
    IReadOnlyList<SpeechProviderType> SupportedTypes,
    /// <summary>Per-(vendor, type) form schema. Keyed by <see cref="SpeechProviderType"/>.</summary>
    IReadOnlyList<SpeechVendorFormSchema> Forms);

public sealed record SpeechVendorFormSchema(
    SpeechProviderType Type,
    /// <summary>Discriminator value used when serializing the resulting <see cref="SpeechProviderConfig"/>.</summary>
    string ConfigKind,
    IReadOnlyList<SpeechVendorFormField> Fields);

public sealed record SpeechVendorFormField(
    /// <summary>JSON property name on the config record.</summary>
    string Name,
    /// <summary>UI label.</summary>
    string Label,
    /// <summary>Renderer hint: <c>text</c>, <c>password</c>, <c>select</c>, <c>number</c>, <c>textarea</c>.</summary>
    string Widget,
    bool Required,
    /// <summary>Helper text shown beneath the input.</summary>
    string? Description = null,
    /// <summary>Placeholder text for free-form inputs.</summary>
    string? Placeholder = null,
    /// <summary>Default value if the user leaves the field blank.</summary>
    string? Default = null,
    /// <summary>For <c>select</c> widgets: the dropdown options.</summary>
    IReadOnlyList<SpeechVendorFormOption>? Options = null,
    /// <summary>For <c>number</c> widgets: validation bounds.</summary>
    double? Min = null,
    double? Max = null);

public sealed record SpeechVendorFormOption(string Value, string Label, string? Description = null);

// ── Catalog data ────────────────────────────────────────────────────────────────────────

internal static class SpeechVendorCatalog
{
    public static SpeechVendorsCatalogResponse BuildResponse() => new(new[]
    {
        OpenAIVendor(),
        AzureVendor(),
        ElevenLabsVendor(),
        MistralVendor(),
        OpenAIRealtimeVendor(),
        AzureVoiceLiveVendor(),
    });

    private static SpeechVendorDescriptor OpenAIVendor() => new(
        Vendor: "openai",
        DisplayName: "OpenAI",
        SupportedTypes: new[] { SpeechProviderType.Stt, SpeechProviderType.Tts },
        Forms: new[]
        {
            new SpeechVendorFormSchema(
                Type: SpeechProviderType.Stt,
                ConfigKind: "openai-whisper",
                Fields: new[]
                {
                    Field("defaultModel", "Default model", "select", required: false, defaultValue: "whisper-1",
                        options: new[]
                        {
                            new SpeechVendorFormOption("whisper-1", "whisper-1", "Standard Whisper model"),
                        },
                        description: "Recipes can override per-call. Leave blank to use whisper-1."),
                    Field("defaultLanguage", "Default language hint (BCP-47)", "text", required: false,
                        placeholder: "en (auto-detect if blank)",
                        description: "Bias Whisper toward a specific language. Recipes can override."),
                }),

            new SpeechVendorFormSchema(
                Type: SpeechProviderType.Tts,
                ConfigKind: "openai-tts",
                Fields: new[]
                {
                    Field("defaultModelId", "Default model", "select", required: false, defaultValue: "tts-1",
                        options: new[]
                        {
                            new SpeechVendorFormOption("tts-1", "tts-1", "Standard quality, lower latency"),
                            new SpeechVendorFormOption("tts-1-hd", "tts-1-hd", "Higher fidelity"),
                            new SpeechVendorFormOption("gpt-4o-mini-tts", "gpt-4o-mini-tts", "GPT-4o-based"),
                        },
                        description: "Recipes can override per-call."),
                }),
        });

    private static SpeechVendorDescriptor AzureVendor() => new(
        Vendor: "azure",
        DisplayName: "Azure Speech",
        SupportedTypes: new[] { SpeechProviderType.Stt, SpeechProviderType.Tts },
        Forms: new[]
        {
            new SpeechVendorFormSchema(
                Type: SpeechProviderType.Stt,
                ConfigKind: "azure-stt",
                Fields: new[]
                {
                    Field("region", "Region", "text", required: true, defaultValue: "eastus",
                        placeholder: "e.g. eastus, westeurope, uksouth"),
                    Field("defaultLanguage", "Default recognition language (BCP-47)", "text", required: false,
                        defaultValue: "en-US"),
                }),

            new SpeechVendorFormSchema(
                Type: SpeechProviderType.Tts,
                ConfigKind: "azure-tts",
                Fields: new[]
                {
                    Field("region", "Region", "text", required: true, defaultValue: "eastus",
                        description: "Azure Speech is region-pinned. Voice picks happen on the recipe."),
                }),
        });

    private static SpeechVendorDescriptor ElevenLabsVendor() => new(
        Vendor: "elevenlabs",
        DisplayName: "ElevenLabs",
        SupportedTypes: new[] { SpeechProviderType.Tts },
        Forms: new[]
        {
            new SpeechVendorFormSchema(
                Type: SpeechProviderType.Tts,
                ConfigKind: "elevenlabs-tts",
                Fields: new[]
                {
                    Field("defaultModelId", "Default model", "select", required: false, defaultValue: "eleven_multilingual_v2",
                        options: new[]
                        {
                            new SpeechVendorFormOption("eleven_multilingual_v2", "Multilingual v2", "Stable, multi-language"),
                            new SpeechVendorFormOption("eleven_turbo_v2_5", "Turbo v2.5", "Lower latency"),
                            new SpeechVendorFormOption("eleven_flash_v2_5", "Flash v2.5", "Lowest latency"),
                        }),
                    Field("defaultStability", "Default stability", "number", required: false, min: 0, max: 1,
                        description: "0.0–1.0. Higher = more consistent. Vendor-wide default."),
                    Field("defaultSimilarityBoost", "Default similarity boost", "number", required: false, min: 0, max: 1,
                        description: "0.0–1.0. Higher = closer to original voice."),
                    Field("defaultOptimizeStreamingLatency", "Default latency optimization", "number", required: false, min: 0, max: 4,
                        description: "0–4. Higher = lower latency, more artefacts."),
                }),
        });

    private static SpeechVendorDescriptor MistralVendor() => new(
        Vendor: "mistral",
        DisplayName: "Mistral (Voxtral)",
        SupportedTypes: new[] { SpeechProviderType.Tts },
        Forms: new[]
        {
            new SpeechVendorFormSchema(
                Type: SpeechProviderType.Tts,
                ConfigKind: "mistral-tts",
                Fields: new[]
                {
                    // Mistral publishes a single Voxtral TTS model id today; the legacy
                    // "voxtral-tts" placeholder is rewritten by AonikMistralVoiceEngine
                    // so existing rows keep working. Dropdown stays a `select` so a new
                    // option can ship without re-editing the field's widget.
                    Field("defaultModelId", "Default model", "select", required: false,
                        defaultValue: "voxtral-mini-tts-2603",
                        options: new[]
                        {
                            new SpeechVendorFormOption(
                                "voxtral-mini-tts-2603",
                                "voxtral-mini-tts-2603",
                                "Low-latency Voxtral mini (only published TTS model id)."),
                        },
                        description: "Recipes can override per-call."),
                    // Mistral's `/v1/audio/speech` returns Server-Sent Events whose
                    // `audio_data` payloads carry whichever container the request asked
                    // for. AonikMistralVoiceEngine supports WAV and raw PCM; everything
                    // else needs a decoder we don't ship yet. WAV is the default because
                    // its 44-byte header auto-validates sample rate / bit depth.
                    Field("defaultResponseFormat", "Audio format", "select", required: false,
                        defaultValue: "wav",
                        options: new[]
                        {
                            new SpeechVendorFormOption(
                                "wav",
                                "WAV (default)",
                                "PCM + 44-byte header. Validates 24 kHz / 16-bit / mono on the wire."),
                            new SpeechVendorFormOption(
                                "pcm",
                                "Raw PCM",
                                "Marginally lower latency. Trusts 24 kHz / 16-bit / mono; a vendor-side rate change would distort playback."),
                        },
                        description: "WAV is safest; pick PCM only if you've benchmarked the latency win and pinned the vendor rate."),
                }),
        });

    private static SpeechVendorDescriptor OpenAIRealtimeVendor() => new(
        Vendor: "openai-realtime",
        DisplayName: "OpenAI Realtime",
        SupportedTypes: new[] { SpeechProviderType.Composite },
        Forms: new[]
        {
            new SpeechVendorFormSchema(
                Type: SpeechProviderType.Composite,
                ConfigKind: "openai-realtime",
                Fields: new[]
                {
                    Field("defaultModel", "Default model", "select", required: false, defaultValue: "gpt-realtime-mini",
                        options: new[]
                        {
                            new SpeechVendorFormOption("gpt-realtime-mini", "gpt-realtime-mini", "Cost-optimised"),
                            new SpeechVendorFormOption("gpt-realtime", "gpt-realtime", "Highest fidelity"),
                        }),
                    Field("defaultInstructionsAddendum", "Default instruction addendum", "textarea", required: false,
                        description: "Vendor-wide append to the resolved agent's instructions. Recipes can override."),
                }),
        });

    private static SpeechVendorDescriptor AzureVoiceLiveVendor() => new(
        Vendor: "azure-voice-live",
        DisplayName: "Azure Voice Live",
        SupportedTypes: new[] { SpeechProviderType.Composite },
        Forms: new[]
        {
            new SpeechVendorFormSchema(
                Type: SpeechProviderType.Composite,
                ConfigKind: "azure-voice-live",
                Fields: new[]
                {
                    Field("region", "Region", "text", required: true, defaultValue: "uksouth",
                        description: "Voice Live availability is regional."),
                    Field("endpoint", "Endpoint URL", "text", required: true,
                        placeholder: "wss://uksouth.tts.speech.microsoft.com/cognitiveservices/voicelive"),
                    Field("defaultModel", "Default model", "select", required: false, defaultValue: "gpt-realtime-mini",
                        options: new[]
                        {
                            new SpeechVendorFormOption("gpt-realtime-mini", "gpt-realtime-mini"),
                            new SpeechVendorFormOption("gpt-realtime", "gpt-realtime"),
                            new SpeechVendorFormOption("phi4-mm-realtime", "phi4-mm-realtime"),
                        }),
                    Field("defaultInstructionsAddendum", "Default instruction addendum", "textarea", required: false),
                }),
        });

    private static SpeechVendorFormField Field(
        string name,
        string label,
        string widget,
        bool required,
        string? defaultValue = null,
        string? placeholder = null,
        string? description = null,
        IReadOnlyList<SpeechVendorFormOption>? options = null,
        double? min = null,
        double? max = null)
        => new(
            Name: name,
            Label: label,
            Widget: widget,
            Required: required,
            Description: description,
            Placeholder: placeholder,
            Default: defaultValue,
            Options: options,
            Min: min,
            Max: max);
}
