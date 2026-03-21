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
        var useCase = options?.ModelId ?? "chat";

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
