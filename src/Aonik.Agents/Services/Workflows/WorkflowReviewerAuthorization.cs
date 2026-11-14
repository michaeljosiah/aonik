using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowReviewerAuthorization(
    AgentsDbContext database, ITenantProvider tenantProvider, IUserDirectoryReader users,
    ITenantRoleReader roles, IPermissionService permissions, WorkflowExecutionAuthorization execution, IClock clock)
{
    public async Task RequireAsync(ToolApprovalRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        if (request.ExpiresAt <= clock.UtcNow || request.Status is ToolApprovalRequestStatus.Rejected or ToolApprovalRequestStatus.Expired or ToolApprovalRequestStatus.Cancelled)
            throw new WorkflowConflictException("The workflow approval was rejected or expired.");
        WorkflowStepRun step = await database.WorkflowStepRuns.AsNoTracking().SingleOrDefaultAsync(
            step => step.TenantId == tenantId && step.Id == request.WorkflowStepRunId, cancellationToken)
            ?? throw new WorkflowConflictException("The workflow approval step is unavailable.");
        WorkflowRun run = await database.WorkflowRuns.AsNoTracking().SingleAsync(run => run.TenantId == tenantId && run.Id == step.RunId, cancellationToken);
        Workflow workflow = await database.Workflows.AsNoTracking().SingleAsync(workflow => workflow.TenantId == tenantId && workflow.Id == run.WorkflowId, cancellationToken);
        if (run.Status is WorkflowRunStatuses.Completed or WorkflowRunStatuses.Cancelled or WorkflowRunStatuses.Failed
            || workflow.RetiredAt.HasValue || step.ApprovalRequestId != request.Id)
            throw new WorkflowConflictException("The workflow is terminal, retired or no longer waiting for this approval.");
        WorkflowVersion version = await database.WorkflowVersions.AsNoTracking().SingleAsync(version => version.TenantId == tenantId
            && version.Id == run.WorkflowVersionId && version.WorkflowId == run.WorkflowId, cancellationToken);
        if (version.DefinitionHash != run.DefinitionHash) throw new WorkflowConflictException("The pinned definition hash changed.");
        WorkflowDefinition definition = WorkflowRunService.ReadDefinition(version);
        WorkflowDefinitionNode node = definition.Nodes.Single(node => node.Key == step.NodeKey);
        WorkflowToolParameters parameters = node.Parameters.Deserialize<WorkflowToolParameters>(WorkflowDefinitionCodec.JsonOptions)
            ?? throw new WorkflowConflictException("The published action contract is invalid.");
        WorkflowReviewerRequirement requirement = parameters.Reviewer ?? throw new WorkflowConflictException("The published action has no reviewer requirement.");
        WorkflowFactsSnapshot recorded = JsonSerializer.Deserialize<WorkflowFactsSnapshot>(step.FactsJson!, WorkflowDefinitionCodec.JsonOptions)!;
        if (parameters.ActionKey != step.ActionKey || parameters.ContractVersion != step.ActionVersion
            || string.IsNullOrEmpty(step.RequiredPermission) || request.TenantId != tenantId
            || request.ArgsHash != step.ArgumentsHash || WorkflowFactsHash.Compute(recorded) != step.FactsHash)
            throw new WorkflowConflictException("The action and approval identity no longer match.");
        if (!(await users.GetByIdsAsync(tenantId, [userId], cancellationToken)).Any(user => user.UserId == userId && user.Status == "Active")
            || !await roles.IsMemberAsync(tenantId, userId, requirement.RoleId, cancellationToken)
            || !await permissions.HasPermissionAsync(userId, "Workflow.Read", cancellationToken)
            || !await permissions.HasPermissionAsync(userId, step.RequiredPermission, cancellationToken)
            || (requirement.ForbidSelfApproval && (run.InitiatingUserId == userId || recorded.SubjectCreatedByUserId == userId)))
            throw new PermissionDeniedException(step.RequiredPermission, "The current user cannot decide this workflow action.");
        await execution.RequireActorAsync(WorkflowExecutionAuthorization.Actor(run), cancellationToken);
        await execution.RequireEnabledAsync(cancellationToken);
        await execution.LoadFactsAsync(run.SubjectType!, run.SubjectId!.Value, new("User", userId, userId), cancellationToken);
        WorkflowFactsSnapshot current = await execution.LoadFactsAsync(run.SubjectType!, run.SubjectId!.Value, WorkflowExecutionAuthorization.Actor(run), cancellationToken);
        if (WorkflowFactsHash.Compute(current) != step.FactsHash)
            throw new WorkflowConflictException("The invoice facts changed after this approval was prepared.");
    }
}
