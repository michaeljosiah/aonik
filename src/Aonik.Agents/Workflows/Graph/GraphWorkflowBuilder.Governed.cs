using Microsoft.Agents.AI.Workflows;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Services.Workflows;
using Aonik.Agents.Workflows.Graph.Executors;

namespace Aonik.Agents.Workflows.Graph;

internal static partial class GraphWorkflowBuilder
{
    internal static Microsoft.Agents.AI.Workflows.Workflow BuildGoverned(WorkflowDefinition definition, WorkflowStepExecutionService execution)
    {
        var entries = new Dictionary<string, ExecutorBinding>(StringComparer.Ordinal);
        var exits = new Dictionary<string, ExecutorBinding>(StringComparer.Ordinal);
        var ports = new List<(Executor Prepare, RequestPort Port, Executor Execute)>();
        var childPorts = new List<(ExecutorBinding Child, ExecutorBinding Port)>();
        var ends = new List<GovernedEndExecutor>();
        foreach (WorkflowDefinitionNode node in definition.Nodes)
        {
            if (WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Parallel))
            {
                var parallel = BuildParallel(node, execution);
                entries.Add(node.Key, parallel.Binding); exits.Add(node.Key, parallel.Binding);
                childPorts.AddRange(parallel.ForwardedPorts.Select(port => (parallel.Binding, port)));
            }
            else if (WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Loop))
            {
                var loop = BuildLoop(node, execution);
                entries.Add(node.Key, loop.Binding); exits.Add(node.Key, loop.Binding);
                childPorts.Add((loop.Binding, loop.ForwardedPort));
            }
            else if (WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Child))
            {
                var prepare = new GovernedChildPrepareExecutor(node, execution);
                RequestPort port = RequestPort.Create<WorkflowStepWait, WorkflowExecutionFrame>($"{node.Key}:child-wait");
                var resume = new GovernedChildResumeExecutor(node, execution);
                ExecutorBinding child = new WorkflowBuilder(prepare).AddEdge(prepare, port).AddEdge(port, resume)
                    .WithOutputFrom(resume).Build().BindAsExecutor($"{node.Key}:child");
                ExecutorBinding forwarded = RequestPort.Create<WorkflowStepWait, WorkflowExecutionFrame>($"{node.Key}:child.{port.Id}")
                    .BindAsExecutor(allowWrappedRequests: true);
                // MAF 1.28 forwards qualified ExternalRequest messages; expose their wrapped request port in the parent.
                childPorts.Add((child, forwarded));
                entries.Add(node.Key, child); exits.Add(node.Key, child);
            }
            else if (WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Tool) || WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Wait)
                || WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Human))
            {
                bool timer = WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Wait);
                bool human = WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Human);
                Executor prepare = timer ? new GovernedWaitPrepareExecutor(node, execution) : human ? new GovernedHumanPrepareExecutor(node, execution) : new GovernedToolPrepareExecutor(node, execution);
                RequestPort port = RequestPort.Create<WorkflowStepWait, WorkflowExecutionFrame>($"{node.Key}:wait");
                Executor execute = timer ? new GovernedWaitResumeExecutor(node, execution) : human ? new GovernedHumanResumeExecutor(node, execution) : new GovernedToolExecuteExecutor(node, execution);
                entries.Add(node.Key, prepare);
                exits.Add(node.Key, execute);
                ports.Add((prepare, port, execute));
            }
            else if (WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.End))
            {
                var end = new GovernedEndExecutor(node, execution);
                entries.Add(node.Key, end); exits.Add(node.Key, end); ends.Add(end);
            }
            else if (WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Trigger) || WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Decision))
            {
                var executor = new GovernedNodeExecutor(node, execution);
                entries.Add(node.Key, executor); exits.Add(node.Key, executor);
            }
            else throw new NotSupportedException($"The governed executor for {node.Kind} is unavailable.");
        }
        WorkflowDefinitionNode trigger = definition.Nodes.Single(node => WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Trigger));
        var builder = new WorkflowBuilder(entries[trigger.Key]).WithName(definition.Name).WithDescription(definition.Description);
        foreach (var (prepare, port, execute) in ports) builder.AddEdge(prepare, port).AddEdge(port, execute);
        foreach (var (child, port) in childPorts) builder.AddEdge<ExternalRequest>(child, port, request => request?.PortInfo.PortId == port.Id, idempotent: false)
            .AddEdge<ExternalResponse>(port, child, response => response is not null, idempotent: false);
        foreach (WorkflowDefinitionEdge edge in definition.Edges)
        {
            WorkflowDefinitionNode source = definition.Nodes.Single(node => node.Key == edge.From);
            bool decision = WorkflowDefinitionValidator.IsKind(source, WorkflowNodeKinds.Decision) || WorkflowDefinitionValidator.IsKind(source, WorkflowNodeKinds.Human)
                || WorkflowControlContracts.IsComposition(source);
            builder.AddEdge<WorkflowExecutionFrame>(exits[edge.From], entries[edge.To],
                frame => frame is not null && !frame.Stopped && (!decision || frame.Route == edge.RouteKey));
        }
        foreach (GovernedEndExecutor end in ends) builder.WithOutputFrom(end);
        return builder.Build();
    }
}
