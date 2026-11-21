using System.Text.Json;

using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Aonik.Ai.Middleware;

/// <summary>
/// A <see cref="DelegatingChatClient"/> middleware that records all AI interactions
/// for audit purposes. Every chat request/response pair is persisted as an
/// <c>AiRun</c> via <see cref="IAiRunWriter"/>, capturing token usage, latency,
/// and outcome to support the AONIK principle that "every AI action is auditable."
/// </summary>
internal sealed class AuditMiddleware : DelegatingChatClient
{
    private const string DefaultUseCase = "chat";

    private readonly IAiRunWriter _aiRunWriter;
    private readonly ILogger<AuditMiddleware> _logger;

    public AuditMiddleware(
        IChatClient innerClient,
        IAiRunWriter aiRunWriter,
        ILogger<AuditMiddleware> logger)
        : base(innerClient)
    {
        _aiRunWriter = aiRunWriter;
        _logger = logger;
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;
        var useCase = ResolveUseCase(options);

        Guid aiRunId = await StartRunAsync(options, useCase, cancellationToken);

        StampCallContext(options, useCase, aiRunId);

        _logger.LogDebug("AuditMiddleware: AI request started at {StartedAt}, AiRunId={AiRunId}", startedAt, aiRunId);

        ChatResponse response;
        try
        {
            response = await base.GetResponseAsync(messages, options, cancellationToken);
        }
        catch (Exception ex)
        {
            if (aiRunId != Guid.Empty)
            {
                try
                {
                    await _aiRunWriter.MarkRunFailedAsync(aiRunId, ex.Message, CancellationToken.None);
                }
                catch (Exception auditEx)
                {
                    _logger.LogWarning(auditEx, "AuditMiddleware: failed to record run failure");
                }
            }
            throw;
        }

        var completedAt = DateTime.UtcNow;
        var durationMs = (completedAt - startedAt).TotalMilliseconds;

        _logger.LogInformation(
            "AuditMiddleware: AI request completed. Duration: {DurationMs}ms, " +
            "InputTokens: {InputTokens}, OutputTokens: {OutputTokens}, TotalTokens: {TotalTokens}",
            durationMs,
            response.Usage?.InputTokenCount ?? 0,
            response.Usage?.OutputTokenCount ?? 0,
            response.Usage?.TotalTokenCount ?? 0);

        if (aiRunId != Guid.Empty)
        {
            try
            {
                // Route through the metrics-aware completion (H17) so the non-streaming path
                // captures structured token/latency/cost metrics — like the streaming path — and
                // feeds the per-tenant AI dashboards, instead of an unstructured outputRef string.
                var totalTokens = (int)(response.Usage?.TotalTokenCount ?? 0);
                await _aiRunWriter.MarkRunCompletedWithMetricsAsync(
                    aiRunId,
                    tokensUsed: totalTokens,
                    latencyMs: (int)durationMs,
                    costEstimate: 0m,
                    outputRef: $"tokens:{totalTokens},latency:{durationMs:F0}ms",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AuditMiddleware: failed to record run completion");
            }
        }

        foreach (FunctionCallContent call in response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>())
            AiToolCallProvenance.Record(call, aiRunId);
        return response;
    }

    private static string ResolveUseCase(ChatOptions? options)
    {
        if (options?.AdditionalProperties is { } props
            && props.TryGetValue(Observability.TelemetryChatClient.UseCasePropertyKey, out var useCaseValue)
            && useCaseValue is string useCase
            && !string.IsNullOrWhiteSpace(useCase))
        {
            return useCase.Trim();
        }

        // Deliberately do NOT fall back to options.ModelId. A model id is a
        // *what* (which provider/model handled the call); a use_case is a
        // *why* (what business need the call serves). Conflating the two leaks
        // model ids into the trace listing as confusing trace names — e.g. a
        // thread-title call shows up as "gpt-5-nano" and can win dedupe over
        // the dominant request-level row. Callers are expected to stamp
        // options.AdditionalProperties[AiTelemetry.UseCaseAttribute] with a
        // semantic value; if they don't, we use the generic "chat" bucket so
        // unstamped calls are visibly grouped instead of disguised.
        return DefaultUseCase;
    }

    private async Task<Guid> StartRunAsync(ChatOptions? options, string useCase, CancellationToken cancellationToken)
    {
        Guid? threadId = options?.AdditionalProperties is { } properties
            && properties.TryGetValue(AiTelemetry.ConversationThreadIdProperty, out var value)
            && value is Guid id ? id : null;
        string inputRefs = threadId is { } persistedThreadId && persistedThreadId != Guid.Empty
            ? JsonSerializer.Serialize(new { threadId = persistedThreadId.ToString("N") })
            : "{}";
        try
        {
            return await _aiRunWriter.StartRunAsync(useCase, inputRefs, cancellationToken);
        }
        catch (Exception ex) when (!threadId.HasValue)
        {
            _logger.LogWarning(ex, "AuditMiddleware: failed to start AiRun; proceeding without audit");
            return Guid.Empty;
        }
    }

    private static void StampCallContext(ChatOptions? options, string useCase, Guid aiRunId)
    {
        if (options is null)
        {
            return;
        }

        options.AdditionalProperties ??= [];
        options.AdditionalProperties[Observability.TelemetryChatClient.UseCasePropertyKey] = useCase;
        options.AdditionalProperties[Observability.TelemetryChatClient.AiRunIdPropertyKey] = aiRunId;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;
        var useCase = ResolveUseCase(options);
        Guid aiRunId = await StartRunAsync(options, useCase, cancellationToken);

        StampCallContext(options, useCase, aiRunId);

        _logger.LogDebug("AuditMiddleware: streaming AI request started at {StartedAt}, AiRunId={AiRunId}", startedAt, aiRunId);

        long totalTokens = 0;
        Exception? failure = null;
        bool completed = false;

        // Manual enumeration: a fault around a `yield return` cannot be caught with try/catch,
        // so we advance the underlying enumerator in a try/catch, capture any fault, and still
        // finalize the run in the finally (even on early consumer disposal or cancellation).
        IAsyncEnumerator<ChatResponseUpdate>? enumerator = null;
        try
        {
            while (true)
            {
                ChatResponseUpdate update;
                try
                {
                    enumerator ??= base.GetStreamingResponseAsync(messages, options, cancellationToken)
                        .GetAsyncEnumerator(cancellationToken);
                    if (!await enumerator.MoveNextAsync())
                    {
                        completed = true;
                        break;
                    }

                    update = enumerator.Current;
                }
                catch (Exception ex)
                {
                    failure = ex;
                    break;
                }

                foreach (var content in update.Contents)
                {
                    // Streaming usage is cumulative/last-wins (a provider reports the running or
                    // final total), so ASSIGN rather than sum — matching TelemetryChatClient — to
                    // avoid double-counting when usage appears in more than one update.
                    if (content is UsageContent usage)
                    {
                        totalTokens = usage.Details.TotalTokenCount
                            ?? ((usage.Details.InputTokenCount ?? 0) + (usage.Details.OutputTokenCount ?? 0));
                    }
                    else if (content is FunctionCallContent call)
                    {
                        AiToolCallProvenance.Record(call, aiRunId);
                    }
                }

                yield return update;
            }
        }
        finally
        {
            try
            {
                if (enumerator is not null) await enumerator.DisposeAsync();
            }
            catch (Exception ex)
            {
                failure ??= ex;
                throw;
            }
            finally
            {
                string? failureReason = failure?.Message ?? (completed ? null : "The response stream ended early.");
                await FinalizeStreamingRunAsync(aiRunId, failureReason, startedAt, totalTokens);
            }
        }

        // Surface the original fault to the caller with its stack intact, after the run has
        // been recorded as failed above.
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(failure);
        }
    }

