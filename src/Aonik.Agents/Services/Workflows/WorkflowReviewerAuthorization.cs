using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.Agents.Workflows.Graph;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowReviewerAuthorization(
    AgentsDbContext database, ITenantProvider tenantProvider, IUserDirectoryReader users,
    ITenantRoleReader roles, IPermissionService permissions, WorkflowExecutionAuthorization execution, IClock clock,
    IConnectorService? connectors = null)
{
    public async Task RequireAsync(ToolApprovalRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        bool recordedHumanResponse = request.ToolName == WorkflowHumanTaskContract.ToolName && request.Status == ToolApprovalRequestStatus.Approved
            && request.DecidedAt < request.ExpiresAt;
        if ((!recordedHumanResponse && request.ExpiresAt <= clock.UtcNow) || request.Status is ToolApprovalRequestStatus.Rejected or ToolApprovalRequestStatus.Expired or ToolApprovalRequestStatus.Cancelled)
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
        if (definition.SchemaVersion == 2 && WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Human))
        {
            await RequireHumanAsync(request, userId, run, step, node, cancellationToken);
            return;
        }
        WorkflowToolParameters parameters = node.Parameters.Deserialize<WorkflowToolParameters>(WorkflowDefinitionCodec.JsonOptions)
            ?? throw new WorkflowConflictException("The published action contract is invalid.");
        WorkflowReviewerRequirement requirement = parameters.Reviewer ?? throw new WorkflowConflictException("The published action has no reviewer requirement.");
        if (GmailActionPolicies.IsMutation(parameters.ActionKey))
        {
            if (connectors is null || parameters.Connection is null) throw new PermissionDeniedException("Integration.Use");
            await connectors.ValidateReviewAccessAsync(parameters.Connection.ConnectionId, new(parameters.ActionKey, parameters.ContractVersion), userId, cancellationToken);
        }
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
        try { await execution.RequireRunAsync(run, cancellationToken); }
        catch (WorkflowExecutionException) { throw new WorkflowConflictException("The workflow's source, dependencies or execution authority changed. Review the run before deciding."); }
        await execution.RequireRecordedActionAsync(WorkflowExecutionAuthorization.Actor(run), step.RequiredPermission!, step.ActionKey!, step.ActionVersion!.Value,
            run.SubjectType!, run.SubjectId!.Value, cancellationToken);
        await execution.RequireEnabledAsync(cancellationToken);
        await execution.LoadFactsAsync(run.SubjectType!, run.SubjectId!.Value, new("User", userId, userId), cancellationToken);
        WorkflowFactsSnapshot current = await execution.LoadFactsAsync(run.SubjectType!, run.SubjectId!.Value, WorkflowExecutionAuthorization.Actor(run), cancellationToken);
        if (WorkflowFactsHash.Compute(current) != step.FactsHash)
            throw new WorkflowConflictException("The invoice facts changed after this approval was prepared.");
    }

    public async Task<string?> ValidateResponseAsync(ToolApprovalRequest request, ToolApprovalDecisionInput decision, CancellationToken cancellationToken)
    {
        if (request.ToolName != WorkflowHumanTaskContract.ToolName) return null;
        WorkflowStepRun step = await database.WorkflowStepRuns.AsNoTracking().SingleAsync(step => step.TenantId == request.TenantId
            && step.Id == request.WorkflowStepRunId, cancellationToken);
        WorkflowRun run = await database.WorkflowRuns.AsNoTracking().SingleAsync(run => run.TenantId == request.TenantId && run.Id == step.RunId, cancellationToken);
        WorkflowVersion version = await database.WorkflowVersions.AsNoTracking().SingleAsync(version => version.TenantId == request.TenantId
            && version.Id == run.WorkflowVersionId && version.WorkflowId == run.WorkflowId, cancellationToken);
        WorkflowDefinitionNode node = WorkflowRunService.ReadDefinition(version).Nodes.Single(node => node.Key == step.NodeKey);
        if (!WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Human)) throw new ArgumentException("This is not a human-task request.");
        return WorkflowHumanTaskContract.ValidateResponse(WorkflowHumanTaskContract.Parameters(node), decision);
    }

    private async Task RequireHumanAsync(ToolApprovalRequest request, Guid userId, WorkflowRun run, WorkflowStepRun step,
        WorkflowDefinitionNode node, CancellationToken cancellationToken)
    {
        WorkflowHumanParameters parameters = WorkflowHumanTaskContract.Parameters(node);
        WorkflowFactsSnapshot recorded = JsonSerializer.Deserialize<WorkflowFactsSnapshot>(step.FactsJson!, WorkflowDefinitionCodec.JsonOptions)!;
        if (request.TenantId != run.TenantId || request.ToolName != WorkflowHumanTaskContract.ToolName || request.ProposalId.HasValue
            || step.ActionKey != WorkflowHumanTaskContract.Key || step.ActionVersion != 1 || step.RequiredPermission != "Workflow.Run"
            || request.RiskTier != "Medium" || step.Status is not ("WaitingForApproval" or "Prepared")
            || request.ArgsHash != step.ArgumentsHash || request.ExpiresAt != step.DeadlineUtc
            || WorkflowFactsHash.Compute(recorded) != step.FactsHash || step.ArgumentsJson is null
            || WorkflowDefinitionCodec.Hash(step.ArgumentsJson) != WorkflowDefinitionCodec.Hash(WorkflowHumanTaskContract.Arguments(run, step, parameters).GetRawText()))
            throw new WorkflowConflictException("The human task and its published response contract no longer match.");
        if (!(await users.GetByIdsAsync(run.TenantId, [userId], cancellationToken)).Any(user => user.UserId == userId && user.Status == "Active")
            || (parameters.Reviewer.UserId.HasValue && parameters.Reviewer.UserId != userId)
            || (parameters.Reviewer.RoleId is { } roleId && !await roles.IsMemberAsync(run.TenantId, userId, roleId, cancellationToken))
            || !await permissions.HasPermissionAsync(userId, "Workflow.Read", cancellationToken)
            || !await permissions.HasPermissionAsync(userId, "Workflow.Run", cancellationToken)
            || (parameters.Reviewer.ForbidSelfApproval && (run.InitiatingUserId == userId || recorded.SubjectCreatedByUserId == userId)))
            throw new PermissionDeniedException("Workflow.Run", "The current user cannot respond to this workflow task.");
        await execution.RequireRunAsync(run, cancellationToken);
        await execution.RequireEnabledAsync(cancellationToken);
        await execution.LoadFactsAsync(run.SubjectType!, run.SubjectId!.Value, new("User", userId, userId), cancellationToken);
        WorkflowFactsSnapshot current = await execution.LoadFactsAsync(run.SubjectType!, run.SubjectId!.Value, WorkflowExecutionAuthorization.Actor(run), cancellationToken);
        if (WorkflowFactsHash.Compute(current) != step.FactsHash) throw new WorkflowConflictException("The subject changed after this task was prepared.");
    }
}
