using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.Agents.Services;

internal sealed class ProposalApprovalService : IProposalApprovalService
{
    private readonly AgentsDbContext _dbContext;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IClock _clock;
    private readonly IProposalDispatcher _dispatcher;
    private readonly IProposalRejectionDispatcher _rejectionDispatcher;

    public ProposalApprovalService(
        AgentsDbContext dbContext,
        ICurrentUserProvider currentUserProvider,
        IClock clock,
        IProposalDispatcher dispatcher,
        IProposalRejectionDispatcher rejectionDispatcher)
    {
        _dbContext = dbContext;
        _currentUserProvider = currentUserProvider;
        _clock = clock;
        _dispatcher = dispatcher;
        _rejectionDispatcher = rejectionDispatcher;
    }

    public async Task<ProposalDetailResponse?> GetByIdAsync(Guid proposalId, CancellationToken ct = default)
    {
        var row = await JoinedQuery()
            .FirstOrDefaultAsync(p => p.Proposal.Id == proposalId, ct);

        return row is null ? null : Map(row);
    }

    public async Task<ListProposalsResponse> ListPendingAsync(
        ListProposalsRequest request,
        CancellationToken cancellationToken = default)
    {
        var take = request.Take <= 0 ? 100 : Math.Min(request.Take, 500);

        var query = JoinedQuery().Where(r => r.Proposal.Status == ProposalStatus.Proposed);

        if (!string.IsNullOrWhiteSpace(request.ProposalType))
        {
            var proposalType = request.ProposalType.Trim();
            query = query.Where(r => r.Proposal.ProposalType == proposalType);
        }
        if (!string.IsNullOrWhiteSpace(request.AgentDomain))
        {
            var domain = request.AgentDomain.Trim();
            query = query.Where(r => r.AgentDomain == domain);
        }
        if (!string.IsNullOrWhiteSpace(request.RiskTier))
        {
            var risk = request.RiskTier.Trim();
            query = query.Where(r => r.Proposal.RiskTier == risk);
        }

        // Total reflects post-filter so a "type rail" badge can show real
        // counts; client paginates by passing different filters.
        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(r => r.Proposal.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new ProposalListItem(
                Id: r.Proposal.Id,
                ProposalType: r.Proposal.ProposalType,
                AgentName: r.AgentName,
                AgentDomain: r.AgentDomain,
                AgentIconUrl: r.AgentIconUrl,
                Confidence: r.Proposal.Confidence,
                Summary: r.Proposal.ImpactSummary,
                RiskTier: r.Proposal.RiskTier,
                CreatedAt: r.Proposal.CreatedAt))
            .ToList();

        return new ListProposalsResponse(items, total);
    }

    public async Task<ProposalDetailResponse> ApproveAsync(Guid proposalId, CancellationToken ct = default)
    {
        var proposal = await _dbContext.Proposals.FirstOrDefaultAsync(p => p.Id == proposalId, ct)
            ?? throw new KeyNotFoundException($"Proposal {proposalId} not found.");

        if (proposal.Status != ProposalStatus.Proposed)
        {
            throw new InvalidOperationException(
                $"Proposal {proposalId} is already {proposal.Status} and cannot be approved.");
        }

        // Spec 032 §8.1: High-risk (money) proposals follow Proposed → Approved →
        // Applied/Failed, where Approved persists the human decision independently
        // of execution and a failed dispatch is TERMINAL (no revert). Low-stakes
        // types (e.g. FLG) keep Spec 030's revert-on-failure so "click Approve
        // again" stays the retry path. Approved is now the explicit intermediate
        // that lets one status field support both recovery models.
        var isHighRisk = string.Equals(proposal.RiskTier, "High", StringComparison.OrdinalIgnoreCase);

        proposal.Status = ProposalStatus.Approved;
        proposal.ApprovedByUserId = _currentUserProvider.GetCurrentUserId();
        proposal.ApprovedAt = _clock.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        ProposalHandlerResult result;
        try
        {
            result = await _dispatcher.DispatchAsync(ToDetail(proposal), ct);
        }
        catch
        {
            // High: a money dispatch whose outcome is unknown must not return to
            // Proposed — re-approving it could double-move funds. Land in Failed
            // (terminal); recovery is an explicit new proposal once the operator
            // confirms the prior attempt with the partner (Spec 032 §8.1).
            // Non-High: best-effort revert — the proposal row and the handler's
            // domain mutations live in different DbContexts (not a distributed
            // transaction), so handlers must be idempotent (Spec 030 §6.1).
            if (isHighRisk)
            {
                await MarkFailedAsync(proposal);
            }
            else
            {
                await RevertToProposedAsync(proposal);
            }
            throw;
        }

        if (!result.Applied)
        {
            // Handler signalled an expected business failure (e.g. payload
            // references a deleted entity). High → terminal Failed; non-High →
            // revert. Either way surface as 422 to distinguish from the 500-class
            // path above.
            if (isHighRisk)
            {
                await MarkFailedAsync(proposal);
            }
            else
            {
                await RevertToProposedAsync(proposal);
            }
            throw new ProposalExecutionFailedException(proposal.Id, result.Message);
        }

        if (isHighRisk)
        {
            // Execution confirmed: Approved → Applied (terminal success).
            proposal.Status = ProposalStatus.Applied;
            await _dbContext.SaveChangesAsync(ct);
        }

        var row = await JoinedQuery().FirstAsync(p => p.Proposal.Id == proposalId, ct);
        return Map(row, result);
    }

