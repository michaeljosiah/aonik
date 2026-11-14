using Microsoft.Agents.AI.Workflows;

namespace Aonik.Agents.Workflows.Graph.Executors;

internal sealed class GovernedControlOutputExecutor(string id) : Executor<WorkflowExecutionFrame, WorkflowExecutionFrame>(id)
{
    public override ValueTask<WorkflowExecutionFrame> HandleAsync(WorkflowExecutionFrame message, IWorkflowContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(message);
}
