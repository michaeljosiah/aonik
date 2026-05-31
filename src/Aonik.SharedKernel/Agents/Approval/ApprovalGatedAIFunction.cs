using Aonik.SharedKernel.Abstractions.Agents;

using Microsoft.Extensions.AI;

namespace Aonik.SharedKernel.Agents.Approval;

/// <summary>
/// Decorator over an <see cref="AIFunction"/> that enforces tiered approval before the inner
/// domain call can run (Spec 032, finding C3). It derives from <see cref="DelegatingAIFunction"/>
/// so the model still sees the original tool unchanged (same name, description, JSON schema),
/// but it intercepts invocation:
/// <list type="bullet">
///   <item><see cref="ToolApprovalTier.Low"/> — audited, then the inner tool runs in-band.</item>
///   <item><see cref="ToolApprovalTier.High"/> — audited, then marshalled into a durable
///   <c>Proposal</c> via <see cref="IToolApprovalService"/>; the inner tool is <strong>never</strong>
///   invoked here and a structured queued result is returned. The matching <c>IProposalHandler</c>
///   is the only path that reaches the domain call, and only after approval.</item>
///   <item><see cref="ToolApprovalTier.Medium"/> — audited, then the inner tool is <strong>not</strong>
///   invoked; a structured requires-approval result is returned (the in-band confirm path is deferred).</item>
/// </list>
/// Unlike the framework's <see cref="ApprovalRequiredAIFunction"/> (a passive marker that the
/// invoker may ignore), this decorator fails closed: the refusal/marshalling is enforced inside
/// <see cref="InvokeCoreAsync"/>, so a Medium/High mutation cannot execute in-band even if a host
/// loop does not honour the approval protocol.
/// </summary>
/// <remarks>
/// Focused Spec 032 slice: the in-band Medium confirmation path is deferred (Medium fails closed
/// like before). The High path is live — it requires the request-scoped
/// <see cref="IToolApprovalService"/>, resolved lazily from the <see cref="IServiceProvider"/>
/// captured at tool-build time. If that service is unavailable (null provider or not registered),
/// High fails closed exactly like Medium rather than running ungated.
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
        if (_options.Tier == ToolApprovalTier.Low)
        {
            // Low risk: a reversible personal-state write. Audited, then runs in-band.
            _auditSink.Record(new ToolApprovalAuditEntry(
                Name, _options.Tier, Executed: true, Outcome: "executed-inline"));

            return await base.InvokeCoreAsync(arguments, cancellationToken).ConfigureAwait(false);
        }

        if (_options.Tier == ToolApprovalTier.High)
        {
            // High: money movement. Never run in-band — marshal into a durable Proposal so the
            // only code path to the domain call is the IProposalHandler, after approval (§7.4).
            var approvals = _serviceProvider?.GetService(typeof(IToolApprovalService)) as IToolApprovalService;
            if (approvals is not null)
            {
                var outcome = await approvals
                    .GateAsync(new ToolGateContext(Name, _options, arguments), cancellationToken)
                    .ConfigureAwait(false);

                if (outcome.Decision == ToolGateDecision.Queued && outcome.ProposalId is { } proposalId)
                {
                    _auditSink.Record(new ToolApprovalAuditEntry(
                        Name, _options.Tier, Executed: false, Outcome: "queued-for-approval"));

                    return ToolApprovalQueuedResult.For(Name, _options, proposalId, outcome.Summary);
                }

                // Any non-Queued outcome on the High path (refused / misconfigured) → fail closed.
            }

            // No marshalling service available, or the gate did not queue: fail closed rather than
            // run the money call ungated. Falls through to the refusal below.
        }

        // Medium (deferred confirm) and High fallbacks: fail closed. Record the attempt and refuse —
        // the inner domain call is never reached here. The agent is told the action is pending, not done.
        _auditSink.Record(new ToolApprovalAuditEntry(
            Name, _options.Tier, Executed: false, Outcome: "blocked-requires-approval"));

        return ToolApprovalRequiredResult.For(Name, _options);
    }
}
