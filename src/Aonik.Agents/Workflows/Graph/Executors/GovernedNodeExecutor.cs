using Microsoft.Agents.AI.Workflows;

using Aonik.Agents.Contracts.Models.Workflows;

namespace Aonik.Agents.Workflows.Graph.Executors;

internal sealed class GovernedNodeExecutor(WorkflowDefinitionNode node, WorkflowStepExecutionService execution)
    : Executor<WorkflowExecutionFrame, WorkflowExecutionFrame>(node.Key)
{
    public override async ValueTask<WorkflowExecutionFrame> HandleAsync(WorkflowExecutionFrame message, IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        WorkflowExecutionFrame result = await execution.SerializeControlAsync(() => execution.ExecuteNodeAsync(node, message, cancellationToken), cancellationToken);
        if (result.Stopped) await context.AddEventAsync(new WorkflowStoppedEvent(result), cancellationToken);
        return result;
    }
}
