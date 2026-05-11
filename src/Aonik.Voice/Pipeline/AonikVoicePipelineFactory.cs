using System.Net.WebSockets;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Ai.Speech;
using Aonik.Voice.Frames;
using Aonik.Voice.Processors;
using Aonik.Voice.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Voxa.Audio.SileroVad;
using Voxa.Frames;
using Voxa.Pipelines;
using Voxa.Processors;
using Voxa.Speech;
using Voxa.Speech.Azure;
using Voxa.Speech.ElevenLabs;
using Voxa.Speech.Mistral;
using Voxa.Speech.OpenAI;
using Voxa.Services.AzureVoiceLive;
using Voxa.Services.OpenAIRealtime;
using Voxa.Transports.WebSocket;

namespace Aonik.Voice.Pipeline;

/// <summary>
/// Strategy dispatcher that composes the per-connection Voxa pipeline. v1 implements the
/// chained-OpenAI recipe; other recipe shapes throw with an explicit "not yet wired" message
/// so the admin UI surfaces it.
///
/// <para>
/// As of spec 024 Phase C.2 the runtime resolves recipes from
/// <see cref="IVoiceModeSettingsService"/> + the speech library, not the legacy
/// <c>ITenantVoiceProviderSettingsService</c>. The endpoint does the resolution and hands
/// us a fully-resolved <see cref="ChainedRecipeRuntimeSpec"/> — this factory never reads
/// from settings stores directly.
/// </para>
/// </summary>
public interface IAonikVoicePipelineFactory
{
    Voxa.Pipelines.Pipeline BuildChained(VoicePipelineBuildRequest request, ChainedRecipeRuntimeSpec recipe);

    /// <summary>
    /// Build a composite voice pipeline (OpenAI Realtime or Azure Voice Live). v1 wiring
    /// pipes audio in and out of the bidirectional composite processor. Tool dispatch and
    /// chat-thread persistence are deferred follow-ups — voice chat works, but the agent
    /// can't invoke MAF functions yet and the conversation isn't persisted to AGUI.
    /// </summary>
    Voxa.Pipelines.Pipeline BuildComposite(VoicePipelineBuildRequest request, CompositeRecipeRuntimeSpec recipe);
}

/// <summary>
/// Inputs for one WS-connection-scoped pipeline build.
/// </summary>
public sealed record VoicePipelineBuildRequest(
    System.Net.WebSockets.WebSocket WebSocket,
    AIAgent VoiceAgent,
    ChatMessage? UserBriefPreamble,
    ChatClientAgentRunOptions? RunOptions,
    IReadOnlySet<string> FrontendToolNames,
    string? InitialChatThreadId,
    string? AgentId,
    Guid? TenantId,
    Guid? UserId,
    IServiceProvider RequestServices);

/// <summary>
/// Fully-resolved chained recipe — STT + TTS provider configs and the per-recipe voice + model
/// picks already pulled from the speech library. The factory pattern-matches on the config
/// types to wire concrete engines and uses the recipe-level picks (not the provider's defaults)
/// for voice + model selection.
/// </summary>
/// <param name="RecipeId">Recipe id (built-in or tenant Guid). Surfaced for logging only.</param>
/// <param name="RecipeDisplayName">Human-readable recipe name. Surfaced for error messages.</param>
/// <param name="SttProviderDisplayName">Human-readable STT provider name. For error messages.</param>
/// <param name="TtsProviderDisplayName">Human-readable TTS provider name. For error messages.</param>
/// <param name="SttConfig">Polymorphic STT config; runtime requires <see cref="OpenAIWhisperConfig"/>.</param>
/// <param name="TtsConfig">Polymorphic TTS config; runtime requires <see cref="OpenAITtsConfig"/>.</param>
/// <param name="TtsVoiceId">Per-recipe voice id (no longer on the provider config).</param>
/// <param name="TtsModelId">Per-recipe model override; null falls back to the provider's default.</param>
/// <param name="SttModel">Per-recipe STT model override; null falls back to the provider's default.</param>
/// <param name="SttLanguage">Per-recipe STT language hint; null falls back to the provider's default.</param>
/// <param name="UseSentenceAggregator">If false, the SentenceAggregator is omitted from the pipeline.</param>
public sealed record ChainedRecipeRuntimeSpec(
    string RecipeId,
    string RecipeDisplayName,
    string SttProviderDisplayName,
    string TtsProviderDisplayName,
    SpeechProviderConfig SttConfig,
    SpeechProviderConfig TtsConfig,
    string TtsVoiceId,
    string? TtsModelId,
    string? SttModel,
    string? SttLanguage,
    bool UseSentenceAggregator,
    /// <summary>
    /// VAD mode chosen on the recipe in the admin UI. "energy" / "silence" →
    /// SilenceGateProcessor, "silero" → SileroVadProcessor, "none" →
    /// passthrough (tests only). If empty, falls back to the global
    /// <c>Voice:Vad</c> configuration, then to "energy".
    /// </summary>
    string? Vad,
    /// <summary>
    /// Silence-required-before-gate-close (Pipecat's <c>stop_secs</c>) for
    /// both Silence and Silero. Null = vendor default (800 ms).
    /// </summary>
    int? VadStopMs);

