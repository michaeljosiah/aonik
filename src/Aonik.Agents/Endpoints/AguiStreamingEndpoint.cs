using System.Diagnostics;

using Aonik.Agents.Contracts.Agui;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using FastEndpoints;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// AG-UI protocol streaming endpoint. The endpoint is intentionally a thin
/// orchestrator: it composes already-extracted services (thread management,
/// agent resolution, voice-mode validation, run-options building, the
/// SSE stream pipeline, post-stream persistence) and is responsible only
/// for HTTP concerns — SSE response headers, RUN_STARTED / RUN_FINISHED
/// framing, error envelopes, and capturing tenant + user identity for
/// background persistence.
///
/// Protocol: POST with JSON body → SSE response with AG-UI events.
/// Reference: https://docs.ag-ui.com/concepts/events
/// </summary>
internal sealed class AguiStreamingEndpoint : Endpoint<AguiRunInput>
{
    private readonly IChatThreadManager _threadManager;
    private readonly IAgentContextualizer _contextualizer;
    private readonly IAguiMessageConverter _converter;
    private readonly IAguiVoiceModeValidator _voiceModeValidator;
    private readonly IAguiRunOptionsBuilder _runOptionsBuilder;
    private readonly IAguiStreamPipeline _streamPipeline;
    private readonly IPostStreamPersistenceCoordinator _coordinator;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<AguiStreamingEndpoint> _logger;
    private readonly ICurrentUserProvider? _currentUserProvider;
    private readonly ITenantContext? _tenantContext;
    private readonly ICurrentUserContext? _currentUserContext;

    public AguiStreamingEndpoint(
        IChatThreadManager threadManager,
        IAgentContextualizer contextualizer,
        IAguiMessageConverter converter,
        IAguiVoiceModeValidator voiceModeValidator,
        IAguiRunOptionsBuilder runOptionsBuilder,
        IAguiStreamPipeline streamPipeline,
        IPostStreamPersistenceCoordinator coordinator,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<AguiStreamingEndpoint> logger,
        ICurrentUserProvider? currentUserProvider = null,
        ITenantContext? tenantContext = null,
        ICurrentUserContext? currentUserContext = null)
    {
        _threadManager = threadManager;
        _contextualizer = contextualizer;
        _converter = converter;
        _voiceModeValidator = voiceModeValidator;
        _runOptionsBuilder = runOptionsBuilder;
        _streamPipeline = streamPipeline;
        _coordinator = coordinator;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
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
        // Voice-mode pre-flight MUST happen before any SSE bytes are written.
        // Once SSE has started we can't switch to a JSON 400 — the client is
        // already in event-stream mode.
        var voiceModeResult = await _voiceModeValidator.ValidateAsync(input, cancellationToken);
        if (!voiceModeResult.IsSuccess)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            HttpContext.Response.ContentType = "application/json";
            await HttpContext.Response.WriteAsJsonAsync(new
            {
                code = voiceModeResult.Code,
                message = voiceModeResult.Message,
            }, cancellationToken);
            return;
        }

        var voiceMode = input.VoiceMode;
        var voiceContext = voiceModeResult.Context;

        using var chatActivity = AiTelemetry.ActivitySource.StartActivity("aonik.chat.agui", ActivityKind.Internal);
        // Stamp a semantic use_case on the chat-level activity so the trace
        // listing's representative row shows "voice" / "chat" instead of
        // the run_id hash or — worse — the use_case of an ancillary call
        // (e.g. "title-generation") winning the dedupe.
        chatActivity?.SetTag(AiTelemetry.UseCaseAttribute, voiceMode ? "voice" : "chat");
        if (voiceMode && voiceContext is not null)
        {
            chatActivity?.SetTag("aonik.chat.audio_format", voiceContext.AbstractFormat);
        }

        var response = HttpContext.Response;
        var runId = input.RunId ?? Guid.NewGuid().ToString("N");
        var requestStopwatch = Stopwatch.StartNew();
        await using var writer = new AguiResponseWriter(response, voiceMode, requestStopwatch);
        // Capture tenant + user identity from the request scope NOW so each
        // per-chunk scope created inside VoiceSynthCoordinator can re-seed
        // its own ITenantContext / ICurrentUserContext. Without this, fresh
        // scopes get empty context — the kill-switch cache key collides
        // across all chunks (Guid.Empty), and tenant query filters on the
        // AiDbContext don't apply correctly.
        var coordinatorTenantId = _tenantContext?.TenantId;
        var coordinatorUserId = _currentUserContext?.UserId;
        await using VoiceSynthCoordinator? voiceCoordinator = voiceMode && voiceContext is not null
            ? new VoiceSynthCoordinator(
                serviceScopeFactory: _serviceScopeFactory,
                writer: writer,
                providerFormat: voiceContext.ProviderFormat,
                mime: voiceContext.AudioMime,
                logger: _logger,
                capturedTenantId: coordinatorTenantId,
                capturedUserId: coordinatorUserId)
            : null;

        long? requestToRunStartedSseMs = null;
        long? requestToAgentReadyMs = null;
        long? requestToLlmStartMs = null;
        var userBriefCacheStatus = "skipped";
        long? userBriefDurationMs = null;
        var historySource = "client";
        long historyDurationMs = 0;
        var historyMessageCount = input.Messages?.Count ?? 0;
        var clientToolCount = 0;
        var isThinClient = input.Messages is { Count: 1 }
            && string.Equals(input.Messages[0].Role, "user", StringComparison.OrdinalIgnoreCase);
        var persistenceQueued = false;
        var outcome = "success";

