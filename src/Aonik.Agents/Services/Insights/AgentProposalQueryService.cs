using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.Agents.Services.Insights;

/// <summary>
/// SharedKernel implementation that lists pending proposals for the current
/// tenant (Status == <see cref="ProposalStatus.Proposed"/>), joined with the
/// proposing agent's display metadata. Tenant scoping is enforced by the
/// AgentsDbContext query filter on <c>Proposal</c>.
/// </summary>
internal sealed class AgentProposalQueryService : IAgentProposalQueryService
{
    private readonly AgentsDbContext _dbContext;

    public AgentProposalQueryService(AgentsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<AgentProposalSummary>> ListPendingAsync(
        int take = 5,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return Array.Empty<AgentProposalSummary>();
        }

        // Left-join to Agents so a proposal whose agent row is missing or
        // soft-deleted still appears in the list with a sensible fallback.
        var rows = await (
            from p in _dbContext.Proposals
            where p.Status == ProposalStatus.Proposed
            join a in _dbContext.Agents on p.ProposedByAgentId equals a.Id into agentJoin
            from agent in agentJoin.DefaultIfEmpty()
            orderby p.CreatedAt descending
            select new
            {
                p.Id,
                AgentName = agent != null ? agent.Name : "Unknown agent",
                AgentDomain = agent != null ? agent.Domain : string.Empty,
                AgentIconUrl = agent != null ? agent.IconUrl : null,
                p.Confidence,
                p.RiskTier,
                p.ImpactSummary,
                p.CreatedAt,
            })
            .Take(take)
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new AgentProposalSummary(
                Id: r.Id,
                AgentName: r.AgentName,
                AgentDomain: r.AgentDomain,
                AgentIconUrl: r.AgentIconUrl,
                Confidence: r.Confidence,
                Summary: r.ImpactSummary,
                Reason: null,
                RiskTier: r.RiskTier,
                CreatedAt: r.CreatedAt))
            .ToList();
    }
}
