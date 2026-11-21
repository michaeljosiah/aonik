using Microsoft.Agents.AI.Workflows;

using Aonik.Agents.Contracts.Models.Workflows;

namespace Aonik.Agents.Workflows.Graph.Executors;

internal sealed class GovernedWaitResumeExecutor(WorkflowDefinitionNode node, WorkflowStepExecutionService execution)
    : Executor<WorkflowExecutionFrame, WorkflowExecutionFrame>($"{node.Key}:execute")
{
    public override async ValueTask<WorkflowExecutionFrame> HandleAsync(WorkflowExecutionFrame message, IWorkflowContext context, CancellationToken cancellationToken = default)
        => await execution.SerializeControlAsync(() => execution.CompleteWaitAsync(node, message, cancellationToken), cancellationToken);
}
