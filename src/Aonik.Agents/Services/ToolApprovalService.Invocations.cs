using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Entities;
using Aonik.Agents.Services.Connectors;
using Aonik.Agents.Services.Workflows;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Agents.Services;

internal sealed partial class ToolApprovalService
{
    private async Task<ToolGateOutcome?> GateExistingInvocationAsync(ToolGateContext context, Guid tenantId, Guid userId, string hash,
        DateTime now, CancellationToken cancellationToken)
    {
        ToolApprovalRequest? request = await _workflowDatabase!.ToolApprovalRequests.SingleOrDefaultAsync(request => request.TenantId == tenantId
            && request.InvocationKey == context.InvocationKey, cancellationToken);
        if (request is null) return null;
        await _workflowDatabase.Entry(request).ReloadAsync(cancellationToken);
        if (request.RequestingUserId != userId || request.ThreadId != _roomContext?.RoomId?.ToString() || request.WorkflowStepRunId is not null
            || request.ToolName != context.ToolName || request.ArgsHash != hash || request.RiskTier != context.Options.Tier.ToString())
            throw new WorkflowConflictException("This invocation key is bound to different arguments or authority. Reuse the original reviewed request.");
        if (_roomApprovalAuthority is not null && !await _roomApprovalAuthority.CanDecideAsync(request.AiRunId, cancellationToken))
            return new(ToolGateDecision.Refused, request.Id, request.ProposalId, Reason: "Current room authority does not permit this invocation.");
        if (request.ExecutionJson is { } execution)
        {
            ConnectorMutationJournal journal = ConnectorJson.Read<ConnectorMutationJournal>(execution);
            if (journal.SchemaVersion != 1) throw new WorkflowConflictException("The invocation's execution evidence is incompatible.");
            return journal.Status == "Completed" && journal.Result is { } result
                ? new(ToolGateDecision.Completed, request.Id, request.ProposalId, Result: result, Reason: "This is the recorded provider receipt; no new mutation ran.")
                : new(ToolGateDecision.NeedsAttention, request.Id, request.ProposalId, Reason: "This operation is in progress or needs provider reconciliation. It was not dispatched again.");
        }
        if (request.ExpiresAt <= now || request.Status is ToolApprovalRequestStatus.Rejected or ToolApprovalRequestStatus.Cancelled or ToolApprovalRequestStatus.Expired)
            return new(ToolGateDecision.Refused, request.Id, request.ProposalId, Reason: "The original invocation expired or was refused. Review its status before creating another operation.");
        if (context.Options.Tier == ToolApprovalTier.High)
        {
            if (request.ProposalId is not { } proposalId) throw new WorkflowConflictException("The invocation has no durable proposal.");
            Proposal proposal = await _workflowDatabase.Proposals.AsNoTracking().SingleAsync(proposal => proposal.TenantId == tenantId && proposal.Id == proposalId, cancellationToken);
            if (proposal.ProposalType != context.Options.ProposalType) throw new WorkflowConflictException("The invocation's action contract changed.");
            return proposal.Status is ProposalStatus.Failed or ProposalStatus.Applied or ProposalStatus.Rejected
                ? new(ToolGateDecision.NeedsAttention, request.Id, proposal.Id, Reason: "Inspect the existing proposal's execution evidence; no new proposal was created.")
                : new(ToolGateDecision.Queued, request.Id, proposal.Id, request.ActionKind);
        }
        if (request.Status == ToolApprovalRequestStatus.Approved)
        {
            // Consumption alone is not provider execution. The same durable journal must claim dispatch before I/O.
            if (request.ConsumedAt is null)
            {
                request.ConsumedAt = now;
                try { await _workflowDatabase.SaveChangesAsync(cancellationToken); }
                catch (DbUpdateConcurrencyException) { throw new WorkflowConflictException("Another invocation owns this approval. Reload its recorded status."); }
            }
            return new(ToolGateDecision.ApprovedInline, request.Id, Summary: request.ActionKind);
        }
        return new(ToolGateDecision.PendingApproval, request.Id, Summary: request.ActionKind);
    }
}
