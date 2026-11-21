using Microsoft.Agents.AI.Workflows;
using Aonik.Agents.Contracts.Models.Workflows;

namespace Aonik.Agents.Workflows.Graph.Executors;

internal sealed class GovernedParallelPrepareExecutor(WorkflowDefinitionNode node, WorkflowStepExecutionService execution)
    : Executor<WorkflowExecutionFrame, WorkflowExecutionFrame>($"{node.Key}:parallel-prepare")
{
    public override async ValueTask<WorkflowExecutionFrame> HandleAsync(WorkflowExecutionFrame message, IWorkflowContext context, CancellationToken cancellationToken = default)
        => await execution.PrepareParallelAsync(node, message, cancellationToken);
}
