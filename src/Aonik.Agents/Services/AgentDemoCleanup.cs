using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions.Agents;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Agents.Services;

/// <summary>
/// Internal implementation of <see cref="IAgentDemoCleanup"/> backed by
/// <see cref="AgentsDbContext"/>. The Platform demo-seed reverse flow
/// previously reached into AgentsDbContext directly; routing through this
/// contract keeps the entity types and DbContext encapsulated in the
/// Agents runtime.
/// </summary>
internal sealed class AgentDemoCleanup : IAgentDemoCleanup
{
    private readonly AgentsDbContext _agentsDbContext;

    public AgentDemoCleanup(AgentsDbContext agentsDbContext)
    {
        _agentsDbContext = agentsDbContext;
    }

    public async Task<AgentActivityCleanupCounts> RemoveAgentActivityAsync(
        Guid tenantId,
        IReadOnlyCollection<string> agentNames,
        CancellationToken cancellationToken = default)
    {
        if (agentNames.Count == 0)
            return new AgentActivityCleanupCounts(0, 0);

        var agentIds = await _agentsDbContext.Agents
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && agentNames.Contains(item.Name))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        if (agentIds.Count == 0)
            return new AgentActivityCleanupCounts(0, 0);

        var proposalsDeleted = await _agentsDbContext.Proposals
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && agentIds.Contains(item.ProposedByAgentId))
            .ExecuteDeleteAsync(cancellationToken);

        var runsDeleted = await _agentsDbContext.AgentRuns
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && agentIds.Contains(item.AgentId))
            .ExecuteDeleteAsync(cancellationToken);

        return new AgentActivityCleanupCounts(proposalsDeleted, runsDeleted);
    }

    public async Task<WorkflowRegistryCleanupCounts> RemoveWorkflowsAndAgentsAsync(
        Guid tenantId,
        IReadOnlyCollection<string> workflowSlugs,
        IReadOnlyCollection<string> agentNames,
        CancellationToken cancellationToken = default)
    {
        var workflowsDeleted = 0;

        if (workflowSlugs.Count > 0)
        {
            var workflowIds = await _agentsDbContext.Workflows
                .IgnoreQueryFilters()
                .Where(item => item.TenantId == tenantId && workflowSlugs.Contains(item.Slug))
                .Select(item => item.Id)
                .ToListAsync(cancellationToken);

            if (workflowIds.Count > 0)
            {
                await _agentsDbContext.WorkflowRuns
                    .IgnoreQueryFilters()
                    .Where(item => workflowIds.Contains(item.WorkflowId))
                    .ExecuteDeleteAsync(cancellationToken);

                await _agentsDbContext.WorkflowVersions
                    .IgnoreQueryFilters()
                    .Where(item => workflowIds.Contains(item.WorkflowId))
                    .ExecuteDeleteAsync(cancellationToken);

                await _agentsDbContext.WorkflowComments
                    .IgnoreQueryFilters()
                    .Where(item => workflowIds.Contains(item.WorkflowId))
                    .ExecuteDeleteAsync(cancellationToken);

                await _agentsDbContext.WorkflowEdges
                    .IgnoreQueryFilters()
                    .Where(item => workflowIds.Contains(item.WorkflowId))
                    .ExecuteDeleteAsync(cancellationToken);

                await _agentsDbContext.WorkflowNodes
                    .IgnoreQueryFilters()
                    .Where(item => workflowIds.Contains(item.WorkflowId))
                    .ExecuteDeleteAsync(cancellationToken);

                workflowsDeleted = await _agentsDbContext.Workflows
                    .IgnoreQueryFilters()
                    .Where(item => workflowIds.Contains(item.Id))
                    .ExecuteDeleteAsync(cancellationToken);
            }
        }

        var agentsDeleted = 0;
        if (agentNames.Count > 0)
        {
            agentsDeleted = await _agentsDbContext.Agents
                .IgnoreQueryFilters()
                .Where(item => item.TenantId == tenantId && agentNames.Contains(item.Name))
                .ExecuteDeleteAsync(cancellationToken);
        }

        return new WorkflowRegistryCleanupCounts(workflowsDeleted, agentsDeleted);
    }
}
