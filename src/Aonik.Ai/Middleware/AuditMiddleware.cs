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

        Guid aiRunId;
        try
        {
            aiRunId = await _aiRunWriter.StartRunAsync(
                useCase,
                "{}",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AuditMiddleware: failed to start AiRun; proceeding without audit");
            aiRunId = Guid.Empty;
        }

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
                    await _aiRunWriter.MarkRunFailedAsync(aiRunId, ex.Message, cancellationToken);
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
                await _aiRunWriter.MarkRunCompletedAsync(
                    aiRunId,
                    outputRef: $"tokens:{response.Usage?.TotalTokenCount ?? 0},latency:{durationMs:F0}ms",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AuditMiddleware: failed to record run completion");
            }
        }

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

        _logger.LogDebug("AuditMiddleware: streaming AI request started at {StartedAt}", startedAt);

        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            yield return update;
        }

        _logger.LogDebug("AuditMiddleware: streaming AI request completed. Duration: {DurationMs}ms",
            (DateTime.UtcNow - startedAt).TotalMilliseconds);
    }
}
