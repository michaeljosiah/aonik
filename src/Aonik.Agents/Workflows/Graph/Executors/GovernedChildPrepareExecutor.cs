using Microsoft.Agents.AI.Workflows;

using Aonik.Agents.Contracts.Models.Workflows;

namespace Aonik.Agents.Workflows.Graph.Executors;

internal sealed class GovernedChildPrepareExecutor(WorkflowDefinitionNode node, WorkflowStepExecutionService execution)
    : Executor<WorkflowExecutionFrame, WorkflowStepWait>($"{node.Key}:child-prepare")
{
    public override async ValueTask<WorkflowStepWait> HandleAsync(WorkflowExecutionFrame message, IWorkflowContext context, CancellationToken cancellationToken = default)
        => await execution.PrepareChildAsync(node, message, cancellationToken);
}
