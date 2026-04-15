using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// AG-UI protocol streaming endpoint. Implements the AG-UI SSE protocol
/// using a custom minimal API endpoint that resolves the scoped
/// <see cref="IMasterOrchestratorService"/> per-request (required because
/// the orchestrator depends on scoped <c>IChatClient</c>).
///
/// Protocol: POST with JSON body → SSE response with AG-UI events.
/// Reference: https://docs.ag-ui.com/concepts/events
/// </summary>
public static class AguiStreamingEndpoint
{
    private static readonly Regex MultiWhitespaceRegex = new("\\s+", RegexOptions.Compiled);
    private static readonly Regex MarkdownLinkRegex = new(@"\[(?<text>[^\]]+)\]\([^)]+\)", RegexOptions.Compiled);
    private static readonly Regex LeadingListMarkerRegex = new(@"^\s*(?:[-*•]+|\d+[.)])\s+", RegexOptions.Compiled);
    private static readonly Regex LeadingHeadingRegex = new(@"^\s*#+\s*", RegexOptions.Compiled);
    private static readonly Regex SpeechPreambleRegex = new(
        @"^(?:(?:here(?:'s| is)(?:\s+a)?\s+quick\s+summary|here(?:'s| is)\s+the\s+summary|quick\s+summary|summary|in\s+summary|overall|to\s+summari[sz]e)\s*:?\s*)+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    // Matches standalone decimal numbers (e.g. "45.00", "1,250.50") that are NOT
    // preceded or followed by a currency symbol/code — those are handled by ExpandCurrencyAmounts.
    private static readonly Regex StandaloneDecimalRegex = new(
        @"(?<![£€$₦₹¥\w])(?<number>[+-]?(?:\d{1,3}(?:,\d{3})+|\d+)\.\d+)(?!\s*(?:USD|EUR|GBP|NGN|GHS|ZAR|ZWL|ZIG|KES|INR|CNY)\b)",
        RegexOptions.Compiled);
    // Matches emoji characters: emoticons, dingbats, symbols & pictographs,
    // transport/map symbols, supplemental symbols, flags, skin-tone modifiers,
    // variation selectors, zero-width joiners, and other common emoji ranges.
    private static readonly Regex EmojiRegex = new(
        @"[\u200D\uFE0F\u00A9\u00AE\u203C\u2049\u2122\u2139\u2194-\u21AA\u231A-\u23FA\u24C2\u25AA-\u27BF\u2934-\u2935\u2B05-\u2B55\u3030\u303D\u3297\u3299]|[\uD83C-\uDBFF][\uDC00-\uDFFF]",
        RegexOptions.Compiled);
    private const string SupportedSpeechCurrencyCodes = "USD|EUR|GBP|NGN|GHS|ZAR|ZWL|ZIG|KES|INR|CNY";
    private const string SupportedSpeechAmountPattern = @"[+-]?(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d+)?";
    private static readonly Regex CurrencyBeforeAmountRegex = new(
        $@"\b(?<code>{SupportedSpeechCurrencyCodes})\s*(?<amount>{SupportedSpeechAmountPattern})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AmountBeforeCurrencyRegex = new(
        $@"\b(?<amount>{SupportedSpeechAmountPattern})\s*(?<code>{SupportedSpeechCurrencyCodes})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CurrencySymbolAmountRegex = new(
        $@"(?<!\w)(?<symbol>GH₵|KSh|₦|£|€|₹|¥|R|\$)\s*(?<amount>{SupportedSpeechAmountPattern})",
        RegexOptions.Compiled);
    private static readonly IReadOnlyDictionary<string, SpokenCurrencyDescriptor> SpokenCurrencies =
        new Dictionary<string, SpokenCurrencyDescriptor>(StringComparer.OrdinalIgnoreCase)
        {
            ["USD"] = new("dollar", "dollars"),
            ["EUR"] = new("euro", "euros"),
            ["GBP"] = new("pound", "pounds"),
            ["NGN"] = new("naira", "naira"),
            ["GHS"] = new("cedi", "cedis"),
            ["ZAR"] = new("rand", "rand"),
            ["ZWL"] = new("Zimbabwe dollar", "Zimbabwe dollars"),
            ["ZIG"] = new("Zimbabwe Gold", "Zimbabwe Gold"),
            ["KES"] = new("Kenyan shilling", "Kenyan shillings"),
            ["INR"] = new("rupee", "rupees"),
            ["CNY"] = new("yuan", "yuan")
        };
    private static readonly IReadOnlyDictionary<string, string> SpokenCurrencySymbols =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["$"] = "USD",
            ["€"] = "EUR",
            ["£"] = "GBP",
            ["₦"] = "NGN",
            ["GH₵"] = "GHS",
            ["R"] = "ZAR",
            ["KSh"] = "KES",
            ["₹"] = "INR",
            ["¥"] = "CNY"
        };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    /// <summary>
    /// Maps the AG-UI streaming endpoint at the specified route pattern.
    /// </summary>
    public static IEndpointConventionBuilder MapAguiStreaming(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/ai/agui")
    {
        return endpoints.MapPost(pattern, HandleAguiRequest)
            .WithName("AgUiStreaming")
            .WithTags("AI Agents")
            .WithSummary("Stream AG-UI chat events")
            .WithDescription("Implements the AG-UI SSE protocol for real-time agent chat. Routes messages through the master orchestrator and streams responses as AG-UI events.");
    }

    private static async Task HandleAguiRequest(HttpContext context)
    {
        var cancellationToken = context.RequestAborted;
        var logger = context.RequestServices.GetRequiredService<ILogger<IMasterOrchestratorService>>();

        // Parse the AG-UI request body
        AguiRunInput? input;
        try
        {
            input = await JsonSerializer.DeserializeAsync<AguiRunInput>(
                context.Request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize AG-UI request body");
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Invalid request body", cancellationToken);
            return;
        }

        if (input is null)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Request body is required", cancellationToken);
            return;
        }

        var threadId = input.ThreadId ?? Guid.NewGuid().ToString("N");
        var runId = input.RunId ?? Guid.NewGuid().ToString("N");

        // Propagate session (threadId) and user identifiers as OTel baggage + span
        // attributes so the BaggageSpanProcessor copies them to all child spans,
        // enabling Langfuse session grouping and user attribution.
        var activity = Activity.Current;
        if (activity is not null)
        {
            activity.SetBaggage(AiTelemetry.SessionIdAttribute, threadId);
            activity.SetTag(AiTelemetry.SessionIdAttribute, threadId);

            var userProvider = context.RequestServices.GetService<ICurrentUserProvider>();
            if (userProvider is not null && userProvider.TryGetCurrentUserId(out var userId))
            {
                var userIdStr = userId.ToString();
                activity.SetBaggage(AiTelemetry.UserIdAttribute, userIdStr);
                activity.SetTag(AiTelemetry.UserIdAttribute, userIdStr);
            }
        }

        // ── Thread persistence: create or load thread ────────────────────
        var chatThreadService = context.RequestServices.GetService<IChatThreadService>();
        var titleGenerator = context.RequestServices.GetService<IChatThreadTitleGenerator>();

        Guid? persistedThreadId = null;
        var isNewThread = false;
        string? firstUserMessage = null;

        if (chatThreadService is not null)
        {
            try
            {
                // Extract the last user message from the AG-UI messages
                firstUserMessage = input.Messages?
                    .LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
                    ?.Content;

                if (!string.IsNullOrEmpty(firstUserMessage))
                {
                    // Check if the threadId maps to an existing persisted thread
                    if (Guid.TryParse(input.ThreadId, out var existingId))
                    {
                        persistedThreadId = existingId;

                        await chatThreadService.AppendMessageAsync(
                            existingId, "user", firstUserMessage,
                            cancellationToken: cancellationToken);
                    }
                    else
                    {
                        // Create a new thread
                        persistedThreadId = await chatThreadService.CreateThreadAsync(
                            firstUserMessage,
                            agentName: input.AgentId,
                            cancellationToken: cancellationToken);
                        isNewThread = true;

                        // Update threadId to use the persisted thread ID for SSE events
                        threadId = persistedThreadId.Value.ToString("N");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "AG-UI thread persistence failed — continuing without thread tracking");
            }
        }

        // Set SSE response headers
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache,no-store";
        context.Response.Headers["Pragma"] = "no-cache";
        context.Response.Headers["X-Accel-Buffering"] = "no"; // Disable nginx buffering

        // Resolve the agent to stream against.
        // Named agentId → build the domain agent descriptor directly; if the descriptor
        //   declares RequiresUserBrief, the User Brief is projected and prepended as a
        //   system message so the agent has full user context before responding.
        // No agentId → master orchestrator (Admin UI default).
        AIAgent agent;
        List<ChatMessage>? userBriefPreamble = null;

        if (!string.IsNullOrEmpty(input.AgentId))
        {
            var (builtAgent, descriptor) = await ResolveDomainAgentAsync(
                input.AgentId, context.RequestServices, logger, cancellationToken);
            agent = builtAgent;

            // If the descriptor declares it needs the User Brief, project it now
            // and prepend as a system message so the persona has full user context.
            if (descriptor.RequiresUserBrief)
            {
                var userProvider = context.RequestServices.GetService<ICurrentUserProvider>();
                var tenantProvider = context.RequestServices.GetService<ITenantProvider>();
                if (userProvider is not null
                    && tenantProvider is not null
                    && userProvider.TryGetCurrentUserId(out var briefUserId)
                    && tenantProvider.TryGetCurrentTenantId(out var briefTenantId))
                {
                    var briefMessage = await BuildUserBriefMessageAsync(
                        context.RequestServices, briefTenantId, briefUserId,
                        logger, cancellationToken);
                    if (briefMessage is not null)
                        userBriefPreamble = [briefMessage];
                }
            }
        }
        else
        {
            var orchestrator = context.RequestServices.GetRequiredService<IMasterOrchestratorService>();
            agent = await orchestrator.GetAgentAsync(cancellationToken);
        }

        // Accumulate streamed assistant text for post-stream persistence
        var assistantTextBuilder = new System.Text.StringBuilder();

        // ── Performance metrics ─────────────────────────────────────────
        var stopwatch = Stopwatch.StartNew();
        long inputTokens = 0;
        long outputTokens = 0;
        long? timeToFirstTokenMs = null;

        try
        {
            // Convert AG-UI messages to M.E.AI ChatMessage list.
            // For agents that declare RequiresUserBrief, the brief is prepended as a
            // system message so the LLM receives current user context before the history.
            var chatMessages = ConvertMessages(input.Messages);
            if (userBriefPreamble is not null)
                chatMessages = [.. userBriefPreamble, .. chatMessages];

            // Convert frontend tool definitions to declaration-only AITool instances.
            // These are passed to the LLM so it knows the tools exist, but they cannot
            // be invoked server-side — the agent framework emits them as FunctionCallContent
            // in the stream, and the frontend handles execution via the re-run loop.
            // Pattern follows official MAF AG-UI: AGUIEndpointRouteBuilderExtensions.cs
            var clientTools = ConvertClientTools(input.Tools, logger);
            var clientToolNames = new HashSet<string>(
                clientTools.Select(t => t.Name), StringComparer.OrdinalIgnoreCase);

            // Build run options with client tools so the LLM sees them alongside
            // the server-side domain agent tools already registered on the agent.
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
                logger.LogDebug(
                    "AG-UI run {RunId}: passing {ToolCount} client tool(s) to agent: {ToolNames}",
                    runId, clientTools.Count, string.Join(", ", clientToolNames));
            }

            // Emit RUN_STARTED
            await WriteSseEventAsync(context.Response, new
            {
                type = "RUN_STARTED",
                threadId,
                runId,
            }, cancellationToken);

            // Stream the agent response
            var messageId = Guid.NewGuid().ToString("N");
            var messageStarted = false;
            var requiresVisualAttention = false;
            var requiresApproval = false;

            await foreach (var update in agent.RunStreamingAsync(
                chatMessages, session: null, options: runOptions, cancellationToken: cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested) break;

                // Convert AgentResponseUpdate to ChatResponseUpdate
                var chatUpdate = update.AsChatResponseUpdate();
                if (chatUpdate is null) continue;

                // Process each content item in the update
                foreach (var content in chatUpdate.Contents ?? [])
                {
                    switch (content)
                    {
                        case TextContent textContent when !string.IsNullOrEmpty(textContent.Text):
                            if (!messageStarted)
                            {
                                timeToFirstTokenMs ??= stopwatch.ElapsedMilliseconds;
                                await WriteSseEventAsync(context.Response, new
                                {
                                    type = "TEXT_MESSAGE_START",
                                    messageId,
                                    role = "assistant",
                                }, cancellationToken);
                                messageStarted = true;
                            }

                            assistantTextBuilder.Append(textContent.Text);

                            await WriteSseEventAsync(context.Response, new
                            {
                                type = "TEXT_MESSAGE_CONTENT",
                                messageId,
                                delta = textContent.Text,
                            }, cancellationToken);
                            break;

                        case FunctionCallContent functionCall:
                            // Use a stable ID across START/ARGS/END — providers may
                            // leave CallId empty, so generate a fallback once and reuse it.
                            var toolCallId = ResolveToolCallId(functionCall);
                            var toolName = functionCall.Name ?? string.Empty;
                            requiresVisualAttention |= IsDisplayToolCall(toolName);
                            requiresApproval |= IsApprovalToolCall(toolName);

                            await WriteSseEventAsync(context.Response, new
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
                                await WriteSseEventAsync(context.Response, new
                                {
                                    type = "TOOL_CALL_ARGS",
                                    toolCallId,
                                    delta = argsJson,
                                }, cancellationToken);
                            }

                            await WriteSseEventAsync(context.Response, new
                            {
                                type = "TOOL_CALL_END",
                                toolCallId,
                            }, cancellationToken);
                            break;

                        case FunctionResultContent functionResult:
                            await WriteSseEventAsync(context.Response, new
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

                            await WriteSseEventAsync(context.Response, new
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

                // Note: chatUpdate.Text is the aggregated text from Contents,
                // so we don't emit it separately to avoid duplicate text deltas.
            }

            var assistantText = assistantTextBuilder.ToString();

            // Close message if one was started
            if (messageStarted)
            {
                await WriteSseEventAsync(context.Response, new
                {
                    type = "TEXT_MESSAGE_END",
                    messageId,
                }, cancellationToken);

            }

            var speechText = BuildSpeechRender(assistantText, requiresVisualAttention, requiresApproval);
            if (!string.IsNullOrWhiteSpace(speechText))
            {
                await WriteSseEventAsync(context.Response, new
                {
                    type = "CUSTOM",
                    name = "speech.render",
                    value = new
                    {
                        messageId,
                        speechText,
                        requiresVisualAttention,
                        requiresApproval
                    }
                }, cancellationToken);
            }

            // Emit RUN_FINISHED with performance metrics
            stopwatch.Stop();

            var metrics = new
            {
                inputTokens,
                outputTokens,
                totalTokens = inputTokens + outputTokens,
                latencyMs = stopwatch.ElapsedMilliseconds,
                timeToFirstTokenMs = timeToFirstTokenMs ?? stopwatch.ElapsedMilliseconds,
            };

            logger.LogInformation(
                "AguiRunCompleted: RunId={RunId} AgentName={AgentName} ThreadId={ThreadId} LatencyMs={LatencyMs} TtftMs={TtftMs} InputTokens={InputTokens} OutputTokens={OutputTokens} TotalTokens={TotalTokens}",
                runId, input.AgentId ?? "orchestrator", threadId,
                metrics.latencyMs, metrics.timeToFirstTokenMs,
                inputTokens, outputTokens, inputTokens + outputTokens);

            await WriteSseEventAsync(context.Response, new
            {
                type = "RUN_FINISHED",
                threadId,
                runId,
                metrics,
            }, cancellationToken);

            // Flush the RUN_FINISHED event out to the wire, then complete the
            // response. Without CompleteAsync + an early return, ACA's Envoy
            // ingress holds the chunked transfer-encoding open until the
            // handler itself returns — so any work we do on this request scope
            // (DB writes, a second LLM call for title generation) adds to the
            // *wire* latency the client sees, even though RUN_FINISHED was
            // already written. Observed: server LatencyMs=13s but
            // requests.duration=46s on ACA. Fix: fire-and-forget the
            // persistence in a detached scope so this handler returns now.
            await context.Response.Body.FlushAsync(CancellationToken.None);
            try
            {
                await context.Response.CompleteAsync();
            }
            catch (Exception completeEx)
            {
                logger.LogDebug(completeEx,
                    "AG-UI Response.CompleteAsync threw for thread {ThreadId} — continuing with persistence",
                    threadId);
            }

            // Capture request-scoped context so the background task can
            // re-seed it in its own scope (the request scope is disposed
            // as soon as this handler returns).
            Guid? capturedTenantId = null;
            Guid? capturedUserId = null;
            var requestTenantContext = context.RequestServices.GetService<ITenantContext>();
            if (requestTenantContext?.TenantId is { } tId) capturedTenantId = tId;
            var requestUserContext = context.RequestServices.GetService<ICurrentUserContext>();
            if (requestUserContext?.UserId is { } uId) capturedUserId = uId;

            var scopeFactory = context.RequestServices.GetRequiredService<IServiceScopeFactory>();
            var capturedAgentId = input.AgentId;
            var capturedThreadId = threadId;
            var capturedRunId = runId;
            var capturedAssistantText = assistantText;
            var capturedFirstUserMessage = firstUserMessage;
            var capturedIsNewThread = isNewThread;
            var capturedPersistedThreadId = persistedThreadId;
            var capturedInputTokens = inputTokens;
            var capturedOutputTokens = outputTokens;
            var capturedLatencyMs = stopwatch.ElapsedMilliseconds;

            _ = Task.Run(async () =>
            {
                try
                {
                    using var bgScope = scopeFactory.CreateScope();
                    var bgServices = bgScope.ServiceProvider;

                    // Re-seed tenant + user context in the new scope so
                    // ITenantProvider / ICurrentUserProvider resolve correctly
                    // and EF query filters on IChatThreadService work.
                    if (capturedTenantId.HasValue)
                    {
                        var tc = bgServices.GetService<ITenantContext>();
                        if (tc is not null)
                        {
                            tc.TenantId = capturedTenantId.Value;
                            tc.ResolutionSource = "agui-post-stream";
                        }
                    }
                    if (capturedUserId.HasValue)
                    {
                        var uc = bgServices.GetService<ICurrentUserContext>();
                        if (uc is not null)
                        {
                            uc.UserId = capturedUserId.Value;
                            uc.TenantId = capturedTenantId;
                            uc.IsAuthenticated = true;
                        }
                    }

                    var bgLogger = bgServices.GetRequiredService<ILogger<IMasterOrchestratorService>>();

                    // ── Post-stream thread persistence ──────────────────
                    var bgChatThreadService = bgServices.GetService<IChatThreadService>();
                    var bgTitleGenerator = bgServices.GetService<IChatThreadTitleGenerator>();
                    if (bgChatThreadService is not null && capturedPersistedThreadId.HasValue)
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(capturedAssistantText))
                            {
                                await bgChatThreadService.AppendMessageAsync(
                                    capturedPersistedThreadId.Value,
                                    "assistant",
                                    capturedAssistantText,
                                    agentName: capturedAgentId,
                                    cancellationToken: CancellationToken.None);
                            }

                            if (capturedIsNewThread && bgTitleGenerator is not null && !string.IsNullOrEmpty(capturedFirstUserMessage))
                            {
                                try
                                {
                                    var title = await bgTitleGenerator.GenerateTitleAsync(
                                        capturedFirstUserMessage, CancellationToken.None);
                                    await bgChatThreadService.UpdateTitleAsync(
                                        capturedPersistedThreadId.Value, title, CancellationToken.None);
                                }
                                catch (Exception titleEx)
                                {
                                    bgLogger.LogWarning(titleEx,
                                        "AG-UI title generation failed for thread {ThreadId} — placeholder title retained",
                                        capturedPersistedThreadId.Value);
                                }
                            }
                        }
                        catch (Exception persistEx)
                        {
                            bgLogger.LogWarning(persistEx,
                                "AG-UI post-stream persistence failed for thread {ThreadId}",
                                capturedPersistedThreadId.Value);
                        }
                    }

                    // ── Post-stream AiRun metrics persistence ───────────
                    var bgAiRunWriter = bgServices.GetService<IAiRunWriter>();
                    if (bgAiRunWriter is not null)
                    {
                        try
                        {
                            var useCase = capturedAgentId ?? "master-orchestrator";
                            var aiRunId = await bgAiRunWriter.StartRunAsync(
                                useCase, $"{{\"threadId\":\"{capturedThreadId}\"}}", CancellationToken.None);

                            await bgAiRunWriter.MarkRunCompletedWithMetricsAsync(
                                aiRunId,
                                tokensUsed: (int)(capturedInputTokens + capturedOutputTokens),
                                latencyMs: (int)capturedLatencyMs,
                                costEstimate: 0m,
                                outputRef: $"tokens:{capturedInputTokens + capturedOutputTokens},latency:{capturedLatencyMs}ms",
                                cancellationToken: CancellationToken.None);
                        }
                        catch (Exception aiRunEx)
                        {
                            bgLogger.LogWarning(aiRunEx,
                                "AG-UI post-stream AiRun persistence failed for run {RunId}", capturedRunId);
                        }
                    }
                }
                catch (Exception bgEx)
                {
                    // Best-effort logging — we're detached from any request scope.
                    logger.LogWarning(bgEx,
                        "AG-UI post-stream background task crashed for thread {ThreadId}",
                        capturedThreadId);
                }
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client disconnected, nothing to do
            logger.LogDebug("AG-UI stream cancelled for thread {ThreadId}", threadId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AG-UI streaming error for thread {ThreadId}, run {RunId}", threadId, runId);

            // Try to send RUN_ERROR before closing
            try
            {
                await WriteSseEventAsync(context.Response, new
                {
                    type = "RUN_ERROR",
                    message = ex.Message,
                    code = "INTERNAL_ERROR",
                }, CancellationToken.None);
            }
            catch
            {
                // If we can't write the error event, the connection is already broken
            }
        }

        // Best-effort final flush. In the success path the response has
        // already been completed via CompleteAsync and this will throw —
        // that's fine, the bytes are already on the wire.
        try
        {
            await context.Response.Body.FlushAsync(CancellationToken.None);
        }
        catch
        {
            // Response already completed / connection already closed.
        }
    }

    /// <summary>
    /// Resolves a named <see cref="IDomainAgentDescriptor"/> by its agent name,
    /// applies any database-level configuration overrides (instructions, tool set,
    /// active flag), and builds the domain agent. Returns both the built agent and
    /// the descriptor so the caller can inspect flags like
    /// <see cref="IDomainAgentDescriptor.RequiresUserBrief"/>.
    /// </summary>
    private static async Task<(AIAgent Agent, IDomainAgentDescriptor Descriptor)> ResolveDomainAgentAsync(
        string agentId,
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var descriptors = services.GetRequiredService<IEnumerable<IDomainAgentDescriptor>>();
        var descriptor = descriptors.FirstOrDefault(
            d => string.Equals(d.Name, agentId, StringComparison.OrdinalIgnoreCase));

        if (descriptor is null)
        {
            throw new InvalidOperationException(
                $"No domain agent descriptor registered with name '{agentId}'. " +
                $"Available: {string.Join(", ", descriptors.Select(d => d.Name))}");
        }

        var configService = services.GetRequiredService<IAgentConfigurationService>();
        var config = await configService.GetResolvedAsync(agentId, cancellationToken);

        if (config is { IsActive: false })
            throw new InvalidOperationException($"Agent '{agentId}' is inactive per configuration.");

        var chatClient = services.GetRequiredService<IChatClient>();
        AIAgent agent;

        if (config is not null)
        {
            var instructionsOverride = !string.IsNullOrWhiteSpace(config.InstructionsText)
                ? config.InstructionsText
                : null;

            HashSet<string>? allowedToolNames = null;
            if (!string.IsNullOrWhiteSpace(config.ToolsetIdsJson) && config.ToolsetIdsJson != "[]")
            {
                try
                {
                    var toolNames = JsonSerializer.Deserialize<List<string>>(config.ToolsetIdsJson);
                    if (toolNames is { Count: > 0 })
                        allowedToolNames = new HashSet<string>(toolNames, StringComparer.Ordinal);
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex, "Invalid ToolsetIdsJson for agent '{AgentName}' — using all tools", agentId);
                }
            }

            agent = descriptor.Build(chatClient, services, instructionsOverride, allowedToolNames);
            logger.LogInformation("AG-UI: resolved domain agent '{AgentName}' with config override", agentId);
        }
        else
        {
            agent = descriptor.Build(chatClient, services);
            logger.LogInformation("AG-UI: resolved domain agent '{AgentName}' with code defaults", agentId);
        }

        return (agent, descriptor);
    }

    /// <summary>
    /// Projects the User Brief for the given user and returns it as a
    /// <see cref="ChatMessage"/> with <see cref="ChatRole.System"/> role,
    /// ready to be prepended to the conversation history.
    /// Returns <c>null</c> if projection fails (agent proceeds without brief).
    /// </summary>
    private static async Task<ChatMessage?> BuildUserBriefMessageAsync(
        IServiceProvider services,
        Guid tenantId,
        Guid userId,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var projector = services.GetRequiredService<IUserBriefProjector>();
            var brief = await projector.ProjectAsync(tenantId, userId, cancellationToken: cancellationToken);

            var briefJson = JsonSerializer.Serialize(brief, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            });

            var content = $"""
                ## User Brief (current context — treat as ground truth for this session)

                ```json
                {briefJson}
                ```
                """;

            return new ChatMessage(ChatRole.System, content);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to build User Brief for user {UserId} in tenant {TenantId} — proceeding without brief",
                userId, tenantId);
            return null;
        }
    }

    /// <summary>
    /// Converts AG-UI frontend tool definitions (raw JSON) into declaration-only
    /// <see cref="AITool"/> instances using <see cref="AIFunctionFactory.CreateDeclaration"/>.
    /// These tools describe functions the LLM can call but that execute client-side.
    /// Pattern follows official MAF AG-UI source: AIToolExtensions.AsAITools().
    /// </summary>
    internal static List<AITool> ConvertClientTools(
        List<JsonElement>? toolElements,
        ILogger logger)
    {
        if (toolElements is null || toolElements.Count == 0)
            return [];

        var tools = new List<AITool>(toolElements.Count);

        foreach (var element in toolElements)
        {
            try
            {
                var name = element.GetProperty("name").GetString();
                if (string.IsNullOrEmpty(name))
                {
                    logger.LogWarning("AG-UI client tool missing 'name', skipping");
                    continue;
                }

                var description = element.TryGetProperty("description", out var descProp)
                    ? descProp.GetString()
                    : null;

                var parameters = element.TryGetProperty("parameters", out var paramsProp)
                    ? paramsProp
                    : default;

                tools.Add(AIFunctionFactory.CreateDeclaration(
                    name: name,
                    description: description,
                    jsonSchema: parameters));

                logger.LogDebug("AG-UI: registered client tool declaration '{ToolName}'", name);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "AG-UI: failed to parse client tool element, skipping");
            }
        }

        return tools;
    }

