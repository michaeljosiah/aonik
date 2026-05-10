using System.Net.WebSockets;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Ai.Speech;
using Aonik.Voice.Frames;
using Aonik.Voice.Processors;
using Aonik.Voice.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Voxa.Pipelines;
using Voxa.Speech;
using Voxa.Speech.Azure;
using Voxa.Speech.ElevenLabs;
using Voxa.Speech.Mistral;
using Voxa.Speech.OpenAI;
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
    bool UseSentenceAggregator);

internal sealed class AonikVoicePipelineFactory : IAonikVoicePipelineFactory
{
    private readonly ILoggerFactory _loggerFactory;
    // Voxa's ElevenLabs + Mistral factories take an HttpClient since they hit a REST endpoint
    // per synthesis frame. IHttpClientFactory is always available in the AspNetCore host —
    // we use it so DNS rotation, pooling, and SocketsHttpHandler timers behave correctly
    // across long-running voice connections.
    private readonly IHttpClientFactory _httpClientFactory;

    public AonikVoicePipelineFactory(ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory)
    {
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
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

        var source = new WebSocketAudioSource(request.WebSocket);
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

        // Pipeline order matches the spec:
        //   WebSocketAudioSource → STT → AonikVoiceAgent → [SentenceAggregator?]
        //   → SpeechTextNormalizerProcessor → TextToSpeechProcessor → WebSocketAudioSink
        // VAD is not enabled in v1 chained-OpenAI (Whisper handles segmentation
        // server-side); the spec calls VAD optional. The sentence aggregator is gated by
        // the recipe's UseSentenceAggregator knob — disabling it lets tokens flow through
        // unbuffered (useful for snappier echoback agents).
        var builder = Voxa.Pipelines.Pipeline.Build()
            .Source(source)
            .Then(stt)
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

            MistralTtsConfig mistral => Mistral.Synthesis(
                new MistralSpeechOptions
                {
                    ApiKey = ResolveApiKey(credentialResolver, "Mistral", recipe.TtsProviderDisplayName),
                    Voice = recipe.TtsVoiceId,
                    // Same on-the-fly rewrite as PreviewEngineFactory: stale "voxtral-tts"
                    // rows map to the production model id so admins don't have to bulk-edit.
                    Model = ResolveMistralModel(recipe.TtsModelId ?? mistral.DefaultModelId),
                },
                _httpClientFactory.CreateClient("Voxa.Speech.Mistral")),

            _ => throw new VoiceConfigurationException(
                $"TTS provider '{recipe.TtsProviderDisplayName}' uses {recipe.TtsConfig.GetType().Name}; "
                + "supported chained TTS configs are OpenAITtsConfig, AzureTtsConfig, ElevenLabsTtsConfig, MistralTtsConfig."),
        };
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
