using Aonik.Agents.Contracts.Agui;
using Aonik.Agents.Contracts.Services;
using Aonik.Voice.Frames;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Voxa.Processors;
using Voxa.Services.MicrosoftAgents;

namespace Aonik.Voice.Processors;

/// <summary>
/// AONIK glue around <see cref="MicrosoftAgentVoice.CreateProcessor"/>. Owns the per-connection
/// voice-turn state that is genuinely AONIK-specific — ChatThread persistence, user-brief preamble,
/// frontend-tool allowlist, post-stream <c>AiRun</c> audit — and lets Voxa own the agent loop, the
/// data-loop / turn-worker split, frontend-tool TCS correlation, and turn-boundary frames.
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
///       background coordinator writes the <c>AiRun</c> row.</item>
/// </list>
/// </summary>
public static class AonikVoiceAgent
{
    /// <summary>
    /// Build a fully-configured agent-loop processor. All AONIK contracts (thread manager, message
    /// converter, post-stream coordinator) are captured by closure on the returned processor.
    /// </summary>
    public static AgentLoopProcessor CreateProcessor(
        AIAgent voiceAgent,
        ChatMessage? userBriefPreamble,
        ChatClientAgentRunOptions? runOptions,
        IReadOnlySet<string> frontendToolNames,
        IChatThreadManager threadManager,
        IAguiMessageConverter converter,
        IPostStreamPersistenceCoordinator postStreamCoordinator,
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

            options.OnTurnCompleted = (turnCtx, summary, ct) =>
            {
                if (lastTurnThreadCtx is not { } captured)
                {
                    // BuildMessages didn't run — nothing to record. Shouldn't happen on a successful
                    // turn (BuildMessages is the first thing the driver does), but defensive.
                    return ValueTask.CompletedTask;
                }

                // Per-turn audit. Mirrors AguiStreamingEndpoint's PostStreamPersistenceContext call
                // (line 479-491). AuditMiddleware doesn't write AiRun rows for streaming responses;
                // the row comes from this coordinator.
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

                return ValueTask.CompletedTask;
            };
        }, logger);
    }
}