/// <summary>
/// Fully-resolved composite recipe — the composite provider's config + the per-recipe
/// voice / model / instructions picks. The factory pattern-matches on
/// <see cref="ProviderConfig"/> to wire the right concrete processor.
/// </summary>
/// <param name="RecipeId">Recipe id. Surfaced for logging only.</param>
/// <param name="RecipeDisplayName">Human-readable recipe name. Surfaced for error messages.</param>
/// <param name="ProviderDisplayName">Human-readable provider name. For error messages.</param>
/// <param name="ProviderConfig">Polymorphic provider config; runtime requires <see cref="OpenAIRealtimeCompositeConfig"/> or <see cref="AzureVoiceLiveCompositeConfig"/>.</param>
/// <param name="Voice">Per-recipe voice id (e.g. <c>alloy</c>, <c>nova</c>).</param>
/// <param name="Model">Per-recipe model override; null falls back to the provider's default.</param>
/// <param name="InstructionsAddendum">Per-recipe instruction addendum appended to the agent instructions.</param>
public sealed record CompositeRecipeRuntimeSpec(
    string RecipeId,
    string RecipeDisplayName,
    string ProviderDisplayName,
    SpeechProviderConfig ProviderConfig,
    string Voice,
    string? Model,
    string? InstructionsAddendum);

