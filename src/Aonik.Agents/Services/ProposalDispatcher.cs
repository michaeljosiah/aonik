using System.Diagnostics;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Agents.Services;

/// <summary>
/// Resolves the <see cref="IProposalHandler"/> registered for a given
/// proposal type and invokes it. Wraps the handler call in an
/// <c>aonik.proposals.dispatch</c> OpenTelemetry span tagged with
/// <c>proposal.type</c>, <c>proposal.id</c>, <c>proposal.applied</c>,
/// <c>proposal.applied_resource_type</c>, and
/// <c>proposal.applied_resource_id</c>.
/// <para>
/// Spec 097 §12.1: the approve endpoint lives in the (core) Agents module, so the HTTP gate never
/// sees it — this is the one remaining execution seam for a disabled module's tools. Before the
/// handler runs, its module (from the <see cref="AonikModuleAttribute"/> of the handler's assembly)
/// is checked against the proposal's tenant; a handler from a known, non-core module that is off
/// is never invoked and <see cref="ModuleDisabledException"/> is thrown instead (the approval
/// service lands the proposal in <c>Failed</c> and the HTTP layer answers 403 <c>module.disabled</c>).
/// Core and unattributed handlers, and hosts without an <see cref="IModuleEnablementReader"/>,
/// dispatch as before.
/// </para>
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
            await EnsureHandlerModuleEnabledAsync(handler, proposal, activity, cancellationToken).ConfigureAwait(false);

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

    /// <summary>The catalogue id of the module whose assembly declares <paramref name="handlerType"/>, when that module can be switched off.</summary>
    internal static string? GatedModuleId(Type handlerType)
    {
        var moduleId = ModuleCatalog.TryGetModuleId(handlerType);
        return moduleId is not null && ModuleCatalog.IsKnown(moduleId) && !ModuleCatalog.CoreIds.Contains(moduleId)
            ? moduleId
            : null;
    }

    private async Task EnsureHandlerModuleEnabledAsync(
        IProposalHandler handler,
        AgentProposalDetail proposal,
        Activity? activity,
        CancellationToken cancellationToken)
    {
        var moduleId = GatedModuleId(handler.GetType());
        if (moduleId is null)
        {
            return;
        }

        activity?.SetTag("proposal.module", moduleId);

        var reader = _services.GetService<IModuleEnablementReader>();
        if (reader is null)
        {
            return;
        }

        var enablement = await reader.GetAsync(proposal.TenantId, cancellationToken).ConfigureAwait(false);
        if (enablement.IsEnabled(moduleId))
        {
            return;
        }

        activity?.SetTag("proposal.module_disabled", true);
        throw new ModuleDisabledException(moduleId);
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