    /// <summary>
    /// Converts AG-UI input messages to M.E.AI <see cref="ChatMessage"/> objects.
    /// Handles all AG-UI message types including assistant messages with tool calls
    /// and tool result messages with toolCallId references.
    /// </summary>
    private static List<ChatMessage> ConvertMessages(IEnumerable<AguiMessage>? messages)
    {
        if (messages is null)
            return [];

        var result = new List<ChatMessage>();
        foreach (var msg in messages)
        {
            var roleName = msg.Role?.ToLowerInvariant();

            switch (roleName)
            {
                case "assistant":
                {
                    var contents = new List<AIContent>();

                    // Add text content if present
                    if (!string.IsNullOrEmpty(msg.Content))
                        contents.Add(new TextContent(msg.Content));

                    // Add function call content items for each tool call
                    if (msg.ToolCalls is { Count: > 0 })
                    {
                        foreach (var tc in msg.ToolCalls)
                        {
                            if (tc.Function is null) continue;

                            IDictionary<string, object?>? args = null;
                            if (!string.IsNullOrEmpty(tc.Function.Arguments))
                            {
                                try
                                {
                                    args = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                                        tc.Function.Arguments, JsonOptions);
                                }
                                catch
                                {
                                    // If arguments fail to parse, pass as a single "raw" key
                                    args = new Dictionary<string, object?> { ["raw"] = tc.Function.Arguments };
                                }
                            }

                            contents.Add(new FunctionCallContent(tc.Id ?? string.Empty, tc.Function.Name ?? string.Empty, args));
                        }
                    }

                    result.Add(new ChatMessage(ChatRole.Assistant, contents));
                    break;
                }

                case "tool":
                {
                    // Tool result message — wraps content in FunctionResultContent
                    var toolContent = new FunctionResultContent(
                        msg.ToolCallId ?? string.Empty,
                        msg.Content ?? string.Empty);

                    result.Add(new ChatMessage(ChatRole.Tool, [toolContent]));
                    break;
                }

                default:
                {
                    var role = roleName switch
                    {
                        "user" => ChatRole.User,
                        "system" => ChatRole.System,
                        "developer" => ChatRole.System, // Map developer to system
                        _ => ChatRole.User,
                    };

                    result.Add(new ChatMessage(role, msg.Content ?? string.Empty));
                    break;
                }
            }
        }

