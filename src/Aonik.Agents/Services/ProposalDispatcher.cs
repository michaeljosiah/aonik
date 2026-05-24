using System.Diagnostics;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Agents.Services;

/// <summary>
/// Resolves the <see cref="IProposalHandler"/> registered for a given
/// proposal type and invokes it. Wraps the handler call in an
/// <c>aonik.proposals.dispatch</c> OpenTelemetry span tagged with
/// <c>proposal.type</c>, <c>proposal.id</c>, <c>proposal.applied</c>,
/// <c>proposal.applied_resource_type</c>, and
/// <c>proposal.applied_resource_id</c>.
/// </summary>
internal sealed class ProposalDispatcher : IProposalDispatcher
{
    private readonly IServiceProvider _services;

    public ProposalDispatcher(IServiceProvider services) => _services = services;

    public async Task<ProposalHandlerResult> DispatchAsync(
        AgentProposalDetail proposal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        var handler = _services.GetKeyedService<IProposalHandler>(proposal.ProposalType)
            ?? throw new NoProposalHandlerRegisteredException(proposal.ProposalType);

        using var activity = AiTelemetry.ActivitySource.StartActivity(
            "aonik.proposals.dispatch",
            ActivityKind.Internal);
        activity?.SetTag("proposal.type", proposal.ProposalType);
        activity?.SetTag("proposal.id", proposal.Id);

        try
        {
            var result = await handler.HandleAsync(proposal, cancellationToken).ConfigureAwait(false);

            activity?.SetTag("proposal.applied", result.Applied);
            if (result.AppliedResourceType is not null)
            {
                activity?.SetTag("proposal.applied_resource_type", result.AppliedResourceType);
            }
            if (result.AppliedResourceId is not null)
            {
                activity?.SetTag("proposal.applied_resource_id", result.AppliedResourceId.Value);
            }

            return result;
        }
        catch (Exception ex)
        {
            AiTelemetry.MarkError(activity, ex);
            throw;
        }
    }
}

/// <summary>
/// Resolves the optional <see cref="IProposalRejectionHandler"/> registered
/// for a given proposal type and invokes it. Missing rejection handlers are
/// not an error — many proposal types have no cleanup to do; only
/// approval-side dispatch treats a missing handler as fatal.
///
/// Wraps the call in an <c>aonik.proposals.rejection_dispatch</c> span
/// tagged with <c>proposal.type</c>, <c>proposal.id</c>, and
/// <c>proposal.cleanup_handler_registered</c>.
/// </summary>
internal sealed class ProposalRejectionDispatcher : IProposalRejectionDispatcher
{
    private readonly IServiceProvider _services;

    public ProposalRejectionDispatcher(IServiceProvider services) => _services = services;

    public async Task DispatchAsync(
        AgentProposalDetail proposal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        var handler = _services.GetKeyedService<IProposalRejectionHandler>(proposal.ProposalType);

        using var activity = AiTelemetry.ActivitySource.StartActivity(
            "aonik.proposals.rejection_dispatch",
            ActivityKind.Internal);
        activity?.SetTag("proposal.type", proposal.ProposalType);
        activity?.SetTag("proposal.id", proposal.Id);
        activity?.SetTag("proposal.cleanup_handler_registered", handler is not null);

        if (handler is null)
        {
            return;
        }

        try
        {
            await handler.HandleRejectionAsync(proposal, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AiTelemetry.MarkError(activity, ex);
            throw;
        }
    }
}
