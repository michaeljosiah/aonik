using Microsoft.Agents.AI.Workflows;
using Aonik.Agents.Contracts.Models.Workflows;

namespace Aonik.Agents.Workflows.Graph.Executors;

internal sealed class GovernedLoopPrepareExecutor(WorkflowDefinitionNode node, WorkflowStepExecutionService execution)
    : Executor<WorkflowExecutionFrame, WorkflowExecutionFrame>($"{node.Key}:loop-prepare")
{
    public override async ValueTask<WorkflowExecutionFrame> HandleAsync(WorkflowExecutionFrame message, IWorkflowContext context, CancellationToken cancellationToken = default)
        => await execution.PrepareLoopAsync(node, message, cancellationToken);
}