internal sealed class AonikVoicePipelineFactory : IAonikVoicePipelineFactory
{
    private readonly ILoggerFactory _loggerFactory;
    // Voxa's ElevenLabs + Mistral factories take an HttpClient since they hit a REST endpoint
    // per synthesis frame. IHttpClientFactory is always available in the AspNetCore host —
    // we use it so DNS rotation, pooling, and SocketsHttpHandler timers behave correctly
    // across long-running voice connections.
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public AonikVoicePipelineFactory(
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public Voxa.Pipelines.Pipeline BuildChained(VoicePipelineBuildRequest request, ChainedRecipeRuntimeSpec recipe)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(recipe);

        // Phase G: each leg dispatches by config type so admins can mix and match the four
        // vendors we ship adapters for (OpenAI, Azure, ElevenLabs, Mistral). STT supports
        // OpenAI Whisper + Azure today; TTS supports all four. Composite recipes
        // (Voice Live / OpenAI Realtime) still throw — they need bidirectional Voxa
        // processors that aren't in 0.4.0-alpha yet, so the endpoint dispatches them away
        // from here.
        var credentialResolver = request.RequestServices
            .GetRequiredService<Aonik.SharedKernel.Abstractions.Ai.IVoiceProviderCredentialResolver>();

        var stt = BuildSttProcessor(recipe, credentialResolver);
        var tts = BuildTtsProcessor(recipe, credentialResolver);

        // Tag incoming binary frames as 16 kHz mono PCM. The Voxa default is 24 kHz
        // (Voice Live's native rate), but BOTH our clients send 16 kHz: Flutter's
        // `record` plugin records 16-bit PCM at 16 kHz to match Whisper's expectation,
        // and the admin UI's `LiveVoiceTestCard` resamples to 16 kHz before sending.
        // Without this override, `AudioRawFrame.SampleRate == 24000` mis-tag would
        // cause SileroVadProcessor to short-circuit ("sample rate mismatch, forwarding
        // without VAD") — defeating the whole point of wiring it in below.
        var source = new WebSocketAudioSource(
            request.WebSocket,
            new WebSocketAudioOptions
            {
                InputSampleRate = 16000,
                Channels = 1,
            });

        // Voxa's WebSocketAudioSink + custom-serializer hook covers the AONIK threadReady
        // envelope. No subclass needed; the local AonikVoiceWebSocketSink was retired in
        // Phase B.
        var sink = new WebSocketAudioSink(
            request.WebSocket,
            customSerializer: ThreadReadyFrameSerializer.Serialize);

        var normalizer = new SpeechTextNormalizerProcessor(
            request.RequestServices.GetRequiredService<ISpeechTextNormalizer>());

        // Voxa.Services.MicrosoftAgents owns the agent loop; AonikVoiceAgent supplies the
        // AONIK-specific closures (ChatThread persistence, user-brief preamble, audit, threadReady).
        var agent = AonikVoiceAgent.CreateProcessor(
            voiceAgent: request.VoiceAgent,
            userBriefPreamble: request.UserBriefPreamble,
            runOptions: request.RunOptions,
            frontendToolNames: request.FrontendToolNames,
            threadManager: request.RequestServices.GetRequiredService<IChatThreadManager>(),
            converter: request.RequestServices.GetRequiredService<IAguiMessageConverter>(),
            postStreamCoordinator: request.RequestServices.GetRequiredService<IPostStreamPersistenceCoordinator>(),
            initialChatThreadId: request.InitialChatThreadId,
            agentId: request.AgentId,
            tenantId: request.TenantId,
            userId: request.UserId,
            logger: _loggerFactory.CreateLogger("Aonik.Voice.AonikVoiceAgent"));

        // Pipeline shape mirrors Voxa's reference sample at /voice/openai-batch —
        // proven working end-to-end against this same Whisper + TTS stack:
        //
        //   WebSocketAudioSource → AudioArrivalLogger → VAD (config-driven)
        //   → STT → TranscriptionFilter → AonikVoiceAgent → [SentenceAggregator?]
        //   → SpeechTextNormalizerProcessor → TextToSpeechProcessor → WebSocketAudioSink
        //
        // VAD is REQUIRED — Voxa's OpenAIWhisperEngine only flushes on
        // UserStoppedSpeakingFrame. The earlier comment claiming "Whisper handles
        // segmentation server-side" was wrong; see SpeechToTextProcessor.cs:66-73.
        //
        // VAD selector via `Voice:Vad` configuration:
        //
        //   "silence" (default) — SilenceGateProcessor. Pure RMS math, no native deps,
        //                         can't fail to initialise. Proven working end-to-end.
        //                         Lower noise rejection than Silero — fans / keyboards /
        //                         distant chatter all pass through and may trigger
        //                         Whisper hallucinations (caught by TranscriptionFilter).
        //   "silero"            — SileroVadProcessor. ML-based gate via ONNX Runtime
        //                         loading Silero v6. Much better noise rejection but
        //                         needs the ONNX runtime to load a shipped model on
        //                         first call. Opt-in until we have OTEL on the init
        //                         path — if init fails on the container the pipeline
        //                         silently bypasses to SilenceGate per Silero's own
        //                         sample-rate-mismatch fallback. ConfidenceThreshold
        //                         tuned to 0.3 for AGC'd browser / mobile mics per
        //                         Pipecat's documented value.
        //   "none"              — Skip VAD entirely. Use ONLY for tests that drive
        //                         the pipeline with synthetic UserStartedSpeakingFrame /
        //                         UserStoppedSpeakingFrame events directly; in
        //                         production this freezes the STT engine waiting for
        //                         frames that never come.
        //
        // Why TranscriptionFilter: Whisper hallucinates "Thank you.", "you", "." etc.
        // from breath and room noise. Without the filter the agent re-runs on every
        // hallucination, racking up API spend AND adding latency to genuine turns.
        //
        // Why AudioArrivalLogger: same diag the sample uses. Prints frames/bytes/peak-RMS
        // per second so we can see audio is actually flowing through the WS at the
        // expected sample rate. Inserted right after the source so it instruments the
        // raw inbound, not the gate-filtered stream.
        //
        // The sentence aggregator stays gated by the recipe's UseSentenceAggregator
        // knob — disabling it lets tokens flow unbuffered (snappier echoback agents).
        var audioArrivalLogger = new AudioArrivalLogger(
            _loggerFactory.CreateLogger("Aonik.Voice.AudioArrival"));

        var vadLogger = _loggerFactory.CreateLogger("Aonik.Voice.Vad");
        var vad = BuildVadProcessor(recipe, _configuration, vadLogger);

        var transcriptionFilter = new TranscriptionFilter();

        var builder = Voxa.Pipelines.Pipeline.Build()
            .Source(source)
            .Then(audioArrivalLogger)
            .Then(vad)
            .Then(stt)
            .Then(transcriptionFilter)
            .Then(agent);

        if (recipe.UseSentenceAggregator)
        {
            builder = builder.Then(new SentenceAggregator());
        }

        return builder
            .Then(normalizer)
            .Then(tts)
            .Sink(sink);
    }

