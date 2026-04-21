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
        var response = HttpContext.Response;
        var runId = input.RunId ?? Guid.NewGuid().ToString("N");
        var requestStopwatch = Stopwatch.StartNew();
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

        // Resolve / create the persisted thread before anything else — the
        // thread GUID is what we stamp onto OTel baggage and SSE events.
        var threadCtx = await _threadManager.EnsureThreadAsync(
            input.ThreadId, input.Messages, input.AgentId, cancellationToken);
        var threadId = threadCtx.ThreadIdString;
        var requestToThreadReadyMs = requestStopwatch.ElapsedMilliseconds;

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
            await WriteSseEventAsync(response, new
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

            var historyResolution = await historyTask;
            var effectiveMessages = historyResolution.Messages;
            historySource = historyResolution.Source;
            historyDurationMs = historyResolution.DurationMs;
            historyMessageCount = effectiveMessages?.Count ?? 0;

            var chatMessages = _converter.ConvertMessages(effectiveMessages);
            if (agentContext.UserBriefPreamble is not null)
                chatMessages = [agentContext.UserBriefPreamble, .. chatMessages];

            // Client-side tool declarations — the LLM sees them so it can emit
            // FunctionCallContent, but the frontend is responsible for execution.
            var clientTools = _converter.ConvertClientTools(input.Tools);
            clientToolCount = clientTools.Count;

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
                                await WriteSseEventAsync(response, new
                                {
                                    type = "TEXT_MESSAGE_START",
                                    messageId,
                                    role = "assistant",
                                }, cancellationToken);
                                messageStarted = true;
                            }

                            assistantTextBuilder.Append(textContent.Text);
                            speechBuffer.Append(textContent.Text);

                            await WriteSseEventAsync(response, new
                            {
                                type = "TEXT_MESSAGE_CONTENT",
                                messageId,
                                delta = textContent.Text,
                            }, cancellationToken);
                            requestToFirstTokenSseMs ??= requestStopwatch.ElapsedMilliseconds;

                            while (speechBuffer.TryPopSentence(out var rawChunk))
                            {
                                await EmitSpeechChunkAsync(
                                    response, messageId, speechBuffer.NextChunkIndex - 1,
                                    rawChunk, isFinal: false, cancellationToken);
                            }
                            break;

                        case FunctionCallContent functionCall:
                            var toolCallId = _classifier.ResolveCallId(functionCall);
                            var toolName = functionCall.Name ?? string.Empty;
                            requiresVisualAttention |= _classifier.IsDisplay(toolName);
                            requiresApproval |= _classifier.RequiresApproval(toolName);

                            await WriteSseEventAsync(response, new
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
                                await WriteSseEventAsync(response, new
                                {
                                    type = "TOOL_CALL_ARGS",
                                    toolCallId,
                                    delta = argsJson,
                                }, cancellationToken);
                            }

                            await WriteSseEventAsync(response, new
                            {
                                type = "TOOL_CALL_END",
                                toolCallId,
                            }, cancellationToken);
                            break;

                        case FunctionResultContent functionResult:
                            await WriteSseEventAsync(response, new
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

                            await WriteSseEventAsync(response, new
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
                await WriteSseEventAsync(response, new
                {
                    type = "TEXT_MESSAGE_END",
                    messageId,
                }, cancellationToken);
            }

            var tailChunk = speechBuffer.FlushRemaining();
            if (tailChunk is not null)
            {
                await EmitSpeechChunkAsync(
                    response, messageId, speechBuffer.NextChunkIndex - 1,
                    tailChunk, isFinal: true, cancellationToken);
            }

            var guidanceText = _speechRenderer.RenderGuidance(requiresVisualAttention, requiresApproval);
            await WriteSseEventAsync(response, new
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

            requestStopwatch.Stop();

            var metrics = new
            {
                inputTokens,
                outputTokens,
                totalTokens = inputTokens + outputTokens,
                latencyMs = requestStopwatch.ElapsedMilliseconds,
                timeToFirstTokenMs = timeToFirstTokenMs ?? requestStopwatch.ElapsedMilliseconds,
            };

            _logger.LogInformation(
                "AguiRunCompleted: RunId={RunId} AgentName={AgentName} ThreadId={ThreadId} LatencyMs={LatencyMs} TtftMs={TtftMs} InputTokens={InputTokens} OutputTokens={OutputTokens} TotalTokens={TotalTokens}",
                runId, input.AgentId ?? "orchestrator", threadId,
                metrics.latencyMs, metrics.timeToFirstTokenMs,
                inputTokens, outputTokens, inputTokens + outputTokens);

            await WriteSseEventAsync(response, new
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
            _logger.LogDebug("AG-UI stream cancelled for thread {ThreadId}", threadId);
        }
        catch (Exception ex)
        {
            outcome = "error";
            _logger.LogError(ex, "AG-UI streaming error for thread {ThreadId}, run {RunId}", threadId, runId);

            try
            {
                await WriteSseEventAsync(response, new
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
    /// </summary>
    private static async Task WriteSseEventAsync<T>(
        HttpResponse response,
        T eventData,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(eventData, JsonOptions);
        await response.WriteAsync($"data: {json}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    private async Task EmitSpeechChunkAsync(
        HttpResponse response,
        string messageId,
        int chunkIndex,
        string rawChunk,
        bool isFinal,
        CancellationToken cancellationToken)
    {
        var chunkText = _speechRenderer.RenderChunk(rawChunk);
        if (string.IsNullOrWhiteSpace(chunkText))
            return;

        await WriteSseEventAsync(response, new
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
    }
}
