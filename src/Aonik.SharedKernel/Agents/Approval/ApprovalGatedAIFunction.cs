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
///   <item><see cref="ToolApprovalTier.Medium"/> / <see cref="ToolApprovalTier.High"/> — audited,
///   then the inner tool is <strong>not</strong> invoked; a structured requires-approval result
///   is returned instead.</item>
/// </list>
/// Unlike the framework's <see cref="ApprovalRequiredAIFunction"/> (a passive marker that the
/// invoker may ignore), this decorator fails closed: the refusal is enforced inside
/// <see cref="InvokeCoreAsync"/>, so a Medium/High mutation cannot execute even if a host loop
/// does not honour the approval protocol.
/// </summary>
/// <remarks>
/// Focused Spec 032 slice: the in-band Medium confirmation and the durable High proposal
/// execution paths are deferred. Until they land, Medium and High both surface a
/// requires-approval result rather than executing ungated.
/// </remarks>
internal sealed class ApprovalGatedAIFunction : DelegatingAIFunction
{
    private readonly ToolApprovalOptions _options;
    private readonly IToolApprovalAuditSink _auditSink;

    public ApprovalGatedAIFunction(
        AIFunction inner,
        ToolApprovalOptions options,
        IToolApprovalAuditSink auditSink)
        : base(inner)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _auditSink = auditSink ?? throw new ArgumentNullException(nameof(auditSink));
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

        // Medium / High: fail closed. Record the attempt and refuse — the inner domain call is
        // never reached here. The agent is told the action is pending approval, not done.
        _auditSink.Record(new ToolApprovalAuditEntry(
            Name, _options.Tier, Executed: false, Outcome: "blocked-requires-approval"));

        return ToolApprovalRequiredResult.For(Name, _options);
    }
}
