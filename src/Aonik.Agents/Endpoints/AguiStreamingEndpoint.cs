using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aonik.Agents.Contracts.Agui;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FastEndpoints;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// AG-UI protocol streaming endpoint. Implements the AG-UI SSE protocol as a
/// protocol adapter: parse request → resolve thread + agent → stream agent
/// output as SSE events → enqueue post-stream persistence.
///
/// All non-protocol concerns (thread persistence, agent resolution, message
/// conversion, tool classification, speech rendering, post-stream writes)
/// live in injected services so this file stays focused on AG-UI wire format.
///
/// Protocol: POST with JSON body → SSE response with AG-UI events.
/// Reference: https://docs.ag-ui.com/concepts/events
/// </summary>
internal sealed class AguiStreamingEndpoint : Endpoint<AguiRunInput>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private readonly IChatThreadManager _threadManager;
    private readonly IAgentContextualizer _contextualizer;
    private readonly IAguiMessageConverter _converter;
    private readonly IToolCallClassifier _classifier;
    private readonly ISpeechRenderer _speechRenderer;
    private readonly IPostStreamPersistenceCoordinator _coordinator;
    private readonly IStreamingTextToSpeechService? _streamingTts;
    private readonly ITenantTextToSpeechSettingsService? _ttsSettings;
    private readonly ICurrentUserProvider? _currentUserProvider;
    private readonly ITenantContext? _tenantContext;
    private readonly ICurrentUserContext? _currentUserContext;
    private readonly ILogger<AguiStreamingEndpoint> _logger;

    public AguiStreamingEndpoint(
        IChatThreadManager threadManager,
        IAgentContextualizer contextualizer,
        IAguiMessageConverter converter,
        IToolCallClassifier classifier,
        ISpeechRenderer speechRenderer,
        IPostStreamPersistenceCoordinator coordinator,
        ILogger<AguiStreamingEndpoint> logger,
        IStreamingTextToSpeechService? streamingTts = null,
        ITenantTextToSpeechSettingsService? ttsSettings = null,
        ICurrentUserProvider? currentUserProvider = null,
        ITenantContext? tenantContext = null,
        ICurrentUserContext? currentUserContext = null)
    {
        _threadManager = threadManager;
        _contextualizer = contextualizer;
        _converter = converter;
        _classifier = classifier;
        _speechRenderer = speechRenderer;
        _coordinator = coordinator;
        _logger = logger;
        _streamingTts = streamingTts;
        _ttsSettings = ttsSettings;
        _currentUserProvider = currentUserProvider;
        _tenantContext = tenantContext;
        _currentUserContext = currentUserContext;
    }

    public override void Configure()
    {
        Post("/ai/agui");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Stream AG-UI chat events";
            s.Description = "Implements the AG-UI SSE protocol for real-time agent chat. Routes messages through the master orchestrator and streams responses as AG-UI events.";
        });
        Options(x => x.WithTags("AI Agents"));
    }

    public override async Task HandleAsync(AguiRunInput input, CancellationToken cancellationToken)
    {
        // Voice-mode pre-flight validation MUST happen before any SSE bytes
        // are written. Once SSE has started we can't switch to a JSON 400
        // body — the client is already in event-stream mode.
        var voiceMode = input.VoiceMode;
        var requestedAbstractFormat = voiceMode
            ? (input.AudioFormat ?? AudioFormatNegotiation.DefaultAbstractFormat)
            : null;
        string? providerFormat = null;
        string? abstractFormat = null;
        string? audioMime = null;

        if (voiceMode)
        {
            if (_streamingTts is null || _ttsSettings is null)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                HttpContext.Response.ContentType = "application/json";
                await HttpContext.Response.WriteAsJsonAsync(new
                {
                    code = "voice_mode_unavailable",
                    message = "Voice mode is not supported in this deployment.",
                }, cancellationToken);
                return;
            }

            if (!AudioFormatNegotiation.IsKnownAbstractFormat(requestedAbstractFormat))
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                HttpContext.Response.ContentType = "application/json";
                await HttpContext.Response.WriteAsJsonAsync(new
                {
                    code = "invalid_audio_format",
                    message = $"Unsupported audioFormat '{input.AudioFormat}'. Use one of: mp3, opus, wav.",
                }, cancellationToken);
                return;
            }

            // Ask the tenant settings layer for the configured TTS provider so
            // we can validate the abstract → provider format mapping before
            // committing to SSE.
            var settings = await _ttsSettings.GetCurrentAsync(cancellationToken);
            if (!settings.Enabled)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                HttpContext.Response.ContentType = "application/json";
                await HttpContext.Response.WriteAsJsonAsync(new
                {
                    code = "voice_mode_disabled",
                    message = "Text-to-speech is disabled for this tenant; voice mode is unavailable.",
                }, cancellationToken);
                return;
            }

            providerFormat = AudioFormatNegotiation.MapToProviderFormat(settings.DefaultProfile.Provider, requestedAbstractFormat!);
            if (providerFormat is null)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                HttpContext.Response.ContentType = "application/json";
                await HttpContext.Response.WriteAsJsonAsync(new
                {
                    code = "unsupported_audio_format",
                    message = $"Provider '{settings.DefaultProfile.Provider}' does not support audioFormat '{requestedAbstractFormat}' for voice-mode AGUI.",
                }, cancellationToken);
                return;
            }

            abstractFormat = requestedAbstractFormat;
            audioMime = AudioFormatNegotiation.MapAbstractToMime(requestedAbstractFormat!);
        }

        using var chatActivity = AiTelemetry.ActivitySource.StartActivity("aonik.chat.agui", ActivityKind.Internal);
        // Stamp a semantic use_case on the chat-level activity so the trace
        // listing's representative row shows "voice" / "chat" instead of
        // the run_id hash or — worse — the use_case of an ancillary call
        // (e.g. "title-generation") winning the dedupe.
        chatActivity?.SetTag(AiTelemetry.UseCaseAttribute, voiceMode ? "voice" : "chat");
        if (voiceMode)
        {
            chatActivity?.SetTag("aonik.chat.audio_format", abstractFormat);
        }

        var response = HttpContext.Response;
        var runId = input.RunId ?? Guid.NewGuid().ToString("N");
        var requestStopwatch = Stopwatch.StartNew();
        await using var writer = new AguiResponseWriter(response, voiceMode, requestStopwatch);
        await using VoiceSynthCoordinator? voiceCoordinator = voiceMode
            ? new VoiceSynthCoordinator(
                streamingTts: _streamingTts!,
                writer: writer,
                providerFormat: providerFormat!,
                mime: audioMime!,
                logger: _logger)
            : null;
        var assistantTextBuilder = new System.Text.StringBuilder();
        long inputTokens = 0;
        long outputTokens = 0;
        long? timeToFirstTokenMs = null;
        long? requestToRunStartedSseMs = null;
        long? requestToAgentReadyMs = null;
        long? requestToLlmStartMs = null;
        long? requestToFirstTokenSseMs = null;
        long? userBriefDurationMs = null;
        var userBriefCacheStatus = "skipped";
        var historySource = "client";
        long historyDurationMs = 0;
        var historyMessageCount = input.Messages?.Count ?? 0;
        var clientToolCount = 0;
        var isThinClient = input.Messages is { Count: 1 }
            && string.Equals(input.Messages[0].Role, "user", StringComparison.OrdinalIgnoreCase);
        var persistenceQueued = false;
        var outcome = "success";

        chatActivity?.SetTag("aonik.chat.run_id", runId);
        chatActivity?.SetTag("aonik.agent.name", input.AgentId ?? "orchestrator");
        chatActivity?.SetTag("aonik.chat.is_thin_client", isThinClient);
        chatActivity?.SetTag("aonik.chat.input_message_count", historyMessageCount);
        chatActivity?.SetBaggage("aonik.chat.run_id", runId);
        chatActivity?.SetBaggage("aonik.agent.name", input.AgentId ?? "orchestrator");

        // Resolve / create the persisted thread before anything else — the
        // thread GUID is what we stamp onto OTel baggage and SSE events.
        var threadCtx = await _threadManager.EnsureThreadAsync(
            input.ThreadId, input.Messages, input.AgentId, cancellationToken);
        var threadId = threadCtx.ThreadIdString;
        var requestToThreadReadyMs = requestStopwatch.ElapsedMilliseconds;

        chatActivity?.SetTag("aonik.chat.thread_id", threadId);
        chatActivity?.SetTag("aonik.chat.is_new_thread", threadCtx.IsNewThread);
        chatActivity?.SetBaggage("aonik.chat.thread_id", threadId);

        // Propagate session (threadId) and user identifiers as OTel baggage +
        // span attributes so the BaggageSpanProcessor copies them to all child
        // spans, enabling Langfuse session grouping and user attribution.
        var activity = Activity.Current;
        if (activity is not null)
        {
            activity.SetBaggage(AiTelemetry.SessionIdAttribute, threadId);
            activity.SetTag(AiTelemetry.SessionIdAttribute, threadId);

            if (_currentUserProvider is not null
                && _currentUserProvider.TryGetCurrentUserId(out var userId))
            {
                var userIdStr = userId.ToString();
                activity.SetBaggage(AiTelemetry.UserIdAttribute, userIdStr);
                activity.SetTag(AiTelemetry.UserIdAttribute, userIdStr);
            }
        }

        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache,no-store";
        response.Headers["Pragma"] = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no";

        var agentContextTask = _contextualizer.ResolveAsync(input.AgentId, cancellationToken);
        var historyTask = _threadManager.ReconstructHistoryAsync(
            threadCtx.PersistedThreadId, input.Messages, cancellationToken);

        try
        {
            await response.StartAsync(cancellationToken);
            await WriteSseEventAsync(writer, new
            {
                type = "RUN_STARTED",
                threadId,
                runId,
            }, cancellationToken);
            requestToRunStartedSseMs = requestStopwatch.ElapsedMilliseconds;

            var agentContext = await agentContextTask;
            var agent = agentContext.Agent;
            requestToAgentReadyMs = requestStopwatch.ElapsedMilliseconds;
            userBriefCacheStatus = agentContext.UserBriefCacheStatus;
            userBriefDurationMs = agentContext.UserBriefDurationMs;

            chatActivity?.SetTag("aonik.user_brief.cache_status", userBriefCacheStatus);
            if (userBriefDurationMs.HasValue)
                chatActivity?.SetTag("aonik.user_brief.duration_ms", userBriefDurationMs.Value);

            var historyResolution = await historyTask;
            var effectiveMessages = historyResolution.Messages;
            historySource = historyResolution.Source;
            historyDurationMs = historyResolution.DurationMs;
            historyMessageCount = effectiveMessages?.Count ?? 0;

            chatActivity?.SetTag("aonik.chat.history_source", historySource);
            chatActivity?.SetTag("aonik.chat.history_duration_ms", historyDurationMs);
            chatActivity?.SetTag("aonik.chat.history_message_count", historyMessageCount);

            var chatMessages = _converter.ConvertMessages(effectiveMessages);
            if (agentContext.UserBriefPreamble is not null)
                chatMessages = [agentContext.UserBriefPreamble, .. chatMessages];

            // Client-side tool declarations — the LLM sees them so it can emit
            // FunctionCallContent, but the frontend is responsible for execution.
            var clientTools = _converter.ConvertClientTools(input.Tools);
            clientToolCount = clientTools.Count;
            chatActivity?.SetTag("aonik.chat.client_tool_count", clientToolCount);

            ChatClientAgentRunOptions? runOptions = null;
            if (clientTools.Count > 0)
            {
                runOptions = new ChatClientAgentRunOptions
                {
                    ChatOptions = new ChatOptions
                    {
                        Tools = clientTools,
                    },
                };
                _logger.LogDebug(
                    "AG-UI run {RunId}: passing {ToolCount} client tool(s) to agent: {ToolNames}",
                    runId, clientTools.Count,
                    string.Join(", ", clientTools.Select(t => t.Name)));
            }

            var messageId = Guid.NewGuid().ToString("N");
            var messageStarted = false;
            var requiresVisualAttention = false;
            var requiresApproval = false;
            var speechBuffer = new SpeechStreamBuffer();

            requestToLlmStartMs = requestStopwatch.ElapsedMilliseconds;
            chatActivity?.AddEvent(new ActivityEvent(
                "aonik.chat.llm_start",
                tags: new ActivityTagsCollection
                {
                    ["elapsed_ms"] = requestToLlmStartMs.Value,
                    ["message_count"] = chatMessages.Count,
                }));

            await foreach (var update in agent.RunStreamingAsync(
                chatMessages, session: null, options: runOptions, cancellationToken: cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested) break;

                var chatUpdate = update.AsChatResponseUpdate();
                if (chatUpdate is null) continue;

                foreach (var content in chatUpdate.Contents ?? [])
                {
                    switch (content)
                    {
                        case TextContent textContent when !string.IsNullOrEmpty(textContent.Text):
                            if (!messageStarted)
                            {
                                timeToFirstTokenMs ??= requestStopwatch.ElapsedMilliseconds;
                                chatActivity?.SetTag("aonik.chat.time_to_first_token_ms", timeToFirstTokenMs.Value);
                                chatActivity?.AddEvent(new ActivityEvent(
                                    "aonik.chat.first_token",
                                    tags: new ActivityTagsCollection
                                    {
                                        ["elapsed_ms"] = timeToFirstTokenMs.Value,
                                    }));
                                await WriteSseEventAsync(writer, new
                                {
                                    type = "TEXT_MESSAGE_START",
                                    messageId,
                                    role = "assistant",
                                }, cancellationToken);
                                messageStarted = true;
                            }

                            assistantTextBuilder.Append(textContent.Text);
                            speechBuffer.Append(textContent.Text);

                            await WriteSseEventAsync(writer, new
                            {
                                type = "TEXT_MESSAGE_CONTENT",
                                messageId,
                                delta = textContent.Text,
                            }, cancellationToken);
                            requestToFirstTokenSseMs ??= requestStopwatch.ElapsedMilliseconds;

                            while (speechBuffer.TryPopSentence(out var rawChunk))
                            {
                                await EmitSpeechChunkAsync(
                                    writer, voiceCoordinator, threadId, messageId, speechBuffer.NextChunkIndex - 1,
                                    rawChunk, isFinal: false, cancellationToken);
                            }
                            break;

                        case FunctionCallContent functionCall:
                            var toolCallId = _classifier.ResolveCallId(functionCall);
                            var toolName = functionCall.Name ?? string.Empty;
                            requiresVisualAttention |= _classifier.IsDisplay(toolName);
                            requiresApproval |= _classifier.RequiresApproval(toolName);

                            await WriteSseEventAsync(writer, new
                            {
                                type = "TOOL_CALL_START",
                                toolCallId,
                                toolCallName = functionCall.Name,
                                parentMessageId = messageId,
                            }, cancellationToken);

                            if (functionCall.Arguments is { Count: > 0 })
                            {
                                var argsJson = JsonSerializer.Serialize(
                                    functionCall.Arguments, JsonOptions);
                                await WriteSseEventAsync(writer, new
                                {
                                    type = "TOOL_CALL_ARGS",
                                    toolCallId,
                                    delta = argsJson,
                                }, cancellationToken);
                            }

                            await WriteSseEventAsync(writer, new
                            {
                                type = "TOOL_CALL_END",
                                toolCallId,
                            }, cancellationToken);
                            break;

                        case FunctionResultContent functionResult:
                            await WriteSseEventAsync(writer, new
                            {
                                type = "TOOL_CALL_RESULT",
                                messageId = Guid.NewGuid().ToString("N"),
                                toolCallId = functionResult.CallId,
                                content = functionResult.Result?.ToString(),
                                role = "tool",
                            }, cancellationToken);
                            break;

                        case TextReasoningContent reasoningContent
                            when !string.IsNullOrEmpty(reasoningContent.Text):

                            await WriteSseEventAsync(writer, new
                            {
                                type = "REASONING_MESSAGE_CONTENT",
                                messageId,
                                delta = reasoningContent.Text,
                            }, cancellationToken);
                            break;

                        case UsageContent usageContent:
                            inputTokens += usageContent.Details.InputTokenCount ?? 0;
                            outputTokens += usageContent.Details.OutputTokenCount ?? 0;
                            break;
                    }
                }
            }

            var assistantText = assistantTextBuilder.ToString();

            if (messageStarted)
            {
                await WriteSseEventAsync(writer, new
                {
                    type = "TEXT_MESSAGE_END",
                    messageId,
                }, cancellationToken);
            }

            var tailChunk = speechBuffer.FlushRemaining();
            if (tailChunk is not null)
            {
                await EmitSpeechChunkAsync(
                    writer, voiceCoordinator, threadId, messageId, speechBuffer.NextChunkIndex - 1,
                    tailChunk, isFinal: true, cancellationToken);
            }

            var guidanceText = _speechRenderer.RenderGuidance(requiresVisualAttention, requiresApproval);
            await WriteSseEventAsync(writer, new
            {
                type = "CUSTOM",
                name = "speech.render",
                value = new
                {
                    messageId,
                    speechText = guidanceText,
                    requiresVisualAttention,
                    requiresApproval,
                    isFinal = true,
                }
            }, cancellationToken);

            // Voice-mode drain contract — RUN_FINISHED must come AFTER
            // every audio frame has been flushed to the wire. Wait first
            // for synth workers to finish enqueueing frames, then close
            // the audio channel, then wait for the writer's pump to
            // complete. Per the design refinement: "drained" means
            // writer-flushed, not synth-completed.
            if (voiceCoordinator is not null)
            {
                await voiceCoordinator.WaitForAllSynthesisAsync();
                writer.CompleteAudioInput();
                await writer.WaitForAudioDrainAsync();
            }

            requestStopwatch.Stop();
            var audioMetrics = writer.GetAudioMetrics();

            object metrics = audioMetrics.VoiceMode
                ? new
                {
                    inputTokens,
                    outputTokens,
                    totalTokens = inputTokens + outputTokens,
                    latencyMs = requestStopwatch.ElapsedMilliseconds,
                    timeToFirstTokenMs = timeToFirstTokenMs ?? requestStopwatch.ElapsedMilliseconds,
                    audioBytes = audioMetrics.AudioBytes,
                    audioFrames = audioMetrics.AudioFrames,
                    audioFramesDropped = audioMetrics.AudioFramesDropped,
                    ttsCacheHits = audioMetrics.TtsCacheHits,
                    ttsCacheMisses = audioMetrics.TtsCacheMisses,
                    firstAudibleByteMs = audioMetrics.FirstAudibleByteMs,
                    audioDrainMs = audioMetrics.AudioDrainMs,
                }
                : new
                {
                    inputTokens,
                    outputTokens,
                    totalTokens = inputTokens + outputTokens,
                    latencyMs = requestStopwatch.ElapsedMilliseconds,
                    timeToFirstTokenMs = timeToFirstTokenMs ?? requestStopwatch.ElapsedMilliseconds,
                };

            if (audioMetrics.VoiceMode)
            {
                chatActivity?.SetTag("aonik.chat.audio_bytes", audioMetrics.AudioBytes);
                chatActivity?.SetTag("aonik.chat.audio_frames", audioMetrics.AudioFrames);
                chatActivity?.SetTag("aonik.chat.audio_frames_dropped", audioMetrics.AudioFramesDropped);
                chatActivity?.SetTag("aonik.chat.tts_cache_hits", audioMetrics.TtsCacheHits);
                chatActivity?.SetTag("aonik.chat.tts_cache_misses", audioMetrics.TtsCacheMisses);
                if (audioMetrics.FirstAudibleByteMs.HasValue)
                    chatActivity?.SetTag("aonik.chat.first_audible_byte_ms", audioMetrics.FirstAudibleByteMs.Value);
                if (audioMetrics.AudioDrainMs.HasValue)
                    chatActivity?.SetTag("aonik.chat.audio_drain_ms", audioMetrics.AudioDrainMs.Value);
            }

            _logger.LogInformation(
                "AguiRunCompleted: RunId={RunId} AgentName={AgentName} ThreadId={ThreadId} LatencyMs={LatencyMs} TtftMs={TtftMs} InputTokens={InputTokens} OutputTokens={OutputTokens} TotalTokens={TotalTokens} VoiceMode={VoiceMode} AudioBytes={AudioBytes} AudioFrames={AudioFrames} AudioFramesDropped={AudioFramesDropped} FirstAudibleByteMs={FirstAudibleByteMs} AudioDrainMs={AudioDrainMs}",
                runId, input.AgentId ?? "orchestrator", threadId,
                requestStopwatch.ElapsedMilliseconds, timeToFirstTokenMs ?? requestStopwatch.ElapsedMilliseconds,
                inputTokens, outputTokens, inputTokens + outputTokens,
                audioMetrics.VoiceMode, audioMetrics.AudioBytes, audioMetrics.AudioFrames,
                audioMetrics.AudioFramesDropped, audioMetrics.FirstAudibleByteMs, audioMetrics.AudioDrainMs);

            await WriteSseEventAsync(writer, new
            {
                type = "RUN_FINISHED",
                threadId,
                runId,
                metrics,
            }, cancellationToken);

            // Flush + complete the response now. Without CompleteAsync, ACA's
            // Envoy ingress holds the chunked transfer-encoding open until
            // this handler returns — so any post-stream work would add to the
            // wire latency the client sees. The coordinator below picks up
            // persistence on a detached scope.
            await response.Body.FlushAsync(CancellationToken.None);
            try
            {
                await response.CompleteAsync();
            }
            catch (Exception completeEx)
            {
                _logger.LogDebug(completeEx,
                    "AG-UI Response.CompleteAsync threw for thread {ThreadId} — continuing with persistence",
                    threadId);
            }

            // Capture tenant/user from the request scope so the coordinator
            // can re-seed them in its background scope.
            QueuePostStreamPersistence(assistantText, requestStopwatch.ElapsedMilliseconds);
            persistenceQueued = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            outcome = "cancelled";
            chatActivity?.SetStatus(ActivityStatusCode.Unset, "cancelled");
            _logger.LogDebug("AG-UI stream cancelled for thread {ThreadId}", threadId);
        }
        catch (Exception ex)
        {
            outcome = "error";
            AiTelemetry.MarkError(chatActivity, ex);
            _logger.LogError(ex, "AG-UI streaming error for thread {ThreadId}, run {RunId}", threadId, runId);

            try
            {
                await WriteSseEventAsync(writer, new
                {
                    type = "RUN_ERROR",
                    message = ex.Message,
                    code = "INTERNAL_ERROR",
                }, CancellationToken.None);
            }
            catch
            {
                // If we can't write the error event, the connection is already broken.
            }
        }

        if (!persistenceQueued && threadCtx.PersistedThreadId.HasValue && !string.IsNullOrEmpty(threadCtx.FirstUserMessage))
        {
            QueuePostStreamPersistence(assistantTextBuilder.ToString(), requestStopwatch.ElapsedMilliseconds);
        }

        if (requestStopwatch.IsRunning)
            requestStopwatch.Stop();

        chatActivity?.SetTag("aonik.chat.outcome", outcome);
        chatActivity?.SetTag("aonik.chat.total_duration_ms", requestStopwatch.ElapsedMilliseconds);
        if (requestToRunStartedSseMs.HasValue)
            chatActivity?.SetTag("aonik.chat.request_to_run_started_sse_ms", requestToRunStartedSseMs.Value);
        if (requestToAgentReadyMs.HasValue)
            chatActivity?.SetTag("aonik.chat.request_to_agent_ready_ms", requestToAgentReadyMs.Value);
        if (requestToLlmStartMs.HasValue)
            chatActivity?.SetTag("aonik.chat.request_to_llm_start_ms", requestToLlmStartMs.Value);
        if (requestToFirstTokenSseMs.HasValue)
            chatActivity?.SetTag("aonik.chat.request_to_first_token_sse_ms", requestToFirstTokenSseMs.Value);

        _logger.LogInformation(
            "AguiRunPhases: RunId={RunId} AgentName={AgentName} ThreadId={ThreadId} Outcome={Outcome} RequestToThreadReadyMs={RequestToThreadReadyMs} RequestToRunStartedSseMs={RequestToRunStartedSseMs} RequestToAgentReadyMs={RequestToAgentReadyMs} RequestToLlmStartMs={RequestToLlmStartMs} RequestToFirstTokenMs={RequestToFirstTokenMs} RequestToFirstTokenSseMs={RequestToFirstTokenSseMs} UserBriefDurationMs={UserBriefDurationMs} UserBriefCacheStatus={UserBriefCacheStatus} HistoryDurationMs={HistoryDurationMs} HistorySource={HistorySource} HistoryMessageCount={HistoryMessageCount} IsNewThread={IsNewThread} IsThinClient={IsThinClient} HasUserBrief={HasUserBrief} ClientToolCount={ClientToolCount}",
            runId,
            input.AgentId ?? "orchestrator",
            threadId,
            outcome,
            requestToThreadReadyMs,
            requestToRunStartedSseMs,
            requestToAgentReadyMs,
            requestToLlmStartMs,
            timeToFirstTokenMs,
            requestToFirstTokenSseMs,
            userBriefDurationMs,
            userBriefCacheStatus,
            historyDurationMs,
            historySource,
            historyMessageCount,
            threadCtx.IsNewThread,
            isThinClient,
            userBriefCacheStatus is "hit" or "miss",
            clientToolCount);

        // Best-effort final flush. In the success path the response has
        // already been completed via CompleteAsync and this will throw —
        // that's fine, the bytes are already on the wire.
        try
        {
            await response.Body.FlushAsync(CancellationToken.None);
        }
        catch
        {
            // Response already completed / connection already closed.
        }

        void QueuePostStreamPersistence(string assistantText, long latencyMs)
        {
            Guid? capturedTenantId = _tenantContext?.TenantId;
            Guid? capturedUserId = _currentUserContext?.UserId;

            _coordinator.Enqueue(new PostStreamPersistenceContext(
                PersistedThreadId: threadCtx.PersistedThreadId,
                TenantId: capturedTenantId,
                UserId: capturedUserId,
                AssistantText: assistantText,
                AgentId: input.AgentId,
                InputTokens: inputTokens,
                OutputTokens: outputTokens,
                LatencyMs: latencyMs,
                IsNewThread: threadCtx.IsNewThread,
                FirstUserMessage: threadCtx.FirstUserMessage,
                ThreadIdString: threadId,
                RunId: runId));
        }
    }

    /// <summary>
    /// Writes a single SSE event as a <c>data: {json}\n\n</c> line.
    /// Routes through <see cref="AguiResponseWriter.WriteControlAsync"/>
    /// so audio frames in voice mode never preempt control writes.
    /// </summary>
    private static Task WriteSseEventAsync<T>(
        AguiResponseWriter writer,
        T eventData,
        CancellationToken cancellationToken) =>
        writer.WriteControlAsync(eventData, cancellationToken);

    private async Task EmitSpeechChunkAsync(
        AguiResponseWriter writer,
        VoiceSynthCoordinator? voiceCoordinator,
        string threadId,
        string messageId,
        int chunkIndex,
        string rawChunk,
        bool isFinal,
        CancellationToken cancellationToken)
    {
        var chunkText = _speechRenderer.RenderChunk(rawChunk);
        if (string.IsNullOrWhiteSpace(chunkText))
            return;

        await WriteSseEventAsync(writer, new
        {
            type = "CUSTOM",
            name = "speech.chunk",
            value = new
            {
                messageId,
                chunkIndex,
                speechText = chunkText,
                isFinal,
            },
        }, cancellationToken);

        // Voice mode fans out per-chunk synthesis; audio frames are
        // enqueued through the prioritised writer's audio channel and
        // flushed by its background pump while the LLM continues to
        // emit later text deltas.
        voiceCoordinator?.StartChunkSynthesis(messageId, chunkIndex, chunkText, threadId, cancellationToken);
    }
}
