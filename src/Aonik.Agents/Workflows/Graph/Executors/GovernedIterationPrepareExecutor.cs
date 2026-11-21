using Microsoft.Agents.AI.Workflows;
using Aonik.Agents.Contracts.Models.Workflows;

namespace Aonik.Agents.Workflows.Graph.Executors;

internal sealed class GovernedIterationPrepareExecutor(WorkflowDefinitionNode node, WorkflowStepExecutionService execution)
    : Executor<WorkflowExecutionFrame, WorkflowStepWait>($"{node.Key}:iteration-prepare")
{
    public override async ValueTask<WorkflowStepWait> HandleAsync(WorkflowExecutionFrame message, IWorkflowContext context, CancellationToken cancellationToken = default)
        => await execution.PrepareIterationAsync(node, message, cancellationToken);
}