    private async Task FinalizeStreamingRunAsync(Guid aiRunId, string? failureReason, DateTime startedAt, long totalTokens)
    {
        var durationMs = (DateTime.UtcNow - startedAt).TotalMilliseconds;

        _logger.LogInformation(
            "AuditMiddleware: streaming AI request {Outcome}. Duration: {DurationMs}ms, TotalTokens: {TotalTokens}",
            failureReason is null ? "completed" : "failed", durationMs, totalTokens);

        if (aiRunId == Guid.Empty)
        {
            return;
        }

        // Finalize with CancellationToken.None: the audit write must land even when the request
        // that produced the stream was cancelled — otherwise a cancelled stream leaves a run
        // stuck in the started state.
        try
        {
            if (failureReason is not null)
            {
                await _aiRunWriter.MarkRunFailedAsync(aiRunId, failureReason, CancellationToken.None);
            }
            else
            {
                await _aiRunWriter.MarkRunCompletedWithMetricsAsync(
                    aiRunId,
                    tokensUsed: (int)totalTokens,
                    latencyMs: (int)durationMs,
                    costEstimate: 0m,
                    outputRef: $"stream:tokens:{totalTokens},latency:{durationMs:F0}ms",
                    CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AuditMiddleware: failed to finalize streaming run audit");
        }
    }
}
