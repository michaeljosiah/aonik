namespace Aonik.SharedKernel.Abstractions.Agents;

/// <summary>
/// Domain-side executor for an approved proposal. Implementations live in the
/// owning module (e.g. PersonalFinance for FLG annotations, Finance for bill
/// payments) and are registered with DI keyed by their <see cref="ProposalType"/>
/// discriminator. Resolved at approval time by <see cref="IProposalDispatcher"/>.
///
/// Failure modes:
///   • Throw for unexpected errors (DB unavailable, partner API failure).
///     The dispatcher reverts the proposal to <c>Proposed</c> and rethrows.
///   • Return <see cref="ProposalHandlerResult.Applied"/> = <c>false</c> +
///     <see cref="ProposalHandlerResult.Message"/> for expected business
///     reasons (payload references a deleted entity, idempotency conflict
///     that should not retry). The dispatcher reverts and throws
///     <see cref="ProposalExecutionFailedException"/> which surfaces as HTTP 422.
///   • Return <see cref="ProposalHandlerResult.Applied"/> = <c>true</c> with
///     optional <see cref="ProposalHandlerResult.AppliedResourceType"/> /
///     <see cref="ProposalHandlerResult.AppliedResourceId"/> on success.
/// </summary>
public interface IProposalHandler
{
    /// <summary>The <c>ProposalType</c> string this handler is registered for.</summary>
    string ProposalType { get; }

    Task<ProposalHandlerResult> HandleAsync(
        AgentProposalDetail proposal,
        CancellationToken cancellationToken);
}

/// <summary>
/// Optional symmetric counterpart: cleans up domain state when a proposal is
/// dismissed (for example, removes a <c>Proposed</c>-status FLG node that was
/// created when the proposal was raised). If no rejection handler is
/// registered for a given <see cref="ProposalType"/>, the dispatcher no-ops —
/// some proposal types have no cleanup to do.
/// </summary>
public interface IProposalRejectionHandler
{
    string ProposalType { get; }

    Task HandleRejectionAsync(
        AgentProposalDetail proposal,
        CancellationToken cancellationToken);
}

/// <summary>
/// Outcome record returned by an <see cref="IProposalHandler"/>. Carries
/// enough metadata for the approval response, audit log, and UI to describe
/// what happened without forcing the caller to re-query the domain.
///
/// Fields are intentionally minimal: ProposalType / ProposalId are not
/// included because the caller already has them from
/// <see cref="AgentProposalDetail"/>. No metadata dictionary — add one only
/// if a real consumer needs it.
/// </summary>
public sealed record ProposalHandlerResult(
    bool Applied,
    string? AppliedResourceType = null,
    Guid? AppliedResourceId = null,
    string? Message = null);

/// <summary>
/// Resolves the <see cref="IProposalHandler"/> registered for a given
/// <c>ProposalType</c> and invokes it. Throws
/// <see cref="NoProposalHandlerRegisteredException"/> when no handler is
/// registered — that error is treated as approval failure, not silent no-op.
/// </summary>
public interface IProposalDispatcher
{
    Task<ProposalHandlerResult> DispatchAsync(
        AgentProposalDetail proposal,
        CancellationToken cancellationToken);
}

/// <summary>
/// Symmetric counterpart for the dismissal path. No-ops (returns
/// successfully) when no <see cref="IProposalRejectionHandler"/> is
/// registered for the <c>ProposalType</c> — missing rejection handlers are
/// not an error, since not every proposal type has cleanup to do.
/// </summary>
public interface IProposalRejectionDispatcher
{
    Task DispatchAsync(
        AgentProposalDetail proposal,
        CancellationToken cancellationToken);
}

/// <summary>
/// Thrown by <see cref="IProposalDispatcher"/> when no
/// <see cref="IProposalHandler"/> is registered for the proposal's type.
/// Surfaced as HTTP 409 by the approve endpoint so the caller can tell
/// "approve failed because the system can't execute this type" apart from
/// the 500-class unexpected-failure path.
/// </summary>
public sealed class NoProposalHandlerRegisteredException : Exception
{
    public NoProposalHandlerRegisteredException(string proposalType)
        : base($"No IProposalHandler is registered for proposal type '{proposalType}'.")
    {
        ProposalType = proposalType;
    }

    public string ProposalType { get; }
}

/// <summary>
/// Thrown by <see cref="IProposalDispatcher"/> when a handler returns
/// <see cref="ProposalHandlerResult.Applied"/> = <c>false</c>. Carries the
/// handler's <see cref="ProposalHandlerResult.Message"/> so the caller can
/// explain the business reason. Surfaced as HTTP 422.
/// </summary>
public sealed class ProposalExecutionFailedException : Exception
{
    public ProposalExecutionFailedException(Guid proposalId, string? message)
        : base(message ?? $"Handler returned Applied = false for proposal '{proposalId}'.")
    {
        ProposalId = proposalId;
    }

    public Guid ProposalId { get; }
}
