using System.Text.Json;

using Aonik.Agents.Contracts.Agui;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.Voice.Frames;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Voxa.Frames;
using Voxa.Processors;
using Voxa.Services.MicrosoftAgents;

namespace Aonik.Voice.Processors;

/// <summary>
/// AONIK glue around <see cref="MicrosoftAgentVoice.CreateProcessor"/>. Owns the per-connection
/// voice-turn state that is genuinely AONIK-specific — ChatThread persistence, user-brief preamble,
/// frontend-tool allowlist, post-stream <c>AiRun</c> audit, and the Spec 032 approval bridge — and
/// lets Voxa own the agent loop, the data-loop / turn-worker split, frontend-tool TCS correlation,
/// and turn-boundary frames.
///
/// <para>
/// This replaces the previous local <c>AonikAgentProcessor</c> (~280 LOC of pipeline plumbing) with
/// closure-configured <see cref="MicrosoftAgentVoiceOptions"/> delegates. The behavior is unchanged:
/// </para>
///
/// <list type="bullet">
///   <item><c>BuildMessages</c> calls <see cref="IChatThreadManager.EnsureThreadAsync"/> +
///       <see cref="IChatThreadManager.ReconstructHistoryAsync"/> on every turn (mirrors AGUI's
///       stateless re-run model), prepends the optional user-brief preamble, and emits a
///       <see cref="ThreadReadyFrame"/> exactly once per WS session.</item>
///   <item><c>BuildRunOptions</c> returns the precomputed <see cref="ChatClientAgentRunOptions"/>
///       (already stamped with <c>use_case = "voice"</c> by the endpoint).</item>
///   <item><c>IsFrontendTool</c> consults the AONIK frontend-tool catalog.</item>
///   <item><c>OnTurnCompleted</c> enqueues a <see cref="PostStreamPersistenceContext"/> so the
///       background coordinator writes the <c>AiRun</c> row, then drains any Spec 032 approval the
///       turn raised (see below).</item>
/// </list>
///
/// <para>
/// <strong>Spec 032 approval bridge (voice parity).</strong> When the full gated voice agent runs a
/// classified Medium/High tool, the approval gate does NOT execute the domain call in-band — it
/// returns a structured approval result and records it on the request-scoped
/// <see cref="IToolApprovalStreamNotifier"/> (exactly as the AG-UI stream pipeline relies on). After
/// the turn completes, <c>OnTurnCompleted</c> drains the notifier and, for each gated call, emits a
/// Voxa <c>toolCall</c> envelope named <c>confirmAction</c> carrying the durable
/// <c>approvalRequestId</c> (and, for High, the <c>proposalId</c>). It then awaits the client's
/// <c>toolResult</c> and routes the decision to <see cref="IToolApprovalService.DecideAsync"/> — the
/// single, transport-neutral decision authority. No transport decides whether a mutation runs; voice
/// only presents the card and collects the decision (Spec 032 §7.7).
/// </para>
/// </summary>
public static class AonikVoiceAgent
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    /// <summary>The Voxa <c>toolCall</c> envelope name the client renders the approval card from.</summary>
    private const string ConfirmActionToolName = "confirmAction";

    /// <summary>
    /// Build a fully-configured agent-loop processor. All AONIK contracts (thread manager, message
    /// converter, post-stream coordinator, approval service/notifier) are captured by closure on the
    /// returned processor.
    /// </summary>
    public static AgentLoopProcessor CreateProcessor(
        AIAgent voiceAgent,
        ChatMessage? userBriefPreamble,
        ChatClientAgentRunOptions? runOptions,
        IReadOnlySet<string> frontendToolNames,
        IChatThreadManager threadManager,
        IAguiMessageConverter converter,
        IPostStreamPersistenceCoordinator postStreamCoordinator,
        IToolApprovalService approvalService,
        IToolApprovalStreamNotifier approvalNotifier,
        string? initialChatThreadId,
        string? agentId,
        Guid? tenantId,
        Guid? userId,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(voiceAgent);
        ArgumentNullException.ThrowIfNull(frontendToolNames);
        ArgumentNullException.ThrowIfNull(threadManager);
        ArgumentNullException.ThrowIfNull(converter);
        ArgumentNullException.ThrowIfNull(postStreamCoordinator);
        ArgumentNullException.ThrowIfNull(approvalService);
        ArgumentNullException.ThrowIfNull(approvalNotifier);

        // Per-connection state captured by the closures below. Safe to mutate from BuildMessages
        // and OnTurnCompleted because Voxa's AgentLoopProcessor serialises turns through a single
        // background worker — there is never more than one in-flight turn per processor.
        Guid? persistedThreadId = null;
        var threadReadyEmitted = false;
        ChatThreadContext? lastTurnThreadCtx = null;
        string? lastTurnUserText = null;

        return MicrosoftAgentVoice.CreateProcessor(voiceAgent, options =>
        {
            options.IsFrontendTool = name =>
                !string.IsNullOrEmpty(name) && frontendToolNames.Contains(name);

            options.BuildRunOptions = _ => runOptions;

            options.BuildMessages = async (turnCtx, ct) =>
            {
                var userMessage = new AguiMessage { Role = "user", Content = turnCtx.UserText };

                // EnsureThreadAsync on every turn — that's how new user messages are appended to
                // existing threads (ChatThreadManager.EnqueueDetachedUserMessageAppend), exactly
                // mirroring AGUI's stateless re-run model.
                var threadCtx = await threadManager.EnsureThreadAsync(
                    clientThreadId: persistedThreadId?.ToString("N") ?? initialChatThreadId,
                    messages: new[] { userMessage },
                    agentId: agentId,
                    ct).ConfigureAwait(false);

                if (persistedThreadId is null)
                {
                    persistedThreadId = threadCtx.PersistedThreadId;
                }

                // Emit threadReady once per WS session so mobile knows the persisted ID for
                // reconnects (matches AGUI's RUN_STARTED thread-id signal). Use the loop's
                // emitter — the driver's IAsyncEnumerable<Frame> contract isn't in scope here.
                if (!threadReadyEmitted)
                {
                    await turnCtx.Emitter
                        .EmitAsync(new ThreadReadyFrame(threadCtx.ThreadIdString, threadCtx.IsNewThread), ct)
                        .ConfigureAwait(false);
                    threadReadyEmitted = true;
                }

                // Single-user-message form triggers ChatThreadManager's db-history prepend; passing
                // null/empty would just return empty.
                var history = await threadManager.ReconstructHistoryAsync(
                    persistedThreadId: persistedThreadId,
                    clientMessages: new[] { userMessage },
                    ct).ConfigureAwait(false);

                var messages = converter.ConvertMessages(history.Messages).ToList();
                if (userBriefPreamble is { } preamble)
                {
                    messages.Insert(0, preamble);
                }

                lastTurnThreadCtx = threadCtx;
                lastTurnUserText = turnCtx.UserText;
                return messages;
            };

            options.OnTurnCompleted = async (turnCtx, summary, ct) =>
            {
                if (lastTurnThreadCtx is { } captured)
                {
                    // Per-turn audit. Mirrors AguiStreamingEndpoint's PostStreamPersistenceContext
                    // call (line 479-491). AuditMiddleware doesn't write AiRun rows for streaming
                    // responses; the row comes from this coordinator.
                    postStreamCoordinator.Enqueue(new PostStreamPersistenceContext(
                        PersistedThreadId: persistedThreadId,
                        TenantId: tenantId,
                        UserId: userId,
                        AssistantText: summary.AssistantText,
                        AgentId: agentId,
                        InputTokens: summary.Usage.InputTokens,
                        OutputTokens: summary.Usage.OutputTokens,
                        LatencyMs: summary.ElapsedMs,
                        IsNewThread: captured.IsNewThread,
                        FirstUserMessage: lastTurnUserText ?? string.Empty,
                        ThreadIdString: captured.ThreadIdString,
                        RunId: Guid.NewGuid().ToString("N"),
                        UseCase: "voice"));
                }

                // Spec 032 — surface any gated Medium/High mutation the turn raised. For a read-only
                // voice agent the notifier is empty and this is a no-op.
                await PresentPendingApprovalsAsync(turnCtx, approvalNotifier, approvalService, logger, ct)
                    .ConfigureAwait(false);
            };
        }, logger);
    }

    /// <summary>
    /// Drains the request-scoped approval notifier and, for each gated-but-not-executed mutation,
    /// presents a <c>confirmAction</c> toolCall envelope to the client and routes its decision to
    /// <see cref="IToolApprovalService.DecideAsync"/>. Reuses the same
    /// <c>ToolApprovalRequiredResult</c> / <c>ToolApprovalQueuedResult</c> records the AG-UI stream
    /// pipeline inspects, so the wire payload stays consistent across transports.
    /// </summary>
    private static async ValueTask PresentPendingApprovalsAsync(
        VoiceTurnContext turnCtx,
        IToolApprovalStreamNotifier approvalNotifier,
        IToolApprovalService approvalService,
        ILogger? logger,
        CancellationToken ct)
    {
        var pending = approvalNotifier.Drain();
        if (pending.Count == 0)
        {
            return;
        }

        foreach (var result in pending)
        {
            var card = ToVoiceApprovalCard(result);
            if (card is null)
            {
                // Fail-closed refusal with no durable id — nothing the user can act on. The
                // assistant's prose already told them the action is pending.
                continue;
            }

            try
            {
                await PresentSingleApprovalAsync(turnCtx, approvalService, card, logger, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Socket closed / turn cancelled before the user decided — the request simply stays
                // Pending (Medium) or queued (High) and can be decided later from the approvals queue.
                return;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(
                    ex,
                    "Voice: failed to present approval card for tool {Tool} (request {RequestId})",
                    card.Tool,
                    card.ApprovalRequestId);
            }
        }
    }

    private static async ValueTask PresentSingleApprovalAsync(
        VoiceTurnContext turnCtx,
        IToolApprovalService approvalService,
        VoiceApprovalCard card,
        ILogger? logger,
        CancellationToken ct)
    {
        // The Voxa frontend-tool gateway correlates the result by callId; register the awaiter
        // BEFORE emitting the request so a fast client can't race the registration.
        var callId = Guid.NewGuid().ToString("N");
        var resultTask = turnCtx.FrontendTools.AwaitToolResultAsync(callId, ct);

        var argumentsJson = JsonSerializer.Serialize(
            new
            {
                approvalRequestId = card.ApprovalRequestId,
                proposalId = card.ProposalId,
                tool = card.Tool,
                tier = card.Tier,
                actionKind = card.ActionKind,
                kind = card.Kind,
            },
            JsonOpts);

        // Voxa serialises this to {"type":"toolCall","callId":...,"name":"confirmAction","argumentsJson":...}
        // via WireProtocol.BuildToolCall — the same envelope the mobile client renders an approval card from.
        await turnCtx.Emitter
            .EmitAsync(new ToolCallRequestFrame(callId, ConfirmActionToolName, argumentsJson), ct)
            .ConfigureAwait(false);

        var toolResult = await resultTask.ConfigureAwait(false);

        var decision = ParseDecision(toolResult);
        if (decision is null)
        {
            // The client neither approved nor rejected (cancelled / unparsable). Leave the request
            // Pending and tell the user nothing happened.
            await EmitTextAsync(
                turnCtx,
                $"No decision was recorded for {card.ActionKind}, so nothing was changed.",
                ct).ConfigureAwait(false);
            return;
        }

        var outcome = await approvalService
            .DecideAsync(
                card.ApprovalRequestId,
                new ToolApprovalDecisionInput(decision.Value),
                ct)
            .ConfigureAwait(false);

        logger?.LogInformation(
            "Voice: approval decision for tool {Tool} (request {RequestId}) → {Outcome}",
            card.Tool,
            card.ApprovalRequestId,
            outcome.Outcome);

        // Surface the outcome back into the conversation as a follow-up. For High this is the
        // money-movement result (the proposal executed synchronously inside DecideAsync); for Medium
        // an approval is recorded and the gate runs the tool when the agent re-invokes it on a later
        // turn (the in-turn re-invocation is a deferred follow-up — the decision is durable regardless).
        await EmitTextAsync(turnCtx, DescribeOutcome(card, outcome), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Translates a gated tool result (recorded on the notifier) into the minimal card payload voice
    /// needs, or null when the result is not an actionable approval (e.g. a fail-closed refusal with
    /// no durable request id). Mirrors <c>ToolApprovalStreamEvents.Inspect</c>'s "only when there's an
    /// actionable id" rule, but builds the Voxa envelope shape rather than the AG-UI CUSTOM event.
    /// </summary>
    private static VoiceApprovalCard? ToVoiceApprovalCard(object result) => result switch
    {
        ToolApprovalRequiredResult { ApprovalRequestId: { } requestId } required =>
            new VoiceApprovalCard(
                Kind: "medium",
                ApprovalRequestId: requestId,
                ProposalId: null,
                Tool: required.Tool,
                Tier: required.Tier,
                ActionKind: required.ActionKind),

        ToolApprovalQueuedResult queued =>
            new VoiceApprovalCard(
                Kind: "high",
                ApprovalRequestId: queued.ApprovalRequestId,
                ProposalId: queued.ProposalId,
                Tool: queued.Tool,
                Tier: queued.Tier,
                ActionKind: queued.ActionKind),

        _ => null,
    };

    /// <summary>
    /// Parses the client's <c>toolResult</c> into a decision. Accepts a JSON body with a
    /// <c>decision</c> field ("Approve"/"Reject") or a bare string, and treats an
    /// <see cref="ToolCallResultFrame.IsError"/> frame as no decision. Returns null when the result
    /// carries neither a recognisable approve nor reject.
    /// </summary>
    private static ToolApprovalDecisionType? ParseDecision(ToolCallResultFrame frame)
    {
        if (frame.IsError)
        {
            return null;
        }

        var raw = frame.ResultJson?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        // Try a structured body first: {"decision":"Approve"} / {"decision":"Reject"} / {"approved":true}.
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("decision", out var decisionEl)
                    && decisionEl.ValueKind == JsonValueKind.String
                    && Enum.TryParse<ToolApprovalDecisionType>(decisionEl.GetString(), ignoreCase: true, out var parsed))
                {
                    return parsed;
                }

                if (root.TryGetProperty("approved", out var approvedEl)
                    && (approvedEl.ValueKind == JsonValueKind.True || approvedEl.ValueKind == JsonValueKind.False))
                {
                    return approvedEl.ValueKind == JsonValueKind.True
                        ? ToolApprovalDecisionType.Approve
                        : ToolApprovalDecisionType.Reject;
                }
            }
            else if (root.ValueKind == JsonValueKind.String
                && Enum.TryParse<ToolApprovalDecisionType>(root.GetString(), ignoreCase: true, out var bare))
            {
                return bare;
            }
        }
        catch (JsonException)
        {
            // Fall through to the plain-text interpretation below.
        }

        // Plain-text fallback: the client may send a bare "Approve"/"Reject" string not as JSON.
        if (Enum.TryParse<ToolApprovalDecisionType>(raw.Trim('"'), ignoreCase: true, out var text))
        {
            return text;
        }

        return null;
    }

    private static string DescribeOutcome(VoiceApprovalCard card, ToolApprovalDecisionResult outcome) =>
        outcome.Outcome switch
        {
            ToolApprovalDecisionOutcome.Approved =>
                outcome.Message ?? $"{card.ActionKind} was approved.",
            ToolApprovalDecisionOutcome.Rejected =>
                outcome.Message ?? $"{card.ActionKind} was rejected, so nothing was changed.",
            ToolApprovalDecisionOutcome.Forbidden =>
                "You are not authorised to approve that action.",
            ToolApprovalDecisionOutcome.NotFound =>
                "That approval could not be found, so nothing was changed.",
            ToolApprovalDecisionOutcome.Expired =>
                $"The approval for {card.ActionKind} expired before it was decided.",
            ToolApprovalDecisionOutcome.AlreadyDecided =>
                $"{card.ActionKind} had already been decided.",
            _ => outcome.Message ?? "The decision could not be recorded.",
        };

    private static ValueTask EmitTextAsync(VoiceTurnContext turnCtx, string text, CancellationToken ct) =>
        turnCtx.Emitter.EmitAsync(new TextFrame(text), ct);

    /// <summary>Minimal card payload voice needs to render the confirmAction envelope + route a decision.</summary>
    private sealed record VoiceApprovalCard(
        string Kind,
        Guid ApprovalRequestId,
        Guid? ProposalId,
        string Tool,
        string Tier,
        string ActionKind);
}