        return result;
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

    internal static string BuildSpeechRender(
        string assistantText,
        bool requiresVisualAttention = false,
        bool requiresApproval = false)
    {
        var speechText = string.Empty;
        if (!string.IsNullOrWhiteSpace(assistantText))
        {
            var normalized = assistantText.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');
            normalized = MarkdownLinkRegex.Replace(normalized, "${text}");

            var lines = normalized
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeSpeechLine)
                .Where(line => !string.IsNullOrWhiteSpace(line));

            speechText = string.Join(' ', lines);
        }

        speechText = SpeechPreambleRegex.Replace(speechText, string.Empty);
        speechText = ReplaceSymbolsWithWords(speechText);
        speechText = EmojiRegex.Replace(speechText, string.Empty);
        speechText = ExpandCurrencyAmounts(speechText);
        speechText = NormalizeStandaloneDecimals(speechText);
        speechText = Regex.Replace(speechText, @"\s+([,.;!?])", "$1");
        speechText = MultiWhitespaceRegex.Replace(speechText, " ").Trim();
        speechText = AppendChatReviewGuidance(speechText, requiresVisualAttention, requiresApproval);

        return speechText;
    }

    private static string AppendChatReviewGuidance(
        string speechText,
        bool requiresVisualAttention,
        bool requiresApproval)
    {
        var guidance = BuildChatReviewGuidance(requiresVisualAttention, requiresApproval);
        if (string.IsNullOrWhiteSpace(guidance))
        {
            return speechText;
        }

        if (string.IsNullOrWhiteSpace(speechText))
        {
            return guidance;
        }

        var normalized = speechText.TrimEnd();
        if (normalized.EndsWith(guidance, StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        var separator = normalized.EndsWith('.') || normalized.EndsWith('!') || normalized.EndsWith('?')
            ? " "
            : ". ";

        return $"{normalized}{separator}{guidance}";
    }

    private static string BuildChatReviewGuidance(bool requiresVisualAttention, bool requiresApproval)
    {
        if (requiresVisualAttention && requiresApproval)
        {
            return "I've opened the chat so you can review the details and approve this action.";
        }

        if (requiresApproval)
        {
            return "I've opened the chat so you can review and approve this action.";
        }

        if (requiresVisualAttention)
        {
            return "I've opened the chat so you can review the details.";
        }

        return string.Empty;
    }

    private static string NormalizeSpeechLine(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var wasListItem = LeadingListMarkerRegex.IsMatch(trimmed);
        trimmed = LeadingHeadingRegex.Replace(trimmed, string.Empty);
        trimmed = LeadingListMarkerRegex.Replace(trimmed, string.Empty);
        trimmed = trimmed
            .Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("__", string.Empty, StringComparison.Ordinal)
            .Replace('`', ' ');
        trimmed = MultiWhitespaceRegex.Replace(trimmed, " ").Trim();

        if (wasListItem && trimmed.Length > 0 && !trimmed.EndsWith('.') && !trimmed.EndsWith('!') && !trimmed.EndsWith('?'))
        {
            trimmed = $"{trimmed}.";
        }

        return trimmed;
    }

    private static string ExpandCurrencyAmounts(string value)
    {
        var expanded = CurrencyBeforeAmountRegex.Replace(value, match =>
            BuildSpokenAmount(match.Groups["amount"].Value, match.Groups["code"].Value));

        expanded = AmountBeforeCurrencyRegex.Replace(expanded, match =>
            BuildSpokenAmount(match.Groups["amount"].Value, match.Groups["code"].Value));

        expanded = CurrencySymbolAmountRegex.Replace(expanded, match =>
        {
            var symbol = match.Groups["symbol"].Value;
            if (!SpokenCurrencySymbols.TryGetValue(symbol, out var currencyCode))
            {
                return match.Value;
            }

            return BuildSpokenAmount(match.Groups["amount"].Value, currencyCode);
        });

        return expanded;
    }

    private static string BuildSpokenAmount(string amount, string currencyCode)
    {
        if (!SpokenCurrencies.TryGetValue(currencyCode, out var descriptor))
        {
            return $"{FormatSpokenNumber(amount)} {currencyCode}";
        }

        var normalizedAmount = amount.Replace(",", string.Empty, StringComparison.Ordinal);
        if (!decimal.TryParse(normalizedAmount, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsedAmount))
        {
            return $"{amount} {descriptor.Plural}";
        }

        // Build natural spoken currency: "12 pounds 50", "45 pounds", "1,250 dollars 99"
        return FormatSpokenCurrencyPhrase(parsedAmount, descriptor);
    }

    /// <summary>
    /// Formats a decimal amount for natural speech output.
    /// Whole amounts drop the decimal entirely (45.00 → "45").
    /// Amounts with meaningful fractional parts strip trailing zeros
    /// (3.50 → "3.5", 2.125 → "2.125").
    /// </summary>
    private static string FormatSpokenNumber(string rawAmount)
    {
        var normalized = rawAmount.Replace(",", string.Empty, StringComparison.Ordinal);
        if (!decimal.TryParse(normalized, NumberStyles.Number | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var value))
        {
            return rawAmount;
        }

        // Whole number — no decimal needed
        if (value == decimal.Truncate(value))
        {
            return decimal.Truncate(value).ToString("#,0", CultureInfo.InvariantCulture);
        }

        // Has a meaningful fractional part — strip trailing zeros
        // e.g. 3.50 → "3.5", 2.125 → "2.125"
        return value.ToString("G", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Builds a natural spoken currency phrase by placing the currency name
    /// between the major and minor units, mirroring how humans say amounts:
    /// <list type="bullet">
    ///   <item>£45.00 → "45 pounds"</item>
    ///   <item>£12.50 → "12 pounds 50"</item>
    ///   <item>$1,250.99 → "1,250 dollars 99"</item>
    ///   <item>£1 → "1 pound"</item>
    ///   <item>-£5.25 → "minus 5 pounds 25"</item>
    /// </list>
    /// </summary>
    private static string FormatSpokenCurrencyPhrase(
        decimal amount,
        SpokenCurrencyDescriptor descriptor)
    {
        var abs = decimal.Abs(amount);
        var sign = amount < 0 ? "minus " : "";
        var wholePart = decimal.Truncate(abs);

        var currencyName = abs == 1m ? descriptor.Singular : descriptor.Plural;

        // Whole amount — "45 pounds"
        if (abs == wholePart)
        {
            return $"{sign}{wholePart:#,0} {currencyName}";
        }

        // Has minor units — extract them as an integer (pence, cents, etc.)
        var fractional = (abs - wholePart) * 100;
        var minorUnits = Math.Round(fractional, 0);

        // Standard two-decimal-place amount — "12 pounds 50"
        if (fractional == minorUnits && minorUnits > 0)
        {
            // Use whole number for minor units (no leading zero for speech)
            return $"{sign}{wholePart:#,0} {currencyName} {minorUnits:0}";
        }

        // More than 2 decimal places (rare) — fall back to plain format
        return $"{sign}{abs.ToString("G", CultureInfo.InvariantCulture)} {currencyName}";
    }

    /// <summary>
    /// Replaces symbols with their spoken-word equivalents and strips
    /// emojis/special characters that TTS engines struggle to pronounce.
    /// </summary>
    private static string ReplaceSymbolsWithWords(string text)
    {
        // Order matters: replace multi-char sequences before single chars,
        // and contextual patterns (with surrounding spaces) before bare symbols.
        var result = text;

        // Comparison / arrow symbols
        result = result.Replace(" >= ", " greater than or equal to ", StringComparison.Ordinal);
        result = result.Replace(" <= ", " less than or equal to ", StringComparison.Ordinal);
        result = result.Replace(" != ", " not equal to ", StringComparison.Ordinal);
        result = result.Replace(" => ", " leads to ", StringComparison.Ordinal);
        result = result.Replace(" -> ", " to ", StringComparison.Ordinal);
        result = result.Replace(" <- ", " from ", StringComparison.Ordinal);
        result = result.Replace(" <> ", " versus ", StringComparison.Ordinal);

        // Common symbols with surrounding spaces (contextual)
        result = result.Replace(" > ", " greater than ", StringComparison.Ordinal);
        result = result.Replace(" < ", " less than ", StringComparison.Ordinal);
        result = result.Replace(" = ", " equals ", StringComparison.Ordinal);
        result = result.Replace(" + ", " plus ", StringComparison.Ordinal);
        result = result.Replace(" - ", " minus ", StringComparison.Ordinal);
        result = result.Replace(" x ", " times ", StringComparison.Ordinal);
        result = result.Replace(" / ", " divided by ", StringComparison.Ordinal);
        result = result.Replace(" & ", " and ", StringComparison.Ordinal);
        result = result.Replace(" | ", " or ", StringComparison.Ordinal);
        result = result.Replace(" @ ", " at ", StringComparison.Ordinal);
        result = result.Replace(" % ", " percent ", StringComparison.Ordinal);

        // Percentage attached to a number (e.g. "45%")
        result = Regex.Replace(result, @"(\d)%", "$1 percent");

        // Unicode symbols that TTS may read oddly
        result = result.Replace("→", " to ", StringComparison.Ordinal);
        result = result.Replace("←", " from ", StringComparison.Ordinal);
        result = result.Replace("↑", " up ", StringComparison.Ordinal);
        result = result.Replace("↓", " down ", StringComparison.Ordinal);
        result = result.Replace("✓", " yes ", StringComparison.Ordinal);
        result = result.Replace("✔", " yes ", StringComparison.Ordinal);
        result = result.Replace("✗", " no ", StringComparison.Ordinal);
        result = result.Replace("✘", " no ", StringComparison.Ordinal);
        result = result.Replace("•", ",", StringComparison.Ordinal);
        result = result.Replace("–", " to ", StringComparison.Ordinal);   // en-dash (range)
        result = result.Replace("—", ", ", StringComparison.Ordinal);     // em-dash (pause)
        result = result.Replace("…", "...", StringComparison.Ordinal);
        result = result.Replace("©", " copyright ", StringComparison.Ordinal);
        result = result.Replace("®", " registered ", StringComparison.Ordinal);
        result = result.Replace("™", " trademark ", StringComparison.Ordinal);

        return result;
    }

    /// <summary>
    /// Normalizes standalone decimal numbers for natural speech.
    /// Strips unnecessary trailing zeros so TTS reads "45" instead of
    /// "forty five dot zero zero". Only applies to numbers that were NOT
    /// already processed by <see cref="ExpandCurrencyAmounts"/>.
    /// </summary>
    private static string NormalizeStandaloneDecimals(string text)
    {
        return StandaloneDecimalRegex.Replace(text, match =>
            FormatSpokenNumber(match.Groups["number"].Value));
    }

    internal static bool IsDisplayToolCall(string toolName)
    {
        return toolName.StartsWith("display_", StringComparison.OrdinalIgnoreCase);
    }

    internal static string ResolveToolCallId(FunctionCallContent functionCall)
    {
        return !string.IsNullOrWhiteSpace(functionCall.CallId)
            ? functionCall.CallId
            : Guid.NewGuid().ToString("N");
    }

    internal static bool IsApprovalToolCall(string toolName)
    {
        return string.Equals(toolName, "confirmAction", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record SpokenCurrencyDescriptor(string Singular, string Plural);
}

/// <summary>
/// AG-UI run input DTO. Mirrors the AG-UI protocol's request body.
/// </summary>
public sealed class AguiRunInput
{
    [JsonPropertyName("threadId")]
    public string? ThreadId { get; set; }

    [JsonPropertyName("runId")]
    public string? RunId { get; set; }

    /// <summary>
    /// Optional agent identifier. When set, the endpoint resolves and runs the
    /// named <see cref="IDomainAgentDescriptor"/> directly (bypassing the master
    /// orchestrator). When <c>null</c>, the master orchestrator is used.
    /// Product apps (e.g. Payabo) set this to route to a specific domain agent.
    /// </summary>
    [JsonPropertyName("agentId")]
    public string? AgentId { get; set; }

    [JsonPropertyName("messages")]
    public List<AguiMessage>? Messages { get; set; }

    [JsonPropertyName("state")]
    public JsonElement? State { get; set; }

    [JsonPropertyName("tools")]
    public List<JsonElement>? Tools { get; set; }

    [JsonPropertyName("context")]
    public List<JsonElement>? Context { get; set; }

    [JsonPropertyName("forwardedProps")]
    public JsonElement? ForwardedProperties { get; set; }
}

/// <summary>
/// AG-UI message DTO within the run input.
/// Supports all AG-UI message types: user, assistant (with tool calls),
/// system, developer, tool (with toolCallId), reasoning, and activity.
/// </summary>
public sealed class AguiMessage
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Tool calls made by an assistant message.
    /// </summary>
    [JsonPropertyName("toolCalls")]
    public List<AguiToolCall>? ToolCalls { get; set; }

    /// <summary>
    /// For tool messages: the ID of the tool call this message responds to.
    /// </summary>
    [JsonPropertyName("toolCallId")]
    public string? ToolCallId { get; set; }

    /// <summary>
    /// For tool messages: error message if the tool call failed.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>
    /// Encrypted content for privacy-preserving state continuity.
    /// </summary>
    [JsonPropertyName("encryptedContent")]
    public string? EncryptedContent { get; set; }

    /// <summary>
    /// Encrypted value for reasoning/tool messages.
    /// </summary>
    [JsonPropertyName("encryptedValue")]
    public string? EncryptedValue { get; set; }

    /// <summary>
    /// For activity messages: the activity type discriminator.
    /// </summary>
    [JsonPropertyName("activityType")]
    public string? ActivityType { get; set; }
}

/// <summary>
/// AG-UI tool call DTO within an assistant message.
/// </summary>
public sealed class AguiToolCall
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("function")]
    public AguiFunctionCall? Function { get; set; }

    [JsonPropertyName("encryptedValue")]
    public string? EncryptedValue { get; set; }
}

/// <summary>
/// AG-UI function call DTO within a tool call.
/// </summary>
public sealed class AguiFunctionCall
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }
}
