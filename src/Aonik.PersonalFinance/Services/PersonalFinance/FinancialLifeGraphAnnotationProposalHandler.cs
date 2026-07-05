using System.Text.Json;
using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.PersonalFinance.Services;

/// <summary>
/// Spec 030 — generic-dispatcher handler that applies an approved
/// <c>FinancialLifeGraphAnnotation</c> proposal by flipping the corresponding
/// FLG node and edge to <see cref="FinancialLifeGraphEntityStatus.Active"/>.
///
/// Idempotency: when the node and edge are already <c>Active</c>, the handler
/// reports success without re-saving — matches the spec's "retry path is
/// 'user clicks Approve again'" guarantee.
///
/// Expected business failure: when the payload references a node that no
/// longer exists in the tenant (deleted by a sibling cleanup, race with
/// another approval), the handler returns <c>Applied = false</c> so the
/// dispatcher surfaces HTTP 422 instead of treating it as a 500-class error.
/// </summary>
internal sealed class FinancialLifeGraphAnnotationProposalHandler : IProposalHandler
{
    public const string ProposalTypeKey = "FinancialLifeGraphAnnotation";
    private const string AppliedResourceTypeName = "FinancialLifeGraphNode";

    private readonly PersonalFinanceDbContext _financeDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IFinancialLifeGraphCacheInvalidator _cacheInvalidator;

    public FinancialLifeGraphAnnotationProposalHandler(
        PersonalFinanceDbContext financeDbContext,
        ITenantProvider tenantProvider,
        IFinancialLifeGraphCacheInvalidator cacheInvalidator)
    {
        _financeDbContext = financeDbContext;
        _tenantProvider = tenantProvider;
        _cacheInvalidator = cacheInvalidator;
    }

    public string ProposalType => ProposalTypeKey;

    public async Task<ProposalHandlerResult> HandleAsync(
        AgentProposalDetail proposal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        var tenantId = _tenantProvider.GetCurrentTenantId();

        Guid graphNodeId;
        try
        {
            using var payload = JsonDocument.Parse(proposal.PayloadJson);
            graphNodeId = payload.RootElement.GetProperty("GraphNodeId").GetGuid();
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return new ProposalHandlerResult(
                Applied: false,
                Message: $"Proposal payload is missing or has an invalid GraphNodeId: {ex.Message}");
        }

        var node = await _financeDbContext.FinancialLifeGraphNodes
            .FirstOrDefaultAsync(item => item.Id == graphNodeId && item.TenantId == tenantId, cancellationToken);

        if (node is null)
        {
            return new ProposalHandlerResult(
                Applied: false,
                Message: $"Financial life graph node {graphNodeId} no longer exists for this tenant.");
        }

        var edge = await _financeDbContext.FinancialLifeGraphEdges
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ToNodeKey == $"native-node:{node.Id:D}", cancellationToken);

        if (edge is null)
        {
            return new ProposalHandlerResult(
                Applied: false,
                Message: $"Financial life graph edge for node {graphNodeId} no longer exists for this tenant.");
        }

        // Idempotent re-apply: if both rows are already Active a second
        // approval is a no-op success, not a duplicate-key failure. The retry
        // path "user clicks Approve again" must converge on the same outcome.
        var alreadyActive = node.Status == FinancialLifeGraphEntityStatus.Active
            && edge.Status == FinancialLifeGraphEntityStatus.Active;

        if (!alreadyActive)
        {
            node.Status = FinancialLifeGraphEntityStatus.Active;
            edge.Status = FinancialLifeGraphEntityStatus.Active;
            await _financeDbContext.SaveChangesAsync(cancellationToken);
        }

        await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);

        return new ProposalHandlerResult(
            Applied: true,
            AppliedResourceType: AppliedResourceTypeName,
            AppliedResourceId: graphNodeId,
            Message: alreadyActive ? "Node and edge were already active." : null);
    }
}

/// <summary>
/// Spec 030 — generic-dispatcher cleanup that fires when a
/// <c>FinancialLifeGraphAnnotation</c> proposal is dismissed: flips the
/// proposed FLG node and edge to <see cref="FinancialLifeGraphEntityStatus.Rejected"/>
/// so the graph does not retain orphaned <em>Proposed</em>-state inferences.
///
/// Required for v1 — without this handler, removing the bespoke
/// <c>/personal-finance/graph/proposals/{id}/reject</c> endpoint would
/// silently break the cleanup that the old service used to do.
/// </summary>
internal sealed class FinancialLifeGraphAnnotationProposalRejectionHandler : IProposalRejectionHandler
{
    private readonly PersonalFinanceDbContext _financeDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IFinancialLifeGraphCacheInvalidator _cacheInvalidator;

    public FinancialLifeGraphAnnotationProposalRejectionHandler(
        PersonalFinanceDbContext financeDbContext,
        ITenantProvider tenantProvider,
        IFinancialLifeGraphCacheInvalidator cacheInvalidator)
    {
        _financeDbContext = financeDbContext;
        _tenantProvider = tenantProvider;
        _cacheInvalidator = cacheInvalidator;
    }

    public string ProposalType => FinancialLifeGraphAnnotationProposalHandler.ProposalTypeKey;

    public async Task HandleRejectionAsync(
        AgentProposalDetail proposal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        var tenantId = _tenantProvider.GetCurrentTenantId();

        using var payload = JsonDocument.Parse(proposal.PayloadJson);
        var graphNodeId = payload.RootElement.GetProperty("GraphNodeId").GetGuid();
        var graphEdgeId = payload.RootElement.GetProperty("GraphEdgeId").GetGuid();

        var node = await _financeDbContext.FinancialLifeGraphNodes
            .FirstOrDefaultAsync(item => item.Id == graphNodeId && item.TenantId == tenantId, cancellationToken);

        var edge = await _financeDbContext.FinancialLifeGraphEdges
            .FirstOrDefaultAsync(item => item.Id == graphEdgeId && item.TenantId == tenantId, cancellationToken);

        // Best-effort cleanup: if either row was already deleted or rejected
        // by a sibling flow we simply skip it. Throwing here would force the
        // dismiss endpoint into a 500 even though the user's intent was met.
        var changed = false;
        if (node is not null && node.Status != FinancialLifeGraphEntityStatus.Rejected)
        {
            node.Status = FinancialLifeGraphEntityStatus.Rejected;
            changed = true;
        }
        if (edge is not null && edge.Status != FinancialLifeGraphEntityStatus.Rejected)
        {
            edge.Status = FinancialLifeGraphEntityStatus.Rejected;
            changed = true;
        }

        if (changed)
        {
            await _financeDbContext.SaveChangesAsync(cancellationToken);
        }

        await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);
    }
}
