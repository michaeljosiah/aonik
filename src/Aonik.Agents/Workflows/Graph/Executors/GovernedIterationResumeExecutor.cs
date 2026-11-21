using Microsoft.Agents.AI.Workflows;
using Aonik.Agents.Contracts.Models.Workflows;

namespace Aonik.Agents.Workflows.Graph.Executors;

internal sealed class GovernedIterationResumeExecutor(WorkflowDefinitionNode node, WorkflowStepExecutionService execution)
    : Executor<WorkflowExecutionFrame, WorkflowExecutionFrame>($"{node.Key}:iteration-resume")
{
    public override async ValueTask<WorkflowExecutionFrame> HandleAsync(WorkflowExecutionFrame message, IWorkflowContext context, CancellationToken cancellationToken = default)
        => await execution.CompleteIterationAsync(node, message, cancellationToken);
}
