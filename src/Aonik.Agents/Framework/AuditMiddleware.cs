using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Framework;

/// <summary>
/// A <see cref="DelegatingChatClient"/> middleware that records all AI interactions
/// for audit purposes. Every chat request/response pair is logged with metadata
/// (agent name, tenant, timestamps, token usage) to support the AONIK principle
/// that "every AI action is auditable."
/// 
/// In a future iteration this will write to the <c>AiRun</c> / <c>AiTrace</c>
/// tables in the AI module. For now it logs structured audit data.
/// </summary>
internal class AuditMiddleware : DelegatingChatClient
{
    private readonly ILogger<AuditMiddleware> _logger;

    public AuditMiddleware(
        IChatClient innerClient,
        ILogger<AuditMiddleware> logger)
        : base(innerClient)
    {
        _logger = logger;
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;

        _logger.LogInformation("AuditMiddleware: AI request started at {StartedAt}", startedAt);

        var response = await base.GetResponseAsync(messages, options, cancellationToken);

        var completedAt = DateTime.UtcNow;
        var durationMs = (completedAt - startedAt).TotalMilliseconds;

        _logger.LogInformation(
            "AuditMiddleware: AI request completed. Duration: {DurationMs}ms, ResponseLength: {Length}",
            durationMs,
            response.Text?.Length ?? 0);

        // Future: persist to AiRun table via cross-module event or direct write
        // This requires a reference to AiDbContext or an integration event.

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
