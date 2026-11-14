using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowAutomationDefinitionReader(AgentsDbContext database, ITenantProvider tenantProvider,
    WorkflowAuthorization authorization, WorkflowExecutionAuthorization execution, WorkflowActionRegistry actions)
    : IWorkflowAutomationDefinitionReader
{
    public async Task<AutomationWorkflowCapability> RequireAsync(Guid workflowVersionId, Guid configuringUserId, CancellationToken cancellationToken = default)
    {
        if (await authorization.RequireAsync("Workflow.Write", cancellationToken) != configuringUserId) throw new PermissionDeniedException("Workflow.Write");
        await execution.RequireActorAsync(new("User", configuringUserId, configuringUserId), cancellationToken);
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        WorkflowVersion version = await database.WorkflowVersions.AsNoTracking().SingleOrDefaultAsync(version => version.TenantId == tenantId && version.Id == workflowVersionId, cancellationToken)
            ?? throw new KeyNotFoundException("Published workflow version not found.");
        if (!await database.Workflows.AsNoTracking().AnyAsync(workflow => workflow.TenantId == tenantId && workflow.Id == version.WorkflowId
            && workflow.State == WorkflowStates.Active && workflow.RetiredAt == null, cancellationToken)) throw new ArgumentException("The workflow is not active.");
        WorkflowDefinition definition = WorkflowRunService.ReadDefinition(version);
        List<AutomationActionCapability> capabilities = [];
        foreach (WorkflowDefinitionNode node in definition.Nodes.Where(node => WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Tool)))
        {
            WorkflowToolParameters parameters = node.Parameters.Deserialize<WorkflowToolParameters>(WorkflowDefinitionCodec.JsonOptions)!;
            var handler = actions.Resolve(parameters.ActionKey, parameters.ContractVersion);
            await execution.RequireModuleAsync(handler.GetType(), cancellationToken);
            capabilities.Add(new(handler.Descriptor.Key, handler.Descriptor.Version, handler.Descriptor.RequiredPermission));
        }
        return new(version.WorkflowId, version.Id, version.DefinitionHash!, capabilities.Distinct().ToList());
    }

    public async Task<AutomationIdentityUsage> GetUsageAsync(Guid identityId, CancellationToken cancellationToken = default)
    {
        await authorization.RequireAsync("Workflow.Write", cancellationToken);
        List<Guid?> versions = await database.WorkflowRuns.AsNoTracking().Where(run => run.TenantId == tenantProvider.GetCurrentTenantId()
            && run.ActorType == "AutomationIdentity" && run.ActorId == identityId
            && run.Status != WorkflowRunStatuses.Completed && run.Status != WorkflowRunStatuses.Cancelled && run.Status != WorkflowRunStatuses.Failed)
            .Select(run => run.WorkflowVersionId).ToListAsync(cancellationToken);
        return new(versions.Count, versions.Where(version => version.HasValue).Select(version => version!.Value).Distinct().ToList());
    }
}