    // ── Composite pipeline (OpenAI Realtime / Azure Voice Live) ──────────────────────────

    /// <summary>
    /// Build the per-connection composite voice pipeline. The shape is dramatically
    /// simpler than the chained one: the composite processor folds STT+LLM+TTS+VAD+turn-
    /// taking into a single bidirectional WSS session, so we only need to bridge audio
    /// frames in and out plus upsample 16 kHz mic audio to the 24 kHz both vendors
    /// require on their input audio buffer.
    ///
    /// <para>
    /// Pipeline:
    /// </para>
    ///
    /// <code>
    ///   WebSocketAudioSource(16 kHz)
    ///     → LinearResampler(16→24 kHz)
    ///     → AudioArrivalLogger
    ///     → OpenAIRealtimeProcessor | AzureVoiceLiveProcessor
    ///     → WebSocketAudioSink
    /// </code>
    ///
    /// <para>
    /// <b>v1 limitations</b> — both deferred to follow-up tasks:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <b>Tool dispatch.</b> Voxa's composite processors emit
    ///     <c>ToolCallRequestFrame</c> when the model invokes a tool, and expect
    ///     <c>ToolCallResultFrame</c> back. We pass an empty Tools list for now so the
    ///     model can't invoke functions — the experience is conversational only. Wiring
    ///     MAF <c>AIFunction</c> dispatch into a downstream <c>MafToolDispatcher</c>
    ///     processor is the natural follow-up.
    ///   </item>
    ///   <item>
    ///     <b>Chat-thread persistence.</b> The chained path runs every turn through
    ///     <see cref="AonikVoiceAgent.CreateProcessor"/> which writes <c>AiRun</c> +
    ///     ChatThread rows. The composite path bypasses that processor entirely; voice
    ///     conversations through GPT Realtime aren't yet saved to AGUI history. Will be
    ///     wired by capturing the composite's <c>TranscriptionFrame</c> +
    ///     <c>LlmTextChunkFrame</c> emissions and feeding them through a slimmed
    ///     persistence-only processor.
    ///   </item>
    /// </list>
    /// </summary>
    public Voxa.Pipelines.Pipeline BuildComposite(VoicePipelineBuildRequest request, CompositeRecipeRuntimeSpec recipe)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(recipe);

        var credentialResolver = request.RequestServices
            .GetRequiredService<Aonik.SharedKernel.Abstractions.Ai.IVoiceProviderCredentialResolver>();

        // Mic clients send 16 kHz; both composite APIs require 24 kHz pcm16. Tag the
        // source as 16 kHz so the resampler picks the frames up (frames at any other
        // sample rate pass through unchanged).
        var source = new WebSocketAudioSource(
            request.WebSocket,
            new WebSocketAudioOptions
            {
                InputSampleRate = 16000,
                Channels = 1,
            });

        // Voxa's WebSocketAudioSink covers BotStarted/BotStopped/User*Speaking + Audio
        // frames natively — the composite processor emits all of these — so JSON
        // `speaking` and `interruption` envelopes reach the client without extra glue.
        var sink = new WebSocketAudioSink(
            request.WebSocket,
            customSerializer: ThreadReadyFrameSerializer.Serialize);

        var resampler = new LinearResamplerProcessor(inputSampleRate: 16000, outputSampleRate: 24000);

        var audioArrivalLogger = new AudioArrivalLogger(
            _loggerFactory.CreateLogger("Aonik.Voice.AudioArrival"));

        var composite = BuildCompositeProcessor(request, recipe, credentialResolver);

