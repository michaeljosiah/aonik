using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowApprovalReader(
    AgentsDbContext database, ITenantProvider tenantProvider, WorkflowInvocationContext context,
    WorkflowReviewerAuthorization reviewers, IClock clock, IAutomationIdentityAuthorization? automation = null,
    WorkflowExecutionAuthorization? execution = null) : IWorkflowApprovalReader
{
    public async Task<ApprovedWorkflowProposal> RequireApprovedProposalAsync(Guid proposalId, WorkflowActionDescriptor action,
        CancellationToken cancellationToken = default)
    {
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        if (execution is null || action.Approval is not { Tier: SharedKernel.Abstractions.Agents.ToolApprovalTier.High } policy)
            throw new PermissionDeniedException("Workflow.Run");
        Proposal proposal = await database.Proposals.AsNoTracking().SingleOrDefaultAsync(proposal => proposal.TenantId == tenantId && proposal.Id == proposalId, cancellationToken)
            ?? throw new PermissionDeniedException(action.RequiredPermission);
        var step = await database.WorkflowStepRuns.AsNoTracking().SingleOrDefaultAsync(step => step.TenantId == tenantId
            && step.Id == proposal.WorkflowStepRunId, cancellationToken) ?? throw new PermissionDeniedException(action.RequiredPermission);
        var run = await database.WorkflowRuns.AsNoTracking().SingleAsync(run => run.TenantId == tenantId && run.Id == step.RunId, cancellationToken);
        ToolApprovalRequest request = await database.ToolApprovalRequests.AsNoTracking().SingleOrDefaultAsync(request => request.TenantId == tenantId
            && request.Id == step.ApprovalRequestId && request.ProposalId == proposalId, cancellationToken) ?? throw new PermissionDeniedException(action.RequiredPermission);
        JsonElement arguments = JsonSerializer.Deserialize<JsonElement>(step.ArgumentsJson ?? "null");
        if (arguments.ValueKind != JsonValueKind.Object) throw new PermissionDeniedException(action.RequiredPermission);
        string hash = SharedKernel.Abstractions.Agents.ToolArgumentsHash.Compute(arguments.EnumerateObject().ToDictionary(property => property.Name, property => (object?)property.Value));
        JsonElement payload = JsonSerializer.Deserialize<JsonElement>(proposal.PayloadJson);
        if (payload.ValueKind != JsonValueKind.Object) throw new PermissionDeniedException(action.RequiredPermission);
        string payloadHash = SharedKernel.Abstractions.Agents.ToolArgumentsHash.Compute(payload.EnumerateObject().ToDictionary(property => property.Name, property => (object?)property.Value));
        if (proposal.Status != ProposalStatus.Approved || proposal.ProposalType != policy.ProposalType || proposal.RiskTier != "High"
            || proposal.ApprovedByUserId is not { } reviewer || proposal.ApprovedAt is null || request.DecidedByUserId != reviewer
            || request.DecidedAt is null || request.ConsumedAt is null || request.Status != ToolApprovalRequestStatus.Approved || request.RiskTier != "High"
            || request.ExpiresAt <= clock.UtcNow || request.WorkflowStepRunId != step.Id || request.ToolName != action.ToolName
            || step.OperationId == Guid.Empty || step.ProposalId != proposal.Id || step.Status != "WaitingForApproval" || run.PendingStepRunId != step.Id
            || step.ActionKey != action.Key || step.ActionVersion != action.Version || step.RequiredPermission != action.RequiredPermission
            || hash != payloadHash || hash != request.ArgsHash || hash != step.ArgumentsHash || request.PreviewJson is null
            || run.Status is not (Entities.Workflows.WorkflowRunStatuses.WaitingForApproval or Entities.Workflows.WorkflowRunStatuses.Running))
            throw new PermissionDeniedException(action.RequiredPermission, "The exact workflow proposal has no current durable human approval.");
        await execution.RequireSubjectModuleAsync(run.SubjectType!, cancellationToken);
        await execution.RequireEnabledAsync(cancellationToken);
        await reviewers.RequireAsync(request, reviewer, cancellationToken);
        var invocation = new WorkflowActionInvocation(run.Id, step.Id, step.OperationId, WorkflowExecutionAuthorization.Actor(run), run.SubjectType!,
            run.SubjectId!.Value, step.ExpectedSubjectVersion!, step.FactsHash!, arguments, request.Id);
        return new(invocation, reviewer, JsonSerializer.Deserialize<SharedKernel.Abstractions.Agents.ToolApprovalPreview>(request.PreviewJson,
            WorkflowDefinitionCodec.JsonOptions) ?? throw new PermissionDeniedException(action.RequiredPermission), step.AiRunId);
    }

    public async Task RequireActiveInvocationAsync(WorkflowActionInvocation invocation, string actionKey, int actionVersion,
        CancellationToken cancellationToken = default)
    {
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        if (execution is null || invocation.OperationId == Guid.Empty || context.Run?.Id != invocation.RunId
            || context.Step?.Id != invocation.StepRunId || context.LeaseId == Guid.Empty)
            throw new PermissionDeniedException("Workflow.Run", "The action requires an active persisted workflow invocation.");
        var step = await database.WorkflowStepRuns.AsNoTracking().SingleAsync(step => step.TenantId == tenantId && step.Id == invocation.StepRunId, cancellationToken);
        var run = await database.WorkflowRuns.AsNoTracking().SingleAsync(run => run.TenantId == tenantId && run.Id == invocation.RunId, cancellationToken);
        string argsHash = SharedKernel.Abstractions.Agents.ToolArgumentsHash.Compute(invocation.Arguments.EnumerateObject()
            .ToDictionary(property => property.Name, property => (object?)property.Value));
        if (run.LeaseId != context.LeaseId || run.LeaseExpiresAt <= clock.UtcNow || run.Status != Entities.Workflows.WorkflowRunStatuses.Running
            || run.SubjectId != invocation.SubjectId || run.SubjectType != invocation.SubjectType
            || WorkflowExecutionAuthorization.Actor(run) != invocation.Actor
            || step.RunId != run.Id || step.OperationId != invocation.OperationId || step.Status != "Executing"
            || step.ActionKey != actionKey || step.ActionVersion != actionVersion || step.FactsHash != invocation.FactsHash
            || step.ExpectedSubjectVersion != invocation.ExpectedSubjectVersion || step.ArgumentsHash != argsHash)
            throw new PermissionDeniedException("Workflow.Run", "The persisted invocation identity changed.");
        await execution.RequireRunAsync(run, cancellationToken);
        await execution.RequireEnabledAsync(cancellationToken);
        await execution.RequireRecordedActionAsync(invocation.Actor, step.RequiredPermission!, actionKey, actionVersion,
            invocation.SubjectType, invocation.SubjectId, cancellationToken);
        WorkflowFactsSnapshot facts = await execution.LoadFactsAsync(invocation.SubjectType, invocation.SubjectId, invocation.Actor, cancellationToken);
        if (facts.SubjectVersion != invocation.ExpectedSubjectVersion || WorkflowFactsHash.Compute(facts) != invocation.FactsHash)
            throw new PermissionDeniedException("Workflow.Run", "The document changed after the action was prepared.");
    }

    public async Task<Guid> RequireApprovedActorAsync(WorkflowActionInvocation invocation, string actionKey, int actionVersion,
        CancellationToken cancellationToken = default)
    {
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        if (invocation.OperationId == Guid.Empty || invocation.ApprovalRequestId is null || context.Run?.Id != invocation.RunId
            || context.Step?.Id != invocation.StepRunId || context.LeaseId == Guid.Empty)
            throw new PermissionDeniedException("Workflow.Run", "A workflow write requires its active, persisted invocation.");
        var step = await database.WorkflowStepRuns.AsNoTracking().SingleAsync(step => step.TenantId == tenantId && step.Id == invocation.StepRunId, cancellationToken);
        var run = await database.WorkflowRuns.AsNoTracking().SingleAsync(run => run.TenantId == tenantId && run.Id == invocation.RunId, cancellationToken);
        ToolApprovalRequest request = await database.ToolApprovalRequests.AsNoTracking().SingleAsync(
            request => request.TenantId == tenantId && request.Id == invocation.ApprovalRequestId, cancellationToken);
        string argsHash = SharedKernel.Abstractions.Agents.ToolArgumentsHash.Compute(invocation.Arguments.EnumerateObject()
            .ToDictionary(property => property.Name, property => (object?)property.Value));
        if (run.LeaseId != context.LeaseId || run.LeaseExpiresAt <= clock.UtcNow || run.Status != Entities.Workflows.WorkflowRunStatuses.Running
            || run.SubjectId != invocation.SubjectId || run.SubjectType != invocation.SubjectType
            || run.ActorType != invocation.Actor.Type || run.ActorId != invocation.Actor.Id
            || step.RunId != run.Id || step.OperationId != invocation.OperationId || step.Status != "Executing"
            || step.ActionKey != actionKey || step.ActionVersion != actionVersion || step.FactsHash != invocation.FactsHash
            || step.ExpectedSubjectVersion != invocation.ExpectedSubjectVersion || step.ArgumentsHash != argsHash
            || request.WorkflowStepRunId != step.Id || step.ApprovalRequestId != request.Id
            || request.Status != ToolApprovalRequestStatus.Approved || request.ConsumedAt is null || request.DecidedAt is null
            || request.ExpiresAt <= clock.UtcNow || request.DecidedByUserId is not { } reviewer)
            throw new PermissionDeniedException("Workflow.Run", "The exact workflow action has no valid consumed human approval.");
        await reviewers.RequireAsync(request, reviewer, cancellationToken);
        if (run.ActorType == "AutomationIdentity")
        {
            if (automation is null || step.RequiredPermission is null) throw new PermissionDeniedException("Workflow.Run");
            await automation.RequireAsync(WorkflowExecutionAuthorization.Actor(run), step.RequiredPermission,
                run.SubjectType!, run.SubjectId!.Value, new(actionKey, actionVersion), cancellationToken);
        }
        return reviewer;
    }
}
