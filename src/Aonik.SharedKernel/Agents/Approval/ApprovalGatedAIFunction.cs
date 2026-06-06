using Aonik.SharedKernel.Abstractions.Agents;

using Microsoft.Extensions.AI;

namespace Aonik.SharedKernel.Agents.Approval;

/// <summary>
/// Decorator over an <see cref="AIFunction"/> that enforces tiered approval before the inner
/// domain call can run (Spec 032, finding C3). It derives from <see cref="DelegatingAIFunction"/>
/// so the model still sees the original tool unchanged (same name, description, JSON schema),
/// but it intercepts invocation and routes by tier through <see cref="IToolApprovalService"/>:
/// <list type="bullet">
///   <item><see cref="ToolApprovalTier.Low"/> — a reversible personal-state write. A durable
///   approval record is persisted best-effort (non-blocking), then the inner tool runs in-band.</item>
///   <item><see cref="ToolApprovalTier.Medium"/> — an everyday domain write. The gate consults a
///   durable, args-hash-bound <c>ToolApprovalRequest</c>: if a matching server-validated approval
///   already exists it is consumed and the inner tool runs in-band <strong>once</strong>; otherwise
///   a Pending request is created and a requires-approval result is returned. The inner tool runs
///   only after a human approves via <see cref="IToolApprovalService.DecideAsync"/> and the agent
///   re-invokes with the same arguments.</item>
///   <item><see cref="ToolApprovalTier.High"/> — money movement. Marshalled into a durable
///   <c>Proposal</c>; the inner tool is <strong>never</strong> invoked here and a structured queued
///   result is returned. The matching <c>IProposalHandler</c> is the only path that reaches the
///   domain call, and only after approval.</item>
/// </list>
/// Unlike the framework's <see cref="ApprovalRequiredAIFunction"/> (a passive marker that the
/// invoker may ignore), this decorator fails closed: the refusal/marshalling is enforced inside
/// <see cref="InvokeCoreAsync"/>, so a Medium/High mutation cannot execute in-band even if a host
/// loop does not honour the approval protocol.
/// </summary>
/// <remarks>
/// The <see cref="IToolApprovalService"/> is request-scoped, resolved lazily from the
/// <see cref="IServiceProvider"/> captured at tool-build time. Medium and High require it and fail
/// closed if it is unavailable (null provider or not registered) rather than running ungated. Low
/// treats it as best-effort: a reversible write is not blocked if the durable record cannot be written.
/// </remarks>
internal sealed class ApprovalGatedAIFunction : DelegatingAIFunction
{
    private readonly ToolApprovalOptions _options;
    private readonly IToolApprovalAuditSink _auditSink;
    private readonly IServiceProvider? _serviceProvider;

