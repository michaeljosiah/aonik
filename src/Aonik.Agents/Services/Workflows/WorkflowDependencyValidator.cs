using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.Agents.Workflows.Graph;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Agents.Services.Workflows;

/// <summary>Static bounds over immutable dependencies avoid another runtime budget or dependency store.</summary>
internal sealed class WorkflowDependencyValidator(AgentsDbContext database, ITenantProvider tenantProvider, WorkflowActionRegistry actions,
    WorkflowExecutionAuthorization? authorization = null)
{
    public Task<WorkflowDependencySummary> ValidateAsync(WorkflowDefinition definition, Guid workflowId, CancellationToken cancellationToken = default)
        => VisitAsync(definition, [workflowId], 0, cancellationToken);

    public async Task<WorkflowVersion> RequirePinAsync(WorkflowVersionPin pin, CancellationToken cancellationToken = default)
    {
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        WorkflowVersion version = await database.WorkflowVersions.AsNoTracking().SingleOrDefaultAsync(version => version.TenantId == tenantId
            && version.Id == pin.VersionId && version.PublishedAt != null, cancellationToken) ?? throw new ArgumentException("A pinned published dependency is unavailable in this tenant.");
        if (!string.Equals(version.DefinitionHash, pin.DefinitionHash, StringComparison.Ordinal)
            || !await database.Workflows.AsNoTracking().AnyAsync(workflow => workflow.TenantId == tenantId && workflow.Id == version.WorkflowId
                && workflow.State == WorkflowStates.Active && workflow.RetiredAt == null, cancellationToken))
            throw new ArgumentException("The dependency hash differs or its workflow is retired.");
        _ = WorkflowRunService.ReadDefinition(version);
        return version;
    }

    private async Task<WorkflowDependencySummary> VisitAsync(WorkflowDefinition definition, HashSet<Guid> ancestry, int depth, CancellationToken cancellationToken)
    {
        if (depth > WorkflowControlLimits.MaximumNestingDepth) throw new ArgumentException("Workflow nesting exceeds five levels.");
        int invocations = definition.Nodes.Count; int concurrency = 1; bool readOnly = true;
        foreach (WorkflowDefinitionNode node in definition.Nodes)
        {
            if (WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Tool))
            {
                var parameters = node.Parameters.Deserialize<WorkflowToolParameters>(WorkflowDefinitionCodec.JsonOptions)!;
                var action = actions.Resolve(parameters.ActionKey, parameters.ContractVersion);
                if (authorization is not null)
                {
                    try { await authorization.RequireModuleAsync(action.GetType(), cancellationToken); }
                    catch (WorkflowExecutionException) { throw new ArgumentException("A dependency action's module is unavailable."); }
                }
                readOnly &= action.Descriptor.Approval is null;
            }
            else if (WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Human)) readOnly = false;
            if (!WorkflowControlContracts.IsComposition(node)) continue;
            WorkflowDependencySummary nested;
            if (WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Child))
            {
                nested = await ChildAsync(WorkflowControlContracts.Child(node).Workflow);
                invocations = checked(invocations + nested.Invocations); concurrency = Math.Max(concurrency, nested.Concurrency); readOnly &= nested.ReadOnly;
            }
            else if (WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Loop))
            {
                WorkflowLoopParameters loop = WorkflowControlContracts.Loop(node);
                nested = await ChildAsync(loop.Body);
                invocations = checked(invocations + Math.Min(loop.Items.Count, loop.MaxIterations) * (nested.Invocations + 1));
                concurrency = Math.Max(concurrency, nested.Concurrency); readOnly &= nested.ReadOnly;
            }
            else
            {
                WorkflowParallelParameters parallel = WorkflowControlContracts.Parallel(node);
                List<WorkflowDependencySummary> branches = [];
                foreach (WorkflowParallelBranch branch in parallel.Branches) branches.Add(await ChildAsync(branch.Workflow));
                bool pureBranches = branches.All(branch => branch.ReadOnly);
                if ((!pureBranches && parallel.MaxConcurrency != 1) || (!pureBranches && parallel.JoinPolicy == "AnySuccess"))
                    throw new ArgumentException("Potentially conflicting writes require concurrency one and All. AnySuccess is restricted to read/pure branches.");
                invocations = checked(invocations + branches.Sum(branch => branch.Invocations + 1));
                concurrency = Math.Max(concurrency, branches.Chunk(parallel.MaxConcurrency).Max(group => group.Sum(branch => branch.Concurrency)));
                readOnly &= pureBranches;
            }
            if (invocations > WorkflowControlLimits.MaximumRootInvocations || concurrency > WorkflowControlLimits.MaximumBranches)
                throw new ArgumentException("The pinned graph exceeds 1,000 root invocations or four concurrent branches.");
        }
        return new(invocations, concurrency, readOnly);

        async Task<WorkflowDependencySummary> ChildAsync(WorkflowVersionPin pin)
        {
            WorkflowVersion version = await RequirePinAsync(pin, cancellationToken);
            if (ancestry.Contains(version.WorkflowId)) throw new ArgumentException("Recursive workflow dependencies are not supported.");
            return await VisitAsync(WorkflowRunService.ReadDefinition(version), [.. ancestry, version.WorkflowId], depth + 1, cancellationToken);
        }
    }
}

internal sealed record WorkflowDependencySummary(int Invocations, int Concurrency, bool ReadOnly);
