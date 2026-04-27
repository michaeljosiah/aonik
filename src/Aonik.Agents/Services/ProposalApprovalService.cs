using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Agents.Services;

internal sealed class ProposalApprovalService : IProposalApprovalService
{
    private readonly AgentsDbContext _dbContext;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IClock _clock;

    public ProposalApprovalService(
        AgentsDbContext dbContext,
        ICurrentUserProvider currentUserProvider,
        IClock clock)
    {
        _dbContext = dbContext;
        _currentUserProvider = currentUserProvider;
        _clock = clock;
    }

    public async Task<ProposalDetailResponse?> GetByIdAsync(Guid proposalId, CancellationToken ct = default)
    {
        var row = await JoinedQuery()
            .FirstOrDefaultAsync(p => p.Proposal.Id == proposalId, ct);

        return row is null ? null : Map(row);
    }

    public Task<ProposalDetailResponse> ApproveAsync(Guid proposalId, CancellationToken ct = default) =>
        TransitionAsync(proposalId, ProposalStatus.Approved, ct);

    public Task<ProposalDetailResponse> DismissAsync(Guid proposalId, CancellationToken ct = default) =>
        TransitionAsync(proposalId, ProposalStatus.Rejected, ct);

    private async Task<ProposalDetailResponse> TransitionAsync(
        Guid proposalId, ProposalStatus next, CancellationToken ct)
    {
        var proposal = await _dbContext.Proposals.FirstOrDefaultAsync(p => p.Id == proposalId, ct)
            ?? throw new KeyNotFoundException($"Proposal {proposalId} not found.");

        if (proposal.Status != ProposalStatus.Proposed)
        {
            throw new InvalidOperationException(
                $"Proposal {proposalId} is already {proposal.Status} and cannot be transitioned to {next}.");
        }

        proposal.Status = next;
        proposal.ApprovedByUserId = _currentUserProvider.GetCurrentUserId();
        proposal.ApprovedAt = _clock.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        var row = await JoinedQuery().FirstAsync(p => p.Proposal.Id == proposalId, ct);
        return Map(row);
    }

    // Single-source-of-truth join used by both the read and the post-mutation
    // re-read so the response shape is identical across endpoints.
    private IQueryable<JoinedRow> JoinedQuery() =>
        from p in _dbContext.Proposals
        join a in _dbContext.Agents on p.ProposedByAgentId equals a.Id into agentJoin
        from agent in agentJoin.DefaultIfEmpty()
        select new JoinedRow
        {
            Proposal = p,
            AgentName = agent != null ? agent.Name : "Unknown agent",
            AgentDomain = agent != null ? agent.Domain : string.Empty,
            AgentIconUrl = agent != null ? agent.IconUrl : null,
        };

    private static ProposalDetailResponse Map(JoinedRow row) => new(
        Id: row.Proposal.Id,
        ProposalType: row.Proposal.ProposalType,
        ProposedByAgentId: row.Proposal.ProposedByAgentId,
        AgentName: row.AgentName,
        AgentDomain: row.AgentDomain,
        AgentIconUrl: row.AgentIconUrl,
        AiRunId: row.Proposal.AiRunId,
        Summary: row.Proposal.ImpactSummary,
        RiskTier: row.Proposal.RiskTier,
        Confidence: row.Proposal.Confidence,
        Status: row.Proposal.Status.ToString(),
        ApprovedByUserId: row.Proposal.ApprovedByUserId,
        ApprovedAt: row.Proposal.ApprovedAt,
        PayloadJson: row.Proposal.PayloadJson,
        CreatedAt: row.Proposal.CreatedAt);

    private sealed class JoinedRow
    {
        public Proposal Proposal { get; set; } = null!;
        public string AgentName { get; set; } = string.Empty;
        public string AgentDomain { get; set; } = string.Empty;
        public string? AgentIconUrl { get; set; }
    }
}
