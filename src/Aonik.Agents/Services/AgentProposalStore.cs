using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Agents.Services;

/// <summary>
/// Internal implementation of <see cref="IAgentProposalStore"/> backed by
/// <see cref="AgentsDbContext"/>. Domain consumers (Finance) talk to the
/// SharedKernel contract; the Agents runtime keeps the entity, DbContext,
/// and tenant filtering encapsulated.
/// </summary>
internal sealed class AgentProposalStore : IAgentProposalStore
{
    private readonly AgentsDbContext _agentsDbContext;
    private readonly ICurrentUserProvider _currentUserProvider;

    public AgentProposalStore(
        AgentsDbContext agentsDbContext,
        ICurrentUserProvider currentUserProvider)
    {
        _agentsDbContext = agentsDbContext;
        _currentUserProvider = currentUserProvider;
    }

    public async Task CreateManyAsync(
        IReadOnlyList<AgentProposalCreateRequest> requests,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if (requests.Count == 0) return;

        foreach (var request in requests)
        {
            var proposal = new Proposal
            {
                Id = request.Id,
                TenantId = request.TenantId,
                ProposalType = request.ProposalType,
                ProposedByAgentId = request.ProposedByAgentId,
                AiRunId = request.AiRunId ?? Guid.Empty,
                ImpactSummary = request.ImpactSummary,
                RiskTier = request.RiskTier,
                Status = ProposalStatus.Proposed,
                PayloadJson = request.PayloadJson,
            };
            _agentsDbContext.Proposals.Add(proposal);
        }

        await _agentsDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AgentProposalDetail?> GetByIdAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default)
    {
        var proposal = await _agentsDbContext.Proposals
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == proposalId, cancellationToken);

        return proposal is null ? null : Map(proposal);
    }

    public async Task<IReadOnlyList<AgentProposalDetail>> ListProposedAsync(
        string? proposalType,
        CancellationToken cancellationToken = default)
    {
        var query = _agentsDbContext.Proposals
            .AsNoTracking()
            .Where(item => item.Status == ProposalStatus.Proposed);

        if (!string.IsNullOrWhiteSpace(proposalType))
            query = query.Where(item => item.ProposalType == proposalType);

        var proposals = await query
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        return proposals.Select(Map).ToList();
    }

    public async Task ApproveAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default)
    {
        var proposal = await _agentsDbContext.Proposals
            .FirstOrDefaultAsync(item => item.Id == proposalId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Proposal '{proposalId}' not found in the current tenant.");

        if (proposal.Status != ProposalStatus.Proposed)
        {
            throw new InvalidOperationException(
                $"Proposal '{proposalId}' is in status '{proposal.Status}' and cannot be approved.");
        }

        proposal.Status = ProposalStatus.Approved;
        proposal.ApprovedAt = DateTime.UtcNow;
        proposal.ApprovedByUserId = _currentUserProvider.TryGetCurrentUserId(out var userId)
            ? userId
            : null;

        await _agentsDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RejectAsync(
        Guid proposalId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var proposal = await _agentsDbContext.Proposals
            .FirstOrDefaultAsync(item => item.Id == proposalId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Proposal '{proposalId}' not found in the current tenant.");

        if (proposal.Status != ProposalStatus.Proposed)
        {
            throw new InvalidOperationException(
                $"Proposal '{proposalId}' is in status '{proposal.Status}' and cannot be rejected.");
        }

        proposal.Status = ProposalStatus.Rejected;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            proposal.ImpactSummary = $"{proposal.ImpactSummary} Rejected: {reason.Trim()}";
        }

        await _agentsDbContext.SaveChangesAsync(cancellationToken);
    }

    private static AgentProposalDetail Map(Proposal proposal) =>
        new(
            Id: proposal.Id,
            TenantId: proposal.TenantId,
            ProposalType: proposal.ProposalType,
            Status: proposal.Status.ToString(),
            PayloadJson: proposal.PayloadJson,
            ImpactSummary: proposal.ImpactSummary);
}
