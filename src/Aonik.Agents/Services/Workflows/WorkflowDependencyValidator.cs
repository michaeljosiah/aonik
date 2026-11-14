using System.Text.Json;

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

    public Task<WorkflowVersion> RequirePinAsync(WorkflowVersionPin pin, CancellationToken cancellationToken = default)
        => WorkflowPinnedVersions.RequireAsync(database, tenantProvider.GetCurrentTenantId(), pin, "Workflow", cancellationToken);

    public async Task<WorkflowDefinition> RequireLibraryAsync(DecisionLibraryReference reference, CancellationToken cancellationToken = default)
    {
        WorkflowVersion version = await WorkflowPinnedVersions.RequireAsync(database, tenantProvider.GetCurrentTenantId(), reference.Library, "DecisionLibrary", cancellationToken);
        WorkflowDefinition definition = WorkflowRunService.ReadDefinition(version);
        WorkflowDecisionLibraries.ValidateReference(reference, definition);
        return definition;
    }

    private async Task<WorkflowDependencySummary> VisitAsync(WorkflowDefinition definition, HashSet<Guid> ancestry, int depth, CancellationToken cancellationToken)
    {
        if (depth > WorkflowControlLimits.MaximumNestingDepth) throw new ArgumentException("Workflow nesting exceeds five levels.");
        int invocations = definition.Nodes.Count; int concurrency = 1; bool readOnly = true;
        foreach (WorkflowDefinitionNode node in definition.Nodes)
        {
            if (WorkflowDecisionLibraries.IsReference(node))
            {
                WorkflowDefinition library = await RequireLibraryAsync(WorkflowDecisionLibraries.Reference(node), cancellationToken);
                invocations = checked(invocations + library.Nodes.Count);
            }
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
                int iterations = loop.ItemsFromDecision is not null ? loop.MaxIterations : Math.Min(loop.Items.Count, loop.MaxIterations);
                invocations = checked(invocations + iterations * (nested.Invocations + 1));
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
        if (invocations > WorkflowControlLimits.MaximumRootInvocations || concurrency > WorkflowControlLimits.MaximumBranches)
            throw new ArgumentException("The pinned graph exceeds 1,000 root invocations or four concurrent branches.");
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
