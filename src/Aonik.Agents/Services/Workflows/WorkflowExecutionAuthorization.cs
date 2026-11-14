using Microsoft.Extensions.Configuration;

using Aonik.Agents.Entities.Workflows;
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
    IModuleEnablementReader? modules = null, IAutomationIdentityAuthorization? automation = null)
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
    {
        if (actor.Type != "AutomationIdentity") return;
        if (automation is null) throw new PermissionDeniedException(action.RequiredPermission);
        await automation.RequireAsync(actor, action.RequiredPermission, subjectType, subjectId, new(action.Key, action.Version), cancellationToken);
    }

    public async Task RequireEnabledAsync(CancellationToken cancellationToken = default)
    {
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        string? enabled = await settings.GetTenantValueAsync(WorkflowSettingNames.ExecutionEnabled, tenantId, cancellationToken);
        string? host = configuration["Workflows:ExecutionEnabled"] ?? configuration[WorkflowSettingNames.ExecutionEnabled];
        if (!bool.TryParse(enabled, out bool isEnabled) || !isEnabled || (host is not null && (!bool.TryParse(host, out bool hostEnabled) || !hostEnabled)))
            throw new PermissionDeniedException("Workflow.Run", "Workflow execution is disabled for this tenant.");
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
