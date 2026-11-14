using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowConnectorContextReader(AgentsDbContext database, ITenantProvider tenantProvider,
    WorkflowInvocationContext context, WorkflowExecutionAuthorization authority, IPermissionService permissions, IClock clock)
    : IWorkflowConnectorContextReader
{
    public async Task<ConnectorUseContext> RequireAsync(Guid runId, Guid stepRunId, Guid connectionId, ConnectorActionRef action,
        JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (context.Run?.Id != runId || context.Step?.Id != stepRunId || context.LeaseId == Guid.Empty) throw Denied();
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        WorkflowRun run = await database.WorkflowRuns.AsNoTracking().SingleOrDefaultAsync(run => run.TenantId == tenantId && run.Id == runId, cancellationToken) ?? throw Denied();
        WorkflowStepRun step = await database.WorkflowStepRuns.AsNoTracking().SingleOrDefaultAsync(step => step.TenantId == tenantId && step.Id == stepRunId, cancellationToken) ?? throw Denied();
        string hash = ToolArgumentsHash.Compute(arguments.EnumerateObject().ToDictionary(property => property.Name, property => (object?)property.Value));
        if (run.Status != WorkflowRunStatuses.Running || run.LeaseId != context.LeaseId || run.LeaseExpiresAt <= clock.UtcNow
            || step.RunId != run.Id || step.Status != "Executing" || step.ActionKey != action.Key || step.ActionVersion != action.Version
            || step.ArgumentsHash != hash || run.PendingStepRunId != step.Id) throw Denied();
        await authority.RequireActorAsync(WorkflowExecutionAuthorization.Actor(run), cancellationToken);
        await authority.RequireEnabledAsync(cancellationToken);
        WorkflowConnectorBinding binding = await BindingAsync(run, step.NodeKey, action, cancellationToken);
        if (binding.ConnectionId != connectionId) throw Denied();
        return new(WorkflowExecutionAuthorization.Actor(run), run.WorkflowId, run.WorkflowVersionId, run.Id, step.Id,
            binding.WorkflowGrantId, binding.AllowedActions);
    }

    public async Task RequireGrantTargetAsync(Guid workflowId, Guid userId, CancellationToken cancellationToken = default)
    {
        await authority.RequireActorAsync(new("User", userId, userId), cancellationToken);
        if (!await permissions.HasPermissionAsync(userId, "Workflow.Write", cancellationToken)
            || !await database.Workflows.AsNoTracking().AnyAsync(workflow => workflow.TenantId == tenantProvider.GetCurrentTenantId()
                && workflow.Id == workflowId && workflow.RetiredAt == null, cancellationToken)) throw Denied();
    }

    public async Task<WorkflowConnectorRequestTarget> GetRequestTargetAsync(Guid runId, Guid userId, CancellationToken cancellationToken = default)
    {
        await authority.RequireActorAsync(new("User", userId, userId), cancellationToken);
        WorkflowRun run = await database.WorkflowRuns.AsNoTracking().SingleOrDefaultAsync(run => run.TenantId == tenantProvider.GetCurrentTenantId()
            && run.Id == runId && run.Mode == "Live", cancellationToken) ?? throw Denied();
        if (run.Status != WorkflowRunStatuses.WaitingForConnection || run.InitiatingUserId != userId || run.PendingStepRunId is not { } stepId) throw Denied();
        await authority.LoadFactsAsync(run.SubjectType!, run.SubjectId!.Value, new("User", userId, userId), cancellationToken);
        WorkflowStepRun step = await database.WorkflowStepRuns.AsNoTracking().SingleAsync(step => step.TenantId == run.TenantId && step.RunId == run.Id && step.Id == stepId, cancellationToken);
        WorkflowConnectorBinding binding = await BindingAsync(run, step.NodeKey, new(step.ActionKey!, step.ActionVersion!.Value), cancellationToken);
        return new(run.WorkflowId, binding.ConnectionId, binding.WorkflowGrantId, binding.AllowedActions);
    }

    private async Task<WorkflowConnectorBinding> BindingAsync(WorkflowRun run, string nodeKey, ConnectorActionRef action, CancellationToken cancellationToken)
    {
        if (await database.Workflows.AsNoTracking().AnyAsync(workflow => workflow.Id == run.WorkflowId && workflow.TenantId == run.TenantId
            && workflow.RetiredAt != null, cancellationToken)) throw Denied();
        WorkflowVersion version = await database.WorkflowVersions.AsNoTracking().SingleAsync(version => version.TenantId == run.TenantId
            && version.Id == run.WorkflowVersionId && version.WorkflowId == run.WorkflowId, cancellationToken);
        if (version.DefinitionHash != run.DefinitionHash) throw Denied();
        WorkflowDefinitionNode? node = WorkflowRunService.ReadDefinition(version).Nodes.SingleOrDefault(node => node.Key == nodeKey);
        WorkflowToolParameters parameters = node?.Parameters.Deserialize<WorkflowToolParameters>(WorkflowDefinitionCodec.JsonOptions) ?? throw Denied();
        if (parameters.ActionKey != action.Key || parameters.ContractVersion != action.Version || parameters.Connection is not { } binding
            || binding.AllowedActions?.Contains(action) != true) throw Denied();
        return binding;
    }
    private static PermissionDeniedException Denied() => new("Integration.Use", "Connector use requires the actual leased step and its published account binding.");
}
