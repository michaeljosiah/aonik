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

    public AonikVoicePipelineFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public Voxa.Pipelines.Pipeline BuildChained(VoicePipelineBuildRequest request, ChainedRecipeRuntimeSpec recipe)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(recipe);

        // v1 ships only OpenAI Whisper + OpenAI TTS end-to-end. Other configs (Azure,
        // ElevenLabs, Mistral) need engine wiring; for now each unsupported config raises
        // a clear error that surfaces as a `voice-config-invalid` envelope on the WS.
        if (recipe.SttConfig is not OpenAIWhisperConfig whisperConfig)
        {
            throw new VoiceConfigurationException(
                $"STT provider '{recipe.SttProviderDisplayName}' uses {recipe.SttConfig.GetType().Name}; "
                + "only OpenAIWhisperConfig is wired in v1. Pick a provider that uses OpenAI Whisper or wait for the relevant phase wiring.");
        }
        if (recipe.TtsConfig is not OpenAITtsConfig openaiTtsConfig)
        {
            throw new VoiceConfigurationException(
                $"TTS provider '{recipe.TtsProviderDisplayName}' uses {recipe.TtsConfig.GetType().Name}; "
                + "only OpenAITtsConfig is wired in v1. Pick a provider that uses OpenAI TTS or wait for the relevant phase wiring.");
        }

        // Resolve via the platform credential resolver (tenant override → host default
        // → configuration fallback) instead of reading IConfiguration directly. This
        // keeps secrets out of the pipeline factory's constructor surface and lets
        // tenants configure their own keys through the admin UI.
        var credentialResolver = request.RequestServices
            .GetRequiredService<Aonik.SharedKernel.Abstractions.Ai.IVoiceProviderCredentialResolver>();
        var credential = credentialResolver
            .ResolveAsync("OpenAI", CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        if (!credential.HasCredential || string.IsNullOrWhiteSpace(credential.ApiKey))
        {
            throw new VoiceConfigurationException(
                "OpenAI voice credential not configured for this tenant. Configure it in admin Voice & Speech settings.");
        }
        var openAiKey = credential.ApiKey!;

        // Voice and model selection now lives on the recipe (post-spec-024 refactor) — fall
        // back to the provider's vendor-level defaults when the recipe didn't override.
        var openAiOptions = new OpenAISpeechOptions
        {
            ApiKey = openAiKey,
            SttModel = recipe.SttModel ?? whisperConfig.DefaultModel ?? "whisper-1",
            TtsModel = recipe.TtsModelId ?? openaiTtsConfig.DefaultModelId ?? "tts-1",
            TtsVoice = recipe.TtsVoiceId,
        };

        var source = new WebSocketAudioSource(request.WebSocket);
        // Voxa's WebSocketAudioSink + custom-serializer hook covers the AONIK threadReady
        // envelope. No subclass needed; the local AonikVoiceWebSocketSink was retired in
        // Phase B.
        var sink = new WebSocketAudioSink(
            request.WebSocket,
            customSerializer: ThreadReadyFrameSerializer.Serialize);

        var stt = OpenAISpeech.StreamingTranscription(openAiOptions);
        var tts = OpenAISpeech.Synthesis(openAiOptions);

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

}

/// <summary>Thrown when the tenant config can't produce a runnable v1 pipeline.</summary>
public sealed class VoiceConfigurationException : Exception
{
    public VoiceConfigurationException(string message) : base(message) { }
    public VoiceConfigurationException(string message, Exception inner) : base(message, inner) { }
}
