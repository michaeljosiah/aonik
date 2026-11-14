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
    WorkflowReviewerAuthorization reviewers, IClock clock, IAutomationIdentityAuthorization? automation = null) : IWorkflowApprovalReader
{
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
