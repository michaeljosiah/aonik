using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Agents.Services.Workflows;

/// <summary>Stages a wake-up in the same Agents save as its approval decision.</summary>
internal sealed class WorkflowContinuationWriter(AgentsDbContext database, IClock clock)
{
    public async Task AfterDecisionAsync(ToolApprovalRequest request, CancellationToken cancellationToken = default)
    {
        if (request.WorkflowStepRunId is not { } stepId) return;
        var run = await (from step in database.WorkflowStepRuns.AsNoTracking()
                         join workflowRun in database.WorkflowRuns.AsNoTracking() on step.RunId equals workflowRun.Id
                         where step.TenantId == request.TenantId && workflowRun.TenantId == request.TenantId && step.Id == stepId
                         select workflowRun).SingleAsync(cancellationToken);
        // An in-flight continuation checks the decision before releasing its wait. This fallback
        // runs after its lease, so an approval arriving during that handoff cannot be lost.
        DateTime due = run.LeaseExpiresAt is { } lease && lease > clock.UtcNow ? lease : clock.UtcNow;
        database.EnqueueIntegrationEvent(new WorkflowContinuationRequestedEvent(Guid.NewGuid(), clock.UtcNow,
            run.TenantId, run.Id, run.Continuation, DateTime.SpecifyKind(due, DateTimeKind.Utc)));
    }

    public async Task AfterProposalAsync(Proposal proposal, CancellationToken cancellationToken = default)
    {
        if (proposal.WorkflowStepRunId is null) return;
        ToolApprovalRequest request = await database.ToolApprovalRequests.SingleAsync(
            request => request.TenantId == proposal.TenantId && request.ProposalId == proposal.Id
                && request.WorkflowStepRunId == proposal.WorkflowStepRunId, cancellationToken);
        if (proposal.Status is ProposalStatus.Applied or ProposalStatus.Failed or ProposalStatus.Rejected)
        {
            request.Status = proposal.Status == ProposalStatus.Rejected ? ToolApprovalRequestStatus.Rejected : ToolApprovalRequestStatus.Approved;
            request.DecidedByUserId = proposal.ApprovedByUserId;
            request.DecidedAt = proposal.ApprovedAt;
            request.ConsumedAt = clock.UtcNow;
        }
        await AfterDecisionAsync(request, cancellationToken);
    }

    public void AtExpiry(ToolApprovalRequest request, Entities.Workflows.WorkflowRun run)
    {
        if (request.WorkflowStepRunId is null) return;
        if (run.TenantId != request.TenantId || run.PendingStepRunId != request.WorkflowStepRunId)
            throw new InvalidOperationException("Approval expiry does not match the waiting workflow step.");
        database.EnqueueIntegrationEvent(new WorkflowContinuationRequestedEvent(request.Id, clock.UtcNow, request.TenantId,
            run.Id, run.Continuation, DateTime.SpecifyKind(request.ExpiresAt, DateTimeKind.Utc)));
    }
}
