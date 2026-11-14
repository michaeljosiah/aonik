using Microsoft.Agents.AI.Workflows;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Workflows.Graph.Executors;

namespace Aonik.Agents.Workflows.Graph;

internal static partial class GraphWorkflowBuilder
{
    private static (ExecutorBinding Binding, ExecutorBinding ForwardedPort) BuildLoop(WorkflowDefinitionNode node, WorkflowStepExecutionService execution)
    {
        var prepare = new GovernedLoopPrepareExecutor(node, execution);
        var iteration = new GovernedIterationPrepareExecutor(node, execution);
        RequestPort wait = RequestPort.Create<WorkflowStepWait, WorkflowExecutionFrame>($"{node.Key}:iteration-wait");
        var resume = new GovernedIterationResumeExecutor(node, execution);
        var complete = new GovernedLoopCompleteExecutor(node, execution);
        var loop = new WorkflowBuilder(prepare)
            .AddEdge<WorkflowExecutionFrame>(prepare, iteration, frame => frame is { Route: null })
            .AddEdge<WorkflowExecutionFrame>(prepare, complete, frame => frame?.Route is not null)
            .AddEdge(iteration, wait).AddEdge(wait, resume)
            .AddEdge<WorkflowExecutionFrame>(resume, prepare, frame => frame is { Route: null })
            .AddEdge<WorkflowExecutionFrame>(resume, complete, frame => frame?.Route is not null)
            .WithOutputFrom(complete).Build().BindAsExecutor($"{node.Key}:loop");
        return (loop, RequestPort.Create<WorkflowStepWait, WorkflowExecutionFrame>($"{node.Key}:loop.{wait.Id}").BindAsExecutor(allowWrappedRequests: true));
    }
}
