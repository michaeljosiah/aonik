using System.Net.WebSockets;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions.Ai;
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
/// Strategy dispatcher that composes the per-connection Voxa pipeline based on
/// <see cref="VoiceProviderConfiguration"/>. v1 implements the chained-OpenAI
/// recipe; other recipes throw with an explicit "not yet wired" message so the
/// admin UI surfaces it.
/// </summary>
public interface IAonikVoicePipelineFactory
{
    Voxa.Pipelines.Pipeline BuildChained(VoicePipelineBuildRequest request);
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
    VoiceProviderConfiguration TenantConfig,
    IServiceProvider RequestServices);

internal sealed class AonikVoicePipelineFactory : IAonikVoicePipelineFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public AonikVoicePipelineFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public Voxa.Pipelines.Pipeline BuildChained(VoicePipelineBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.TenantConfig.Kind != VoiceProviderKind.Chained)
        {
            throw new VoiceConfigurationException(
                $"Voice provider kind '{request.TenantConfig.Kind}' is reserved for v1.1; v1 only supports 'chained'. "
                + "See docs/specifications/022.aonik-voice-realtime.md Phase 7.");
        }

        var chained = request.TenantConfig.Chained
            ?? throw new VoiceConfigurationException(
                "Tenant voice configuration declares 'chained' kind but ChainedVoiceConfiguration is null.");

        var sttVendor = chained.Stt.Vendor?.ToLowerInvariant();
        var ttsVendor = chained.Tts.Vendor?.ToLowerInvariant();

        // v1 ships only the OpenAI recipe end-to-end. The other vendors in the
        // matrix (Azure, ElevenLabs, Mistral) need their own engine wiring; for
        // now they raise a clear error that surfaces as a `voice-not-configured`
        // envelope on the WS.
        if (sttVendor != "openai-whisper" && sttVendor != "openai")
        {
            throw new VoiceConfigurationException(
                $"STT vendor '{chained.Stt.Vendor}' not yet wired in v1. Use 'openai-whisper' or wait for the relevant phase wiring.");
        }
        if (ttsVendor != "openai")
        {
            throw new VoiceConfigurationException(
                $"TTS vendor '{chained.Tts.Vendor}' not yet wired in v1. Use 'openai' or wait for the relevant phase wiring.");
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

        var openAiOptions = new OpenAISpeechOptions
        {
            ApiKey = openAiKey,
            SttModel = chained.Stt.Model ?? "whisper-1",
            TtsModel = chained.Tts.ModelId ?? "tts-1",
            TtsVoice = chained.Tts.VoiceId ?? "alloy",
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

        var sentenceAggregator = new SentenceAggregator();

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
        //   WebSocketAudioSource → STT → AonikVoiceAgent → SentenceAggregator
        //   → SpeechTextNormalizerProcessor → TextToSpeechProcessor → WebSocketAudioSink
        // VAD is not enabled in v1 chained-OpenAI (Whisper handles segmentation
        // server-side); the spec calls VAD optional.
        return Voxa.Pipelines.Pipeline.Build()
            .Source(source)
            .Then(stt)
            .Then(agent)
            .Then(sentenceAggregator)
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