    public ApprovalGatedAIFunction(
        AIFunction inner,
        ToolApprovalOptions options,
        IToolApprovalAuditSink auditSink,
        IServiceProvider? serviceProvider = null)
        : base(inner)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _auditSink = auditSink ?? throw new ArgumentNullException(nameof(auditSink));
        _serviceProvider = serviceProvider;
    }

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var approvals = ResolveApprovalService();

        if (_options.Tier == ToolApprovalTier.Low)
        {
            // Low risk: a reversible personal-state write. Persist a durable approval record
            // best-effort — a reversible write must not be blocked by an audit-store hiccup or a
            // missing gate service — then audit and run in-band.
            if (approvals is not null)
            {
                try
                {
                    await approvals
                        .GateAsync(new ToolGateContext(Name, _options, arguments), cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Swallow: the _auditSink record below still satisfies the "every mutation is
                    // audited" invariant, and a reversible Low write should not fail on a write hiccup.
                }
            }

            _auditSink.Record(new ToolApprovalAuditEntry(
                Name, _options.Tier, Executed: true, Outcome: "executed-inline"));

            return await base.InvokeCoreAsync(arguments, cancellationToken).ConfigureAwait(false);
        }

        if (_options.Tier == ToolApprovalTier.Medium)
        {
            // Medium: an everyday domain write. Requires an explicit, server-validated confirmation.
            // The gate consults a durable, args-hash-bound ToolApprovalRequest and tells us whether a
            // matching approval already exists (consume + run once) or a Pending request was created.
            if (approvals is not null)
            {
                var outcome = await approvals
                    .GateAsync(new ToolGateContext(Name, _options, arguments), cancellationToken)
                    .ConfigureAwait(false);

                if (outcome.Decision == ToolGateDecision.ApprovedInline)
                {
                    // A human approved this exact call (bound by args-hash) and the gate consumed the
                    // approval. Run the inner domain call in-band — exactly once for this approval.
                    _auditSink.Record(new ToolApprovalAuditEntry(
                        Name, _options.Tier, Executed: true, Outcome: "executed-inline-approved"));

                    return await base.InvokeCoreAsync(arguments, cancellationToken).ConfigureAwait(false);
                }

                if (outcome.Decision == ToolGateDecision.PendingApproval
                    && outcome.ApprovalRequestId is { } requestId)
                {
                    // A Pending request was persisted. Surface it (carrying the id) so the user can
                    // approve via DecideAsync; the agent then re-invokes and the gate consumes it.
                    _auditSink.Record(new ToolApprovalAuditEntry(
                        Name, _options.Tier, Executed: false, Outcome: "pending-approval"));

                    var pendingResult = ToolApprovalRequiredResult.For(Name, _options, requestId);
                    // Record for the stream pipeline so the approval card is emitted even when this
                    // tool ran nested in a sub-agent and its result never reaches the top-level stream.
                    ResolveStreamNotifier()?.Record(pendingResult);
                    return pendingResult;
                }

                // Any other outcome (refused / misconfigured) → fail closed via the refusal below.
            }

            // No gate service available, or the gate refused: fall through to the fail-closed refusal.
        }

        if (_options.Tier == ToolApprovalTier.High)
        {
            // High: money movement. Never run in-band — marshal into a durable Proposal so the
            // only code path to the domain call is the IProposalHandler, after approval (§7.4).
            if (approvals is not null)
            {
                var outcome = await approvals
                    .GateAsync(new ToolGateContext(Name, _options, arguments), cancellationToken)
                    .ConfigureAwait(false);

                if (outcome.Decision == ToolGateDecision.Queued && outcome.ProposalId is { } proposalId)
                {
                    _auditSink.Record(new ToolApprovalAuditEntry(
                        Name, _options.Tier, Executed: false, Outcome: "queued-for-approval"));

                    var queuedResult = ToolApprovalQueuedResult.For(Name, _options, proposalId, outcome.Summary);
                    // Record for the stream pipeline so the queued-money card is emitted even when this
                    // tool ran nested in a sub-agent and its result never reaches the top-level stream.
                    ResolveStreamNotifier()?.Record(queuedResult);
                    return queuedResult;
                }

                // Any non-Queued outcome on the High path (refused / misconfigured) → fail closed.
            }

            // No marshalling service available, or the gate did not queue: fail closed rather than
            // run the money call ungated. Falls through to the refusal below.
        }

        // Medium (refused / no service) and High fallbacks: fail closed. Record the attempt and
        // refuse — the inner domain call is never reached here. The agent is told it is pending, not done.
        _auditSink.Record(new ToolApprovalAuditEntry(
            Name, _options.Tier, Executed: false, Outcome: "blocked-requires-approval"));

        return ToolApprovalRequiredResult.For(Name, _options);
    }

    /// <summary>
    /// Resolves the request-scoped <see cref="IToolApprovalService"/> from the captured provider,
    /// or null if no provider was supplied / the service is not registered.
    /// </summary>
    private IToolApprovalService? ResolveApprovalService() =>
        _serviceProvider?.GetService(typeof(IToolApprovalService)) as IToolApprovalService;

    /// <summary>
    /// Resolves the request-scoped <see cref="IToolApprovalStreamNotifier"/> from the captured
    /// provider, or null if unavailable. Shared with the AG-UI stream pipeline so a gated tool's
    /// approval card is surfaced even when this tool ran nested inside a sub-agent.
    /// </summary>
    private IToolApprovalStreamNotifier? ResolveStreamNotifier() =>
        _serviceProvider?.GetService(typeof(IToolApprovalStreamNotifier)) as IToolApprovalStreamNotifier;
}