    public async Task<ProposalDetailResponse> DismissAsync(Guid proposalId, CancellationToken ct = default)
    {
        var proposal = await _dbContext.Proposals.FirstOrDefaultAsync(p => p.Id == proposalId, ct)
            ?? throw new KeyNotFoundException($"Proposal {proposalId} not found.");

        if (proposal.Status != ProposalStatus.Proposed)
        {
            throw new InvalidOperationException(
                $"Proposal {proposalId} is already {proposal.Status} and cannot be dismissed.");
        }

        proposal.Status = ProposalStatus.Rejected;
        // V1 reuses ApprovedByUserId / ApprovedAt to record the dismisser and
        // dismissal time (spec 030 §5.6 — schema deliberately unchanged in v1).
        // Anyone reading a Rejected row should interpret these as "the user who
        // made the Approve-or-Dismiss decision."
        proposal.ApprovedByUserId = _currentUserProvider.GetCurrentUserId();
        proposal.ApprovedAt = _clock.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        // Rejection dispatch is fire-and-surface: if cleanup fails, the proposal
        // stays Rejected (we don't un-dismiss the user's explicit intent) but
        // the exception bubbles so the caller knows manual cleanup is required.
        await _rejectionDispatcher.DispatchAsync(ToDetail(proposal), ct);

        var row = await JoinedQuery().FirstAsync(p => p.Proposal.Id == proposalId, ct);
        return Map(row);
    }

    private async Task RevertToProposedAsync(Proposal proposal)
    {
        proposal.Status = ProposalStatus.Proposed;
        proposal.ApprovedByUserId = null;
        proposal.ApprovedAt = null;
        // CancellationToken.None — the caller's token may already be cancelled
        // but we still need to roll back the row we just committed.
        await _dbContext.SaveChangesAsync(CancellationToken.None);
    }

    private async Task MarkFailedAsync(Proposal proposal)
    {
        // Terminal failure for a High-risk proposal. The approver stamp is kept —
        // a human DID approve; only execution failed — so the audit trail records
        // who authorised the attempt. CancellationToken.None for the same reason
        // as RevertToProposedAsync: we must persist the terminal state regardless.
        proposal.Status = ProposalStatus.Failed;
        await _dbContext.SaveChangesAsync(CancellationToken.None);
    }

    private static AgentProposalDetail ToDetail(Proposal proposal) =>
        new(
            Id: proposal.Id,
            TenantId: proposal.TenantId,
            ProposalType: proposal.ProposalType,
            Status: proposal.Status.ToString(),
            PayloadJson: proposal.PayloadJson,
            ImpactSummary: proposal.ImpactSummary);

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

    private static ProposalDetailResponse Map(JoinedRow row, ProposalHandlerResult? result = null) => new(
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
        CreatedAt: row.Proposal.CreatedAt,
        AppliedResourceType: result?.AppliedResourceType,
        AppliedResourceId: result?.AppliedResourceId,
        AppliedMessage: result?.Message);

    private sealed class JoinedRow
    {
        public Proposal Proposal { get; set; } = null!;
        public string AgentName { get; set; } = string.Empty;
        public string AgentDomain { get; set; } = string.Empty;
        public string? AgentIconUrl { get; set; }
    }
}
