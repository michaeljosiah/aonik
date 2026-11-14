using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using Aonik.Agents.Entities;
using Aonik.Agents.Framework;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.Settings;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowApprovalAuthority(
    ITenantProvider tenantProvider, ICurrentUserProvider currentUserProvider,
    IPermissionService permissions, IUserDirectoryReader users, ITenantSettingStore settings,
    IConfiguration configuration, AgentsDbContext database, WorkflowReviewerAuthorization? reviewers = null)
{
    internal static bool IsGovernanceType(string type) => type is WorkflowToolApprovalManifest.PublishType or WorkflowToolApprovalManifest.RetireType;
    internal static bool IsGovernanceTool(string name) => name is "aonik_request_publish_workflow" or "aonik_request_retire_workflow";
    internal static bool IsWorkflowMutation(string name) => IsGovernanceTool(name)
        || name is "aonik_create_workflow" or "aonik_update_workflow" or "aonik_delete_workflow" or "aonik_start_workflow";

    public async Task<bool> CanReadGovernanceAsync(CancellationToken cancellationToken = default)
        => tenantProvider.TryGetCurrentTenantId(out Guid tenantId)
            && currentUserProvider.TryGetCurrentUserId(out Guid userId)
            && await IsActiveAsync(tenantId, userId, cancellationToken)
            && await permissions.HasPermissionAsync(userId, "Workflow.Read", cancellationToken);

    public async Task<bool> CanDecideProposalAsync(Proposal proposal, CancellationToken cancellationToken = default)
    {
        if (proposal.WorkflowStepRunId.HasValue)
        {
            if (reviewers is null || !currentUserProvider.TryGetCurrentUserId(out Guid decidingUser)) return false;
            ToolApprovalRequest? linked = await database.ToolApprovalRequests.AsNoTracking().SingleOrDefaultAsync(
                request => request.TenantId == proposal.TenantId && request.ProposalId == proposal.Id
                    && request.WorkflowStepRunId == proposal.WorkflowStepRunId, cancellationToken);
            if (linked is null) return false;
            return await CanReviewAsync(linked, decidingUser, cancellationToken);
        }
        if (!IsGovernanceType(proposal.ProposalType)) return true;
        if (!(tenantProvider.TryGetCurrentTenantId(out Guid tenantId) && tenantId == proposal.TenantId
            && currentUserProvider.TryGetCurrentUserId(out Guid userId)
            && await IsPublisherAsync(tenantId, userId, cancellationToken))) return false;
        ToolApprovalRequest? request = await database.ToolApprovalRequests.AsNoTracking().SingleOrDefaultAsync(
            request => request.TenantId == tenantId && request.ProposalId == proposal.Id, cancellationToken);
        return request is not null && await SatisfiesSeparationAsync(request, userId, cancellationToken);
    }

    public async Task<bool> CanDecideRequestAsync(ToolApprovalRequest request, CancellationToken cancellationToken = default)
    {
        if (!tenantProvider.TryGetCurrentTenantId(out Guid tenantId) || tenantId != request.TenantId
            || !currentUserProvider.TryGetCurrentUserId(out Guid userId)) return false;
        if (request.WorkflowStepRunId.HasValue)
        {
            if (reviewers is null) return false;
            return await CanReviewAsync(request, userId, cancellationToken);
        }
        if (IsGovernanceTool(request.ToolName)) return await IsPublisherAsync(tenantId, userId, cancellationToken)
            && await SatisfiesSeparationAsync(request, userId, cancellationToken);
        if (request.ToolName is "aonik_create_workflow" or "aonik_update_workflow" or "aonik_delete_workflow")
            return request.RequestingUserId == userId && await IsActiveAsync(tenantId, userId, cancellationToken)
                && await permissions.HasPermissionAsync(userId, "Workflow.Write", cancellationToken);
        if (request.ToolName == "aonik_start_workflow")
            return request.RequestingUserId == userId && await IsActiveAsync(tenantId, userId, cancellationToken)
                && await permissions.HasPermissionAsync(userId, "Workflow.Read", cancellationToken)
                && await permissions.HasPermissionAsync(userId, "Workflow.Run", cancellationToken);
        return true;
    }

    public async Task<bool> IsPublisherAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
        => await IsActiveAsync(tenantId, userId, cancellationToken)
            && await permissions.HasPermissionAsync(userId, "Workflow.Read", cancellationToken)
            && await permissions.HasPermissionAsync(userId, "Workflow.Publish", cancellationToken);

    private async Task<bool> CanReviewAsync(ToolApprovalRequest request, Guid userId, CancellationToken cancellationToken)
    {
        try { await reviewers!.RequireAsync(request, userId, cancellationToken); return true; }
        catch (PermissionDeniedException) { return false; }
    }

    private async Task<bool> IsActiveAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
        => (await users.GetByIdsAsync(tenantId, [userId], cancellationToken))
            .Any(user => user.UserId == userId && string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase));

    private async Task<bool> SatisfiesSeparationAsync(ToolApprovalRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        // Publication governance never resolves a user preference over the tenant's restriction.
        string? value = await settings.GetTenantValueAsync(WorkflowSettingNames.RequireSeparatePublisher,
            request.TenantId, cancellationToken) ?? configuration[WorkflowSettingNames.RequireSeparatePublisher];
        bool separate = value is not null && (!bool.TryParse(value, out bool enabled) || enabled);
        return request.RequestingUserId.HasValue && (!separate || request.RequestingUserId != actorId);
    }
}
