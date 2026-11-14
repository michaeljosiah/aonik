using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Entities;
using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Framework;
using Aonik.Agents.Persistence;
using Aonik.Agents.Workflows.Graph;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Abstractions.Workflows;
using Aonik.SharedKernel.Modules;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowExecutionAuthorization(
    ITenantProvider tenantProvider, IPermissionService permissions, IUserDirectoryReader users,
    ITenantSettingStore settings, IConfiguration configuration, IEnumerable<IWorkflowFactsProvider> factsProviders,
    IModuleEnablementReader? modules = null, IAutomationIdentityAuthorization? automation = null, AgentsDbContext? database = null, IClock? clock = null)
{
    public async Task RequireActorAsync(WorkflowActor actor, CancellationToken cancellationToken = default)
    {
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        if (actor.Type == "AutomationIdentity" && automation is not null)
        {
            await automation.RequireAsync(actor, "Workflow.Read", cancellationToken: cancellationToken);
            await automation.RequireAsync(actor, "Workflow.Run", cancellationToken: cancellationToken);
            return;
        }
        if (actor.Type != "User" || actor.Id == Guid.Empty
            || !(await users.GetByIdsAsync(tenantId, [actor.Id], cancellationToken)).Any(user => user.UserId == actor.Id && user.Status == "Active")
            || !await permissions.HasPermissionAsync(actor.Id, "Workflow.Read", cancellationToken)
            || !await permissions.HasPermissionAsync(actor.Id, "Workflow.Run", cancellationToken))
            throw new PermissionDeniedException("Workflow.Run");
    }

    public async Task RequireActionAsync(WorkflowActor actor, WorkflowActionDescriptor action, string subjectType, Guid subjectId,
        CancellationToken cancellationToken = default)
        => await RequireRecordedActionAsync(actor, action.RequiredPermission, action.Key, action.Version, subjectType, subjectId, cancellationToken);

    public async Task RequireRecordedActionAsync(WorkflowActor actor, string permission, string actionKey, int actionVersion, string subjectType, Guid subjectId,
        CancellationToken cancellationToken = default)
    {
        if (actor.Type != "AutomationIdentity") return;
        if (automation is null) throw new PermissionDeniedException(permission);
        await automation.RequireAsync(actor, permission, subjectType, subjectId, new(actionKey, actionVersion), cancellationToken);
    }

    public async Task RequireEnabledAsync(CancellationToken cancellationToken = default)
    {
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        string? enabled = await settings.GetTenantValueAsync(WorkflowSettingNames.ExecutionEnabled, tenantId, cancellationToken);
        string? host = configuration["Workflows:ExecutionEnabled"] ?? configuration[WorkflowSettingNames.ExecutionEnabled];
        if (!bool.TryParse(enabled, out bool isEnabled) || !isEnabled || (host is not null && (!bool.TryParse(host, out bool hostEnabled) || !hostEnabled)))
            throw new PermissionDeniedException("Workflow.Run", "Workflow execution is disabled for this tenant.");
    }

    public async Task RequireAutomaticEnabledAsync(CancellationToken cancellationToken = default)
    {
        string? tenant = await settings.GetTenantValueAsync(WorkflowSettingNames.AutomaticTriggersEnabled, tenantProvider.GetCurrentTenantId(), cancellationToken);
        string? host = configuration["Workflows:AutomaticTriggersEnabled"] ?? configuration[WorkflowSettingNames.AutomaticTriggersEnabled];
        if (!bool.TryParse(tenant, out bool enabled) || !enabled || (host is not null && (!bool.TryParse(host, out bool hostEnabled) || !hostEnabled)))
            throw new PermissionDeniedException("Workflow.Run", "Automatic workflow triggers are disabled for this tenant.");
    }

    public async Task RequireRunAsync(WorkflowRun run, CancellationToken cancellationToken = default)
    {
        if (run.DeadlineUtc is { } deadline && deadline <= (clock?.UtcNow ?? DateTime.UtcNow)) throw new WorkflowExecutionException("RunDeadlineExceeded");
        await RequireActorAsync(Actor(run), cancellationToken);
        if (run.ActorType == "AutomationIdentity" && automation is not null && run.SubjectId is { } subjectId)
            await automation.RequireAsync(Actor(run), "Workflow.Run", run.SubjectType, subjectId, cancellationToken: cancellationToken);
        if (run.ParentRunId is { } parentId)
        {
            if (database is null || run.Depth is < 1 or > WorkflowControlLimits.MaximumNestingDepth) throw new WorkflowExecutionException("ParentRunUnavailable");
            WorkflowRun parent = await database.WorkflowRuns.AsNoTracking().SingleOrDefaultAsync(parent => parent.TenantId == run.TenantId
                && parent.Id == parentId && parent.Depth == run.Depth - 1, cancellationToken) ?? throw new WorkflowExecutionException("ParentRunUnavailable");
            if (parent.Status is WorkflowRunStatuses.Cancelled or WorkflowRunStatuses.Completed or WorkflowRunStatuses.Failed
                || run.ExecutionRootRunId != (parent.ExecutionRootRunId ?? parent.Id)
                || await database.Workflows.AsNoTracking().AnyAsync(workflow => workflow.TenantId == run.TenantId && workflow.Id == parent.WorkflowId
                    && workflow.RetiredAt != null, cancellationToken)) throw new WorkflowExecutionException("ParentRunCancelled");
            await RequireRunAsync(parent, cancellationToken);
        }
        if (run.TriggerBindingId is not { } bindingId) return;
        await RequireAutomaticEnabledAsync(cancellationToken);
        if (database is null || run.ActorType != "AutomationIdentity" || run.TriggerBindingRevision is null || run.TriggerApprovalProposalId is null
            || !await database.WorkflowTriggerBindings.AsNoTracking().AnyAsync(binding => binding.TenantId == tenantProvider.GetCurrentTenantId()
                && binding.Id == bindingId && binding.WorkflowId == run.WorkflowId && binding.Status == "Active"
                && binding.ActiveRevision >= run.TriggerBindingRevision && binding.DisabledThroughRevision < run.TriggerBindingRevision, cancellationToken))
            throw new WorkflowExecutionException("TriggerDisabled");
        Proposal? approval = await database.Proposals.AsNoTracking().SingleOrDefaultAsync(proposal => proposal.TenantId == run.TenantId
            && proposal.Id == run.TriggerApprovalProposalId && proposal.ProposalType == WorkflowTriggerApprovalManifest.ActivateType
            && proposal.Status == ProposalStatus.Applied && proposal.ApprovedByUserId != null, cancellationToken);
        if (approval is null) throw new WorkflowExecutionException("TriggerApprovalUnavailable");
        var payload = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(approval.PayloadJson);
        if (payload.GetProperty("bindingId").GetGuid() != bindingId || payload.GetProperty("command").GetProperty("definitionHash").GetString() != run.TriggerDefinitionHash)
            throw new WorkflowExecutionException("TriggerApprovalMismatch");
    }

    public async Task<WorkflowFactsSnapshot> LoadFactsAsync(string subjectType, Guid subjectId, WorkflowActor actor,
        CancellationToken cancellationToken = default)
    {
        IWorkflowFactsProvider provider = factsProviders.SingleOrDefault(provider => provider.SubjectType == subjectType)
            ?? throw new WorkflowExecutionException("SubjectProviderUnavailable");
        await RequireModuleAsync(provider.GetType(), cancellationToken);
        return await provider.LoadAsync(subjectId, actor, cancellationToken)
            ?? throw new KeyNotFoundException("Workflow subject not found.");
    }

    public async Task RequireModuleAsync(Type implementation, CancellationToken cancellationToken = default)
    {
        string? module = ModuleCatalog.TryGetModuleId(implementation);
        if (module is not null && modules is not null
            && !(await modules.GetAsync(tenantProvider.GetCurrentTenantId(), cancellationToken)).IsEnabled(module))
            throw new WorkflowExecutionException("ModuleDisabled");
    }

    internal static WorkflowActor Actor(WorkflowRun run) => run.ActorType is { } type && run.ActorId is { } id
        ? new(type, id, run.InitiatingUserId, run.WorkflowVersionId) : throw new WorkflowExecutionException("ExecutionIdentityMissing");
}
