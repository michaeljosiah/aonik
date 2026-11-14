using Microsoft.Agents.AI.Workflows;

using Aonik.Agents.Contracts.Models.Workflows;

namespace Aonik.Agents.Workflows.Graph.Executors;

internal sealed class GovernedEndExecutor(WorkflowDefinitionNode node, WorkflowStepExecutionService execution) : Executor<WorkflowExecutionFrame>(node.Key)
{
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocol) => base.ConfigureProtocol(protocol).YieldsOutput<WorkflowExecutionFrame>();

    public override async ValueTask HandleAsync(WorkflowExecutionFrame message, IWorkflowContext context, CancellationToken cancellationToken = default)
        => await context.YieldOutputAsync(await execution.ExecuteNodeAsync(node, message, cancellationToken), cancellationToken);
}
