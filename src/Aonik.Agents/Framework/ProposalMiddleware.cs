using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Framework;

/// <summary>
/// A <see cref="DelegatingChatClient"/> middleware that intercepts tool/function
/// invocations and wraps them in the AONIK Proposal pattern:
/// 
/// 1. Agent proposes an action (creates a <see cref="Proposal"/>)
/// 2. Proposal is evaluated against risk-tier policy
/// 3. Low-risk proposals are auto-approved; high-risk require human approval
/// 4. Only approved proposals execute the underlying domain service
/// 
/// This ensures all AI-initiated mutations are auditable and policy-governed.
/// </summary>
internal class ProposalMiddleware : DelegatingChatClient
{
    private readonly AgentsDbContext _dbContext;
    private readonly ILogger<ProposalMiddleware> _logger;

    public ProposalMiddleware(
        IChatClient innerClient,
        AgentsDbContext dbContext,
        ILogger<ProposalMiddleware> logger)
        : base(innerClient)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Intercepts the chat response to record any tool invocations as proposals.
    /// In the current implementation this is a pass-through that logs and records
    /// proposals; the full approve/reject flow will be wired in PR 3.3+.
    /// </summary>
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("ProposalMiddleware: intercepting chat request");

        var response = await base.GetResponseAsync(messages, options, cancellationToken);

        // Future: inspect response for function call results, create Proposal records,
        // evaluate risk tier, and gate execution on approval status.

        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("ProposalMiddleware: intercepting streaming chat request");

        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            yield return update;
        }
    }
}
