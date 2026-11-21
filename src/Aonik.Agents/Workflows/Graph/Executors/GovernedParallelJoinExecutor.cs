using Microsoft.Agents.AI.Workflows;
using Aonik.Agents.Contracts.Models.Workflows;

namespace Aonik.Agents.Workflows.Graph.Executors;

internal sealed class GovernedParallelJoinExecutor(WorkflowDefinitionNode node, IReadOnlyList<string> branches, int batch, bool lastBatch, WorkflowStepExecutionService execution)
    : Executor<WorkflowExecutionFrame>($"{node.Key}:join:{batch}")
{
    private readonly WorkflowParallelJoinOutput _output = new();
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocol) => base.ConfigureProtocol(protocol).SendsMessage<WorkflowExecutionFrame>();

    public override async ValueTask HandleAsync(WorkflowExecutionFrame message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        // MAF's barrier releases the source messages individually after every source has arrived.
        await _output.SendAsync(() => execution.JoinParallelBatchAsync(node, branches, lastBatch, cancellationToken), context, cancellationToken);
    }
}
