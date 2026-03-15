using System.Text.Json;
using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class FinancialLifeGraphInferenceService
{
    private readonly FinanceDbContext _financeDbContext;
    private readonly AgentsDbContext _agentsDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IFinancialLifeGraphCacheInvalidator _cacheInvalidator;

    public FinancialLifeGraphInferenceService(
        FinanceDbContext financeDbContext,
        AgentsDbContext agentsDbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IFinancialLifeGraphCacheInvalidator cacheInvalidator)
    {
        _financeDbContext = financeDbContext;
        _agentsDbContext = agentsDbContext;
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

            var proposal = new Proposal
            {
                Id = proposalId,
                TenantId = tenantId,
                ProposalType = "FinancialLifeGraphAnnotation",
                ProposedByAgentId = Guid.Empty,
                AiRunId = request.AiRunId,
                ImpactSummary = $"Proposed graph annotation for recurring merchant {group.Merchant}.",
                RiskTier = "Low",
                Status = ProposalStatus.Proposed,
                PayloadJson = proposalPayloadJson
            };

            _financeDbContext.FinancialLifeGraphNodes.Add(node);
            _financeDbContext.FinancialLifeGraphEdges.Add(edge);
            _agentsDbContext.Proposals.Add(proposal);

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
            await _financeDbContext.SaveChangesAsync(cancellationToken);
            await _agentsDbContext.SaveChangesAsync(cancellationToken);
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

        var proposals = await _agentsDbContext.Proposals
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.Status == ProposalStatus.Proposed)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

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

    public async Task ApproveProposalAsync(Guid proposalId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();

        var proposal = await _agentsDbContext.Proposals
            .FirstOrDefaultAsync(item => item.Id == proposalId && item.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Financial life graph proposal record not found.");

        if (proposal.Status != ProposalStatus.Proposed)
        {
            throw new InvalidOperationException("Only proposed graph annotations can be approved.");
        }

        using var payload = JsonDocument.Parse(proposal.PayloadJson);
        var graphNodeId = payload.RootElement.GetProperty("GraphNodeId").GetGuid();

        var node = await _financeDbContext.FinancialLifeGraphNodes
            .FirstOrDefaultAsync(item => item.Id == graphNodeId && item.TenantId == tenantId && item.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Financial life graph proposal not found.");

        var edge = await _financeDbContext.FinancialLifeGraphEdges
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.UserId == userId && item.ToNodeKey == $"native-node:{node.Id:D}", cancellationToken)
            ?? throw new InvalidOperationException("Financial life graph proposal edge not found.");

        node.Status = FinancialLifeGraphEntityStatus.Active;
        edge.Status = FinancialLifeGraphEntityStatus.Active;
        proposal.Status = ProposalStatus.Approved;
        proposal.ApprovedAt = DateTime.UtcNow;
        proposal.ApprovedByUserId = userId;
        await _financeDbContext.SaveChangesAsync(cancellationToken);
        await _agentsDbContext.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);
    }

    public async Task RejectProposalAsync(Guid proposalId, string? reason = null, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var proposal = await _agentsDbContext.Proposals
            .FirstOrDefaultAsync(item => item.Id == proposalId && item.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Financial life graph proposal record not found.");

        if (proposal.Status != ProposalStatus.Proposed)
        {
            throw new InvalidOperationException("Only proposed graph annotations can be rejected.");
        }

        using var payload = JsonDocument.Parse(proposal.PayloadJson);
        var graphNodeId = payload.RootElement.GetProperty("GraphNodeId").GetGuid();
        var graphEdgeId = payload.RootElement.GetProperty("GraphEdgeId").GetGuid();

        var node = await _financeDbContext.FinancialLifeGraphNodes
            .FirstOrDefaultAsync(item => item.Id == graphNodeId && item.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Financial life graph proposal node not found.");

        var edge = await _financeDbContext.FinancialLifeGraphEdges
            .FirstOrDefaultAsync(item => item.Id == graphEdgeId && item.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Financial life graph proposal edge not found.");

        node.Status = FinancialLifeGraphEntityStatus.Rejected;
        edge.Status = FinancialLifeGraphEntityStatus.Rejected;
        proposal.Status = ProposalStatus.Rejected;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            proposal.ImpactSummary = $"{proposal.ImpactSummary} Rejected: {reason.Trim()}";
        }

        await _financeDbContext.SaveChangesAsync(cancellationToken);
        await _agentsDbContext.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);
    }

    private Guid GetCurrentUserId()
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        return userId;
    }
}
