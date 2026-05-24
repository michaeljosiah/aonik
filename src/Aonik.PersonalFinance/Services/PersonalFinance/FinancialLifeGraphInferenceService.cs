using System.Text.Json;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class FinancialLifeGraphInferenceService
{
    /// <summary>
    /// ProposalType marker stamped on every proposal this service creates.
    /// Used both at create-time (to make filters work) and at list-time (to
    /// scope the IAgentProposalStore.ListProposedAsync read to FLG entries).
    /// </summary>
    private const string FlgProposalType = "FinancialLifeGraphAnnotation";

    private readonly PersonalFinanceDbContext _financeDbContext;
    private readonly IAgentProposalStore _proposalStore;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IFinancialLifeGraphCacheInvalidator _cacheInvalidator;

    public FinancialLifeGraphInferenceService(
        PersonalFinanceDbContext financeDbContext,
        IAgentProposalStore proposalStore,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IFinancialLifeGraphCacheInvalidator cacheInvalidator)
    {
        _financeDbContext = financeDbContext;
        _proposalStore = proposalStore;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<IReadOnlyList<FinancialLifeGraphInferenceProposalResponse>> ProposeRecurringMerchantAnnotationsAsync(
        ProposeRecurringMerchantGraphAnnotationsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.AiRunId == Guid.Empty)
        {
            throw new ArgumentException("AiRunId is required.", nameof(request.AiRunId));
        }

        if (request.MinOccurrences < 2)
        {
            throw new ArgumentException("MinOccurrences must be at least 2.", nameof(request.MinOccurrences));
        }

        if (request.WithinDays <= 0)
        {
            throw new ArgumentException("WithinDays must be greater than 0.", nameof(request.WithinDays));
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();
        var cutoff = DateTime.UtcNow.Date.AddDays(-request.WithinDays);

        var groupedTransactions = await _financeDbContext.PersonalTransactions
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.UserId == userId
                && item.OccurredAt >= cutoff
                && item.Amount < 0
                && item.Merchant != null
                && item.Merchant != string.Empty)
            .GroupBy(item => item.Merchant!)
            .Select(group => new
            {
                Merchant = group.Key,
                Count = group.Count(),
                AverageAmount = group.Average(item => Math.Abs(item.Amount))
            })
            .Where(item => item.Count >= request.MinOccurrences)
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Merchant)
            .ToListAsync(cancellationToken);

        var existingDisplayNames = await _financeDbContext.FinancialLifeGraphNodes
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId)
            .Select(item => item.DisplayName)
            .ToListAsync(cancellationToken);

        var results = new List<FinancialLifeGraphInferenceProposalResponse>();
        var proposalRequests = new List<AgentProposalCreateRequest>();

        foreach (var group in groupedTransactions)
        {
            var displayName = $"Recurring merchant: {group.Merchant}";
            if (existingDisplayNames.Contains(displayName, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var nodeId = Guid.NewGuid();
            var edgeId = Guid.NewGuid();
            var proposalId = Guid.NewGuid();

            var metadataJson = JsonSerializer.Serialize(new
            {
                Merchant = group.Merchant,
                OccurrenceCount = group.Count,
                AverageAmount = decimal.Round(group.AverageAmount, 2),
                WindowDays = request.WithinDays,
                InferenceType = "RecurringMerchant"
            });

            var node = new FinancialLifeGraphNode
            {
                Id = nodeId,
                TenantId = tenantId,
                UserId = userId,
                NodeType = FinancialLifeGraphNodeTypes.InferredAnnotation,
                DisplayName = displayName,
                PropertiesJson = metadataJson,
                Status = FinancialLifeGraphEntityStatus.Proposed,
                IsInferred = true,
                AiRunId = request.AiRunId
            };

            var edge = new FinancialLifeGraphEdge
            {
                Id = edgeId,
                TenantId = tenantId,
                UserId = userId,
                FromNodeKey = FinancialLifeGraphFormatting.BuildNodeId("user", userId),
                Predicate = FinancialLifeGraphPredicates.AnnotatedAs,
                ToNodeKey = $"native-node:{nodeId:D}",
                PropertiesJson = metadataJson,
                Status = FinancialLifeGraphEntityStatus.Proposed,
                IsInferred = true,
                AiRunId = request.AiRunId
            };

            var proposalPayloadJson = JsonSerializer.Serialize(new
            {
                GraphNodeId = nodeId,
                GraphEdgeId = edgeId,
                NodeType = node.NodeType,
                DisplayName = displayName,
                MetadataJson = metadataJson,
                InferenceType = "RecurringMerchant"
            });

            var proposalRequest = new AgentProposalCreateRequest(
                Id: proposalId,
                TenantId: tenantId,
                ProposalType: FlgProposalType,
                ProposedByAgentId: Guid.Empty,
                AiRunId: request.AiRunId,
                ImpactSummary: $"Proposed graph annotation for recurring merchant {group.Merchant}.",
                RiskTier: "Low",
                PayloadJson: proposalPayloadJson);

            _financeDbContext.FinancialLifeGraphNodes.Add(node);
            _financeDbContext.FinancialLifeGraphEdges.Add(edge);
            proposalRequests.Add(proposalRequest);

            results.Add(new FinancialLifeGraphInferenceProposalResponse(
                proposalId,
                nodeId,
                edgeId,
                displayName,
                $"Detected {group.Count} recurring transactions for {group.Merchant} over the last {request.WithinDays} days.",
                group.Count,
                FinancialLifeGraphProposalStatus.Proposed));
        }

        if (results.Count > 0)
        {
            // Save the FLG nodes/edges first; if that fails the call aborts
            // before any agent-side rows are written. CreateManyAsync handles
            // its own SaveChangesAsync on the AgentsDbContext side.
            await _financeDbContext.SaveChangesAsync(cancellationToken);
            await _proposalStore.CreateManyAsync(proposalRequests, cancellationToken);
        }

        return results;
    }

    public async Task<IReadOnlyList<PendingFinancialLifeGraphProposalResponse>> ListPendingProposalsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();

        var nodes = await _financeDbContext.FinancialLifeGraphNodes
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId && item.Status == FinancialLifeGraphEntityStatus.Proposed && item.AiRunId.HasValue)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        var edgeLookup = await _financeDbContext.FinancialLifeGraphEdges
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId && item.Status == FinancialLifeGraphEntityStatus.Proposed && item.AiRunId.HasValue)
            .ToListAsync(cancellationToken);

        // Tenant scoping happens inside the IAgentProposalStore implementation;
        // the FLG type filter narrows the read to graph-annotation proposals.
        var proposals = await _proposalStore.ListProposedAsync(FlgProposalType, cancellationToken);

        return nodes.Select(node =>
        {
            var edge = edgeLookup.First(item => item.ToNodeKey == $"native-node:{node.Id:D}");
            var proposal = proposals.First(item => item.PayloadJson.Contains(node.Id.ToString(), StringComparison.OrdinalIgnoreCase));
            return new PendingFinancialLifeGraphProposalResponse(
                proposal.Id,
                node.Id,
                edge.Id,
                node.NodeType,
                node.DisplayName,
                edge.Predicate,
                FinancialLifeGraphProposalStatus.Proposed,
                node.AiRunId!.Value,
                node.PropertiesJson);
        }).ToList();
    }

    // Approval / rejection of FLG proposals is owned by the generic
    // /ai/proposals/{id}/approve|dismiss pipeline as of spec 030. The
    // domain-side behaviour previously inlined here now lives in
    // FinancialLifeGraphAnnotationProposalHandler and
    // FinancialLifeGraphAnnotationProposalRejectionHandler, resolved by
    // IProposalDispatcher when the user acts on the proposal.

    private Guid GetCurrentUserId()
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        return userId;
    }
}
