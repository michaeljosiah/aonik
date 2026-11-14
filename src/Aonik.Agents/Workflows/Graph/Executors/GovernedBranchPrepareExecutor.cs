using Microsoft.Agents.AI.Workflows;
using Aonik.Agents.Contracts.Models.Workflows;

namespace Aonik.Agents.Workflows.Graph.Executors;

internal sealed class GovernedBranchPrepareExecutor(WorkflowDefinitionNode node, string branchKey, WorkflowStepExecutionService execution)
    : Executor<WorkflowExecutionFrame, WorkflowStepWait>($"{node.Key}:{branchKey}:prepare")
{
    public override async ValueTask<WorkflowStepWait> HandleAsync(WorkflowExecutionFrame message, IWorkflowContext context, CancellationToken cancellationToken = default)
        => await execution.PrepareBranchAsync(node, branchKey, message, cancellationToken);
}
