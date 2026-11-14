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
        var entries = new Dictionary<string, Executor>(StringComparer.Ordinal);
        var exits = new Dictionary<string, Executor>(StringComparer.Ordinal);
        var ports = new List<(Executor Prepare, RequestPort Port, Executor Execute)>();
        var ends = new List<GovernedEndExecutor>();
        foreach (WorkflowDefinitionNode node in definition.Nodes)
        {
            if (WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Tool) || WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Wait)
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
        foreach (WorkflowDefinitionEdge edge in definition.Edges)
        {
            WorkflowDefinitionNode source = definition.Nodes.Single(node => node.Key == edge.From);
            bool decision = WorkflowDefinitionValidator.IsKind(source, WorkflowNodeKinds.Decision) || WorkflowDefinitionValidator.IsKind(source, WorkflowNodeKinds.Human);
            builder.AddEdge<WorkflowExecutionFrame>(exits[edge.From], entries[edge.To],
                frame => frame is not null && !frame.Stopped && (!decision || frame.Route == edge.RouteKey));
        }
        foreach (GovernedEndExecutor end in ends) builder.WithOutputFrom(end);
        return builder.Build();
    }
}
