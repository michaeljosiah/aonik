using Microsoft.Agents.AI.Workflows;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Services.Workflows;
using Aonik.Agents.Workflows.Graph.Executors;

namespace Aonik.Agents.Workflows.Graph;

internal static partial class GraphWorkflowBuilder
{
    private static (ExecutorBinding Binding, IReadOnlyList<ExecutorBinding> ForwardedPorts) BuildParallel(WorkflowDefinitionNode node, WorkflowStepExecutionService execution)
    {
        WorkflowParallelParameters parameters = WorkflowControlContracts.Parallel(node);
        var prepare = new GovernedParallelPrepareExecutor(node, execution);
        var output = new GovernedControlOutputExecutor($"{node.Key}:parallel-output");
        var builder = new WorkflowBuilder(prepare);
        builder.AddEdge<WorkflowExecutionFrame>(prepare, output, frame => frame is not null && frame.Route != "next");
        ExecutorBinding previous = prepare;
        List<RequestPort> ports = [];
        WorkflowParallelBranch[][] batches = parameters.Branches.Chunk(parameters.MaxConcurrency).ToArray();
        for (int batch = 0; batch < batches.Length; batch++)
        {
            WorkflowParallelBranch[] branches = batches[batch];
            var starts = new List<ExecutorBinding>(); var ends = new List<ExecutorBinding>();
            foreach (WorkflowParallelBranch branch in branches)
            {
                var start = new GovernedBranchPrepareExecutor(node, branch.Key, execution);
                var end = new GovernedBranchResumeExecutor(node, branch.Key, execution);
                RequestPort port = RequestPort.Create<WorkflowStepWait, WorkflowExecutionFrame>($"{node.Key}:{branch.Key}:wait");
                builder.AddEdge(start, port).AddEdge(port, end);
                starts.Add(start); ends.Add(end); ports.Add(port);
            }
            builder.AddFanOutEdge<WorkflowExecutionFrame>(previous, starts,
                (frame, count) => frame is { Route: "next" } ? Enumerable.Range(0, count) : []);
            string[] keys = branches.Select(branch => branch.Key).ToArray();
            ExecutorBinding join = new GovernedParallelJoinExecutor(node, keys, batch, batch == batches.Length - 1, execution);
            if (parameters.JoinPolicy == "All")
            {
                builder.AddFanInBarrierEdge(ends, join);
            }
            else
            {
                foreach (ExecutorBinding end in ends) builder.AddEdge<WorkflowExecutionFrame>(end, join, frame => frame is not null);
            }
            builder.AddEdge<WorkflowExecutionFrame>(join, output, frame => frame is not null && frame.Route != "next");
            previous = join;
        }
        string id = $"{node.Key}:parallel";
        return (builder.WithOutputFrom(output).Build().BindAsExecutor(id), ports.Select(port => RequestPort.Create<WorkflowStepWait, WorkflowExecutionFrame>($"{id}.{port.Id}")
            .BindAsExecutor(allowWrappedRequests: true)).ToList());
    }
}
