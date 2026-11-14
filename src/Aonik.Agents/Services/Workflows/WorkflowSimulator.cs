using System.Text.Json;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities.Workflows;

namespace Aonik.Agents.Services.Workflows;

/// <summary>Pure fixture preview. It has no domain, model, connector or event dependencies.</summary>
internal static class WorkflowSimulator
{
    public static WorkflowSimulation Simulate(WorkflowDefinition definition, WorkflowSimulationRequest request)
    {
        string hash = WorkflowDefinitionCodec.Hash(WorkflowDefinitionCodec.Serialize(definition));
        IReadOnlyList<WorkflowDiagnostic> diagnostics = WorkflowDefinitionValidator.Validate(definition, forPublication: false);
        if (diagnostics.Any(diagnostic => diagnostic.Severity == "Error")) return new("Fixture", "Invalid", hash, [], diagnostics);
        Dictionary<string, WorkflowDefinitionNode> nodes = definition.Nodes.ToDictionary(node => node.Key, StringComparer.Ordinal);
        WorkflowDefinitionNode current = definition.Nodes.Single(node => WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Trigger));
        var steps = new List<WorkflowSimulationStep>();

        for (int visited = 0; visited < definition.Nodes.Count; visited++)
        {
            DecisionEvaluation? decision = null;
            JsonElement? fixture = null;
            string? route = null;
            if (WorkflowDefinitionValidator.IsKind(current, WorkflowNodeKinds.Decision))
            {
                DecisionTable table = current.Parameters.Deserialize<DecisionTable>(WorkflowDefinitionCodec.JsonOptions)!;
                decision = DecisionTableEvaluator.Evaluate(table, request.Facts);
                if (decision.Status != "Completed")
                {
                    steps.Add(new(current.Key, current.Kind, decision.Status, decision));
                    return new("Fixture", decision.Status, hash, steps, diagnostics);
                }
                route = decision.Routes.Single();
            }
            else if (WorkflowDefinitionValidator.IsKind(current, WorkflowNodeKinds.End))
            {
                steps.Add(new(current.Key, current.Kind, "Simulated"));
                return new("Fixture", "Completed", hash, steps, diagnostics);
            }
            else if (!WorkflowDefinitionValidator.IsKind(current, WorkflowNodeKinds.Trigger))
            {
                if (request.ActionOutputs is null || !request.ActionOutputs.TryGetValue(current.Key, out JsonElement output))
                {
                    steps.Add(new(current.Key, current.Kind, "NeedsFixture"));
                    return new("Fixture", "NeedsFixture", hash, steps, diagnostics);
                }
                fixture = output.Clone();
            }
            steps.Add(new(current.Key, current.Kind, "Simulated", decision, fixture));
            WorkflowDefinitionEdge edge = definition.Edges.Single(edge => edge.From == current.Key
                && (route is null || edge.RouteKey == route));
            current = nodes[edge.To];
        }

        return new("Fixture", "Blocked", hash, steps, [new("StepLimit", "graph", "Simulation reached its graph limit.")]);
    }
}