        return Voxa.Pipelines.Pipeline.Build()
            .Source(source)
            .Then(resampler)
            .Then(audioArrivalLogger)
            .Then(composite)
            .Sink(sink);
    }

    /// <summary>
    /// Dispatch on the composite provider config to construct the right Voxa processor.
    /// Throws <see cref="VoiceConfigurationException"/> if the config shape doesn't match
    /// either of the two supported vendors — the endpoint surfaces the message to the
    /// client.
    /// </summary>
    private FrameProcessor BuildCompositeProcessor(
        VoicePipelineBuildRequest request,
        CompositeRecipeRuntimeSpec recipe,
        IVoiceProviderCredentialResolver credentialResolver)
    {
        // Resolve agent instructions (best-effort). The composite vendors take their own
        // instructions field; we prepend the agent's resolved instructions so behaviour
        // stays consistent with the chained path even though tools aren't wired yet.
        var instructions = ComposeCompositeInstructions(request, recipe);

        return recipe.ProviderConfig switch
        {
            OpenAIRealtimeCompositeConfig openai => new OpenAIRealtimeProcessor(
                options: new OpenAIRealtimeOptions
                {
                    ApiKey = ResolveApiKey(credentialResolver, "openai-realtime", recipe.ProviderDisplayName),
                    Model = recipe.Model ?? openai.DefaultModel ?? "gpt-realtime-mini",
                    Voice = recipe.Voice,
                    Instructions = instructions,
                    // v1: empty tools. See BuildComposite XML for follow-up plan.
                    Tools = Array.Empty<OpenAIRealtimeTool>(),
                },
                transportFactory: null,
                logger: _loggerFactory.CreateLogger<OpenAIRealtimeProcessor>()),

            AzureVoiceLiveCompositeConfig azure => new AzureVoiceLiveProcessor(
                options: new AzureVoiceLiveOptions
                {
                    Endpoint = ParseRequiredUri(azure.Endpoint, recipe.ProviderDisplayName, "Endpoint"),
                    ApiKey = ResolveApiKey(credentialResolver, "azure-voice-live", recipe.ProviderDisplayName),
                    Model = recipe.Model ?? azure.DefaultModel ?? "gpt-realtime-mini",
                    Voice = recipe.Voice,
                    Instructions = instructions,
                    Tools = Array.Empty<AzureVoiceLiveTool>(),
                },
                transportFactory: null,
                logger: _loggerFactory.CreateLogger<AzureVoiceLiveProcessor>()),

            _ => throw new VoiceConfigurationException(
                $"Composite provider '{recipe.ProviderDisplayName}' uses {recipe.ProviderConfig.GetType().Name}; "
                + "supported composite configs are OpenAIRealtimeCompositeConfig and AzureVoiceLiveCompositeConfig."),
        };
    }

    /// <summary>
    /// Best-effort instruction composition. The recipe-level addendum is appended to the
    /// agent's resolved instructions (if any) so a recipe can layer voice-specific
    /// behaviour on top of the agent's primary system prompt. If the agent doesn't
    /// expose instructions through its public surface, the addendum stands alone.
    /// </summary>
    private static string? ComposeCompositeInstructions(
        VoicePipelineBuildRequest request,
        CompositeRecipeRuntimeSpec recipe)
    {
        // Aonik agents (ChatClientAgent) expose Instructions on the underlying ChatOptions.
        // The AIAgent base class doesn't have it directly, so we read it off the run
        // options the endpoint already prepared. If absent (e.g. agents whose instructions
        // live elsewhere), fall back to just the recipe addendum.
        var agentInstructions = request.RunOptions?.ChatOptions?.Instructions;
        if (string.IsNullOrWhiteSpace(agentInstructions))
        {
            return string.IsNullOrWhiteSpace(recipe.InstructionsAddendum)
                ? null
                : recipe.InstructionsAddendum;
        }

        return string.IsNullOrWhiteSpace(recipe.InstructionsAddendum)
            ? agentInstructions
            : $"{agentInstructions}\n\n{recipe.InstructionsAddendum}";
    }

    private static Uri ParseRequiredUri(string? raw, string providerDisplayName, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new VoiceConfigurationException(
                $"Composite provider '{providerDisplayName}' is missing a {fieldName}. "
                + "Edit the provider config and set the WSS endpoint URL.");
        }
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            throw new VoiceConfigurationException(
                $"Composite provider '{providerDisplayName}' has an invalid {fieldName}: '{raw}'. "
                + "Expected an absolute URI (e.g. 'wss://<resource>.cognitiveservices.azure.com/voice-live/realtime?model=...&api-version=...').");
        }
        return uri;
    }

    // ── VAD selector ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pick the VAD processor for this connection. Resolution order:
    /// recipe-level <see cref="ChainedRecipeRuntimeSpec.Vad"/> → host-wide
    /// <c>Voice:Vad</c> configuration → "energy" (SilenceGate). Stop-silence
    /// ms uses the recipe value first, then <c>Voice:VadStopMs</c>, then 800.
    ///
    /// <para>
    /// "energy" / "silence" → <see cref="SilenceGateProcessor"/>. Proven
    /// working, zero native deps. The default for new recipes.
    /// "silero" → <see cref="SileroVadProcessor"/>. ML-based, much better
    /// noise rejection but requires ONNX Runtime. Opt-in per recipe.
    /// "none" → passthrough; for tests that inject UserStarted/Stopped
    /// frames synthetically. Using it in production freezes STT.
    /// </para>
    /// </summary>
    private static FrameProcessor BuildVadProcessor(
        ChainedRecipeRuntimeSpec recipe,
        IConfiguration configuration,
        ILogger logger)
    {
        // Recipe > host config > "energy" default.
        var rawMode = !string.IsNullOrWhiteSpace(recipe.Vad)
            ? recipe.Vad!
            : configuration["Voice:Vad"];
        var mode = (rawMode ?? string.Empty).Trim().ToLowerInvariant();
        var stopMs = recipe.VadStopMs
            ?? (int.TryParse(configuration["Voice:VadStopMs"], out var s) ? s : 800);
        var startMs = int.TryParse(configuration["Voice:VadStartMs"], out var st) ? st : 200;

        switch (mode)
        {
            case "silero":
            case "silerovad":
            case "ml":
                logger.LogInformation(
                    "VAD = Silero (ML, ONNX) for recipe '{Recipe}'. startMs={StartMs} stopMs={StopMs}. " +
                    "Falls back to passthrough on sample-rate mismatch — watch for "
                    + "'SileroVadProcessor received audio at … forwarding without VAD' warnings.",
                    recipe.RecipeDisplayName, startMs, stopMs);
                return new SileroVadProcessor(new SileroVadOptions
                {
                    SampleRate = 16000,
                    // Browser / phone mic AGC compresses dynamic range; Silero's stock
                    // 0.5 confidence threshold under-triggers on quiet speech. 0.3 is
                    // Pipecat's documented value for AGC'd mics.
                    ConfidenceThreshold = 0.3f,
                    StartDuration = TimeSpan.FromMilliseconds(startMs),
                    StopDuration = TimeSpan.FromMilliseconds(stopMs),
                });

            case "none":
            case "off":
            case "disabled":
                logger.LogWarning(
                    "VAD = none (passthrough) for recipe '{Recipe}'. STT will never receive "
                    + "UserStoppedSpeakingFrame; audio buffers until Voxa's SttBufferSeconds "
                    + "backstop fires. Use only for tests with synthetic turn frames.",
                    recipe.RecipeDisplayName);
                return new PassthroughVad();

            default:
                // "energy" / "silence" / anything else → SilenceGate. The
                // canonical name for the energy gate in our recipe schema is
                // "energy"; "silence" is accepted as an alias for parity with
                // the global Voice:Vad config naming.
                logger.LogInformation(
                    "VAD = SilenceGate (energy + hangover) for recipe '{Recipe}'. hangoverMs={StopMs}",
                    recipe.RecipeDisplayName, stopMs);
                return new SilenceGateProcessor(
                    hangover: TimeSpan.FromMilliseconds(stopMs));
        }
    }

    /// <summary>
    /// No-op VAD used when <c>Voice:Vad=none</c>. Forwards all frames untouched,
    /// emits no UserStarted / UserStopped speaking frames. Practically only useful
    /// for tests that inject those frames synthetically.
    /// </summary>
    private sealed class PassthroughVad : FrameProcessor
    {
        public PassthroughVad() : base("PassthroughVad") { }

        protected override ValueTask ProcessFrameAsync(Frame frame, CancellationToken ct)
            => PushFrameAsync(frame, ct);
    }

    // ── STT processor dispatch ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a Voxa <see cref="SpeechToTextProcessor"/> wrapping the right vendor engine for
    /// the recipe's STT config. Voxa's per-vendor static factories (e.g.
    /// <c>OpenAISpeech.StreamingTranscription</c>) hand back the processor pre-wrapped, so
    /// pipeline composition stays a single <c>.Then(stt)</c> regardless of vendor.
    /// </summary>
    private static SpeechToTextProcessor BuildSttProcessor(
        ChainedRecipeRuntimeSpec recipe,
        IVoiceProviderCredentialResolver credentialResolver)
    {
        return recipe.SttConfig switch
        {
            OpenAIWhisperConfig whisper => OpenAISpeech.StreamingTranscription(new OpenAISpeechOptions
            {
                ApiKey = ResolveApiKey(credentialResolver, "OpenAI", recipe.SttProviderDisplayName),
                SttModel = recipe.SttModel ?? whisper.DefaultModel ?? "whisper-1",
                SttLanguage = recipe.SttLanguage ?? whisper.DefaultLanguage,
            }),

            AzureSttConfig azure => AzureSpeech.StreamingTranscription(new AzureSpeechOptions
            {
                SubscriptionKey = ResolveApiKey(credentialResolver, "Azure", recipe.SttProviderDisplayName),
                Region = RequireRegion(azure.Region, recipe.SttProviderDisplayName, "Azure STT"),
                RecognitionLanguage =
                    recipe.SttLanguage
                    ?? azure.DefaultLanguage
                    ?? "en-US",
            }),

            _ => throw new VoiceConfigurationException(
                $"STT provider '{recipe.SttProviderDisplayName}' uses {recipe.SttConfig.GetType().Name}; "
                + "supported chained STT configs are OpenAIWhisperConfig and AzureSttConfig."),
        };
    }

    // ── TTS processor dispatch ───────────────────────────────────────────────────────────

    private TextToSpeechProcessor BuildTtsProcessor(
        ChainedRecipeRuntimeSpec recipe,
        IVoiceProviderCredentialResolver credentialResolver)
    {
        if (string.IsNullOrWhiteSpace(recipe.TtsVoiceId))
        {
            // Belt-and-braces: the recipe service rejects empty TtsVoiceId on save, but a
            // legacy row could still slip through. Fail fast here instead of letting the
            // engine 4xx mid-stream.
            throw new VoiceConfigurationException(
                $"TTS recipe '{recipe.RecipeDisplayName}' is missing a voice id. "
                + "Open the recipe editor and pick a voice for the TTS provider.");
        }

        return recipe.TtsConfig switch
        {
            OpenAITtsConfig openai => OpenAISpeech.Synthesis(new OpenAISpeechOptions
            {
                ApiKey = ResolveApiKey(credentialResolver, "OpenAI", recipe.TtsProviderDisplayName),
                TtsModel = recipe.TtsModelId ?? openai.DefaultModelId ?? "tts-1",
                TtsVoice = recipe.TtsVoiceId,
            }),

            AzureTtsConfig azure => AzureSpeech.Synthesis(new AzureSpeechOptions
            {
                SubscriptionKey = ResolveApiKey(credentialResolver, "Azure", recipe.TtsProviderDisplayName),
                Region = RequireRegion(azure.Region, recipe.TtsProviderDisplayName, "Azure TTS"),
                Voice = recipe.TtsVoiceId,
            }),

            ElevenLabsTtsConfig eleven => ElevenLabs.Synthesis(
                new ElevenLabsOptions
                {
                    ApiKey = ResolveApiKey(credentialResolver, "ElevenLabs", recipe.TtsProviderDisplayName),
                    VoiceId = recipe.TtsVoiceId,
                    ModelId = recipe.TtsModelId ?? eleven.DefaultModelId ?? "eleven_multilingual_v2",
                },
                _httpClientFactory.CreateClient("Voxa.Speech.ElevenLabs")),

            MistralTtsConfig mistral => BuildMistralTtsProcessor(recipe, credentialResolver, mistral),

            _ => throw new VoiceConfigurationException(
                $"TTS provider '{recipe.TtsProviderDisplayName}' uses {recipe.TtsConfig.GetType().Name}; "
                + "supported chained TTS configs are OpenAITtsConfig, AzureTtsConfig, ElevenLabsTtsConfig, MistralTtsConfig."),
        };
    }

    /// <summary>
    /// Build a TTS processor backed by <see cref="AonikMistralVoiceEngine"/> rather than
    /// <c>Voxa.Speech.Mistral.MistralTextToSpeechEngine</c>.
    ///
    /// <para>
    /// Voxa's 0.4.0-alpha Mistral engine reads the response as raw PCM bytes; Mistral
    /// actually returns an SSE event-stream (<c>data: {"audio_data":"&lt;base64&gt;"}</c>
    /// lines, even when <c>response_format=pcm</c>). The mismatch produced the
    /// "horribly garbled, sounds like static" symptom we hit on the live test card —
    /// the SSE wire-format text was being sent to the client as if it were PCM samples.
    /// AonikMistralVoiceEngine parses the SSE stream correctly, mirroring the
    /// approach <c>Aonik.Ai.Providers.MistralTextToSpeechProvider</c> uses for the
    /// chat-speech path (which is known-good in production).
    /// </para>
    ///
    /// <para>
    /// Output rate is hard-coded to 24 kHz because Mistral's <c>response_format=pcm</c>
    /// always returns 24 kHz mono signed 16-bit LE PCM (OpenAI-compatible). This matches
    /// what Voxa's <see cref="MistralSpeechOptions.OutputSampleRate"/> default declared,
    /// so the AudioRawFrame tagging downstream stays consistent.
    /// </para>
    /// </summary>
    private TextToSpeechProcessor BuildMistralTtsProcessor(
        ChainedRecipeRuntimeSpec recipe,
        IVoiceProviderCredentialResolver credentialResolver,
        MistralTtsConfig mistral)
    {
        var apiKey = ResolveApiKey(credentialResolver, "Mistral", recipe.TtsProviderDisplayName);
        var voiceId = recipe.TtsVoiceId;
        var modelId = recipe.TtsModelId ?? mistral.DefaultModelId;
        var httpClient = _httpClientFactory.CreateClient("Aonik.Voice.Mistral");
        var logger = _loggerFactory.CreateLogger("Aonik.Voice.MistralEngine");

        return new TextToSpeechProcessor(
            engineFactory: () => new AonikMistralVoiceEngine(apiKey, voiceId, modelId, httpClient, logger),
            outputSampleRate: 24000);
    }

    // ── Shared helpers ───────────────────────────────────────────────────────────────────

    private static string ResolveApiKey(
        IVoiceProviderCredentialResolver resolver,
        string vendorKey,
        string providerDisplayName)
    {
        var credential = resolver
            .ResolveAsync(vendorKey, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        if (!credential.HasCredential || string.IsNullOrWhiteSpace(credential.ApiKey))
        {
            throw new VoiceConfigurationException(
                $"{vendorKey} credential is not configured for provider '{providerDisplayName}'. "
                + "Set the API key on the provider in the admin UI's Speech Providers tab.");
        }
        return credential.ApiKey!;
    }

    private static string RequireRegion(string? region, string providerDisplayName, string vendorLabel)
    {
        if (string.IsNullOrWhiteSpace(region))
        {
            throw new VoiceConfigurationException(
                $"{vendorLabel} provider '{providerDisplayName}' is missing a Region. "
                + "Edit the provider config and set a region (e.g. 'eastus', 'westeurope').");
        }
        return region;
    }

    /// <summary>
    /// Mirrors <c>PreviewEngineFactory.ResolveMistralModel</c>: the legacy placeholder
    /// "voxtral-tts" is not a real Mistral model id (Mistral's <c>/v1/audio/speech</c>
    /// returns 400). Rewrite on the fly so existing provider rows keep working without a
    /// manual edit.
    /// </summary>
    private static string ResolveMistralModel(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId)) return "voxtral-mini-tts-2603";
        return modelId.Trim().Equals("voxtral-tts", StringComparison.OrdinalIgnoreCase)
            ? "voxtral-mini-tts-2603"
            : modelId;
    }
}

/// <summary>Thrown when the tenant config can't produce a runnable v1 pipeline.</summary>
public sealed class VoiceConfigurationException : Exception
{
    public VoiceConfigurationException(string message) : base(message) { }
    public VoiceConfigurationException(string message, Exception inner) : base(message, inner) { }
}
