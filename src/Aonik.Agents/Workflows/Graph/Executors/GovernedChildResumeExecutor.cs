using Microsoft.Agents.AI.Workflows;

using Aonik.Agents.Contracts.Models.Workflows;

namespace Aonik.Agents.Workflows.Graph.Executors;

internal sealed class GovernedChildResumeExecutor(WorkflowDefinitionNode node, WorkflowStepExecutionService execution)
    : Executor<WorkflowExecutionFrame, WorkflowExecutionFrame>($"{node.Key}:child-resume")
{
    public override async ValueTask<WorkflowExecutionFrame> HandleAsync(WorkflowExecutionFrame message, IWorkflowContext context, CancellationToken cancellationToken = default)
        => await execution.CompleteChildAsync(node, message, cancellationToken);
}