        // Stream-pipeline-discovered values; defaults so the post-handle
        // logging block always has a consistent picture even when an
        // exception aborts the stream mid-flight.
        long inputTokens = 0;
        long outputTokens = 0;
        long? timeToFirstTokenMs = null;
        long? requestToFirstTokenSseMs = null;
        var assistantText = string.Empty;

        chatActivity?.SetTag("aonik.chat.run_id", runId);
        chatActivity?.SetTag("aonik.agent.name", input.AgentId ?? "orchestrator");
        chatActivity?.SetTag("aonik.chat.is_thin_client", isThinClient);
        chatActivity?.SetTag("aonik.chat.input_message_count", historyMessageCount);
        chatActivity?.SetBaggage("aonik.chat.run_id", runId);
        chatActivity?.SetBaggage("aonik.agent.name", input.AgentId ?? "orchestrator");

        // Surface the user's most recent message on the chat activity so
        // voice traces show the transcribed prompt directly in the trace
        // explorer (otherwise the only place to find it is the raw
        // Application Insights customDimensions JSON, which the trace
        // explorer doesn't render). Truncated to 1 KB to keep span
        // payloads bounded; long pasted prompts get clipped with an
        // ellipsis indicator.
        var firstUserMessage = ExtractLatestUserMessage(input.Messages);
        if (!string.IsNullOrEmpty(firstUserMessage))
        {
            chatActivity?.SetTag("aonik.chat.user_prompt", firstUserMessage);
        }

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
            await writer.WriteControlAsync(new
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

            var configuredModel = agentContext.ConfiguredModelName;
            var runOptions = _runOptionsBuilder.Build(clientTools, configuredModel);

            chatActivity?.SetTag("aonik.chat.configured_model", configuredModel ?? "<global default>");

            if (clientTools.Count > 0)
            {
                _logger.LogDebug(
                    "AG-UI run {RunId}: passing {ToolCount} client tool(s) to agent: {ToolNames}",
                    runId, clientTools.Count,
                    string.Join(", ", clientTools.Select(t => t.Name)));
            }
            if (!string.IsNullOrWhiteSpace(configuredModel))
            {
                _logger.LogDebug(
                    "AG-UI run {RunId}: agent '{AgentId}' configured model: {ModelId}",
                    runId, input.AgentId, configuredModel);
            }

            requestToLlmStartMs = requestStopwatch.ElapsedMilliseconds;

            // Hand the live agent run to the protocol pipeline. It owns
            // every TEXT_MESSAGE / TOOL_CALL / REASONING / speech.chunk
            // / speech.render event and the voice drain — the endpoint
            // just consumes the aggregate stats it returns.
            var streamResult = await _streamPipeline.StreamAsync(
                new AguiStreamPipelineInput(
                    Agent: agent,
                    ChatMessages: chatMessages,
                    RunOptions: runOptions,
                    Writer: writer,
                    VoiceCoordinator: voiceCoordinator,
                    ThreadId: threadId,
                    RequestStopwatch: requestStopwatch,
                    ChatActivity: chatActivity),
                cancellationToken);

            assistantText = streamResult.AssistantText;
            inputTokens = streamResult.InputTokens;
            outputTokens = streamResult.OutputTokens;
            timeToFirstTokenMs = streamResult.TimeToFirstTokenMs;
            requestToFirstTokenSseMs = streamResult.RequestToFirstTokenSseMs;

            // Capture the assistant's full reply on the chat-level activity so
            // the trace explorer can show the response paired with the prompt
            // in a "Chat" section, no matter which span the admin clicks
            // first. Truncated to a 2 KB tag so payload stays bounded; long
            // replies get clipped with an ellipsis indicator.
            if (!string.IsNullOrWhiteSpace(assistantText))
            {
                chatActivity?.SetTag(
                    "aonik.chat.assistant_response",
                    TruncateForActivityTag(assistantText, AssistantResponseTagMaxChars));
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

            await writer.WriteControlAsync(new
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
                await writer.WriteControlAsync(new
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
            QueuePostStreamPersistence(assistantText, requestStopwatch.ElapsedMilliseconds);
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

        void QueuePostStreamPersistence(string capturedText, long latencyMs)
        {
            Guid? capturedTenantId = _tenantContext?.TenantId;
            Guid? capturedUserId = _currentUserContext?.UserId;

            _coordinator.Enqueue(new PostStreamPersistenceContext(
                PersistedThreadId: threadCtx.PersistedThreadId,
                TenantId: capturedTenantId,
                UserId: capturedUserId,
                AssistantText: capturedText,
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
    /// Cap for <c>aonik.chat.user_prompt</c>. Span tags are bounded to keep
    /// payloads sane; long pasted prompts get clipped with an ellipsis so
    /// admins reading the trace know the value isn't the full message.
    /// </summary>
    private const int UserPromptTagMaxChars = 1024;

    /// <summary>
    /// Cap for <c>aonik.chat.assistant_response</c>. Larger than the prompt
    /// cap because LLM replies are typically the longer side of the
    /// exchange. Still bounded so a 50 KB Markdown table doesn't dominate
    /// the customDimensions blob.
    /// </summary>
    private const int AssistantResponseTagMaxChars = 2048;

    private static string? ExtractLatestUserMessage(IReadOnlyList<AguiMessage>? messages)
    {
        if (messages is null || messages.Count == 0) return null;

        for (var i = messages.Count - 1; i >= 0; i--)
        {
            var message = messages[i];
            if (!string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
                continue;

            var content = message.Content;
            if (string.IsNullOrWhiteSpace(content)) continue;

            return TruncateForActivityTag(content.Trim(), UserPromptTagMaxChars);
        }

        return null;
    }

    private static string TruncateForActivityTag(string value, int maxChars)
        => value.Length <= maxChars ? value : value[..maxChars] + "…";
}
