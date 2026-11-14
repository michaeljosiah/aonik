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
            else if (WorkflowDefinitionValidator.IsKind(current, WorkflowNodeKinds.Wait))
                fixture = current.Parameters.Clone();
            else if (WorkflowControlContracts.IsComposition(current))
            {
                if (request.ActionOutputs is null || !request.ActionOutputs.TryGetValue(current.Key, out JsonElement output))
                {
                    steps.Add(new(current.Key, current.Kind, "NeedsFixture"));
                    return new("Fixture", "NeedsFixture", hash, steps, diagnostics);
                }
                route = output.ValueKind == JsonValueKind.Object && output.TryGetProperty("route", out JsonElement value)
                    && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                if (route is null || !WorkflowControlContracts.Routes(current).Contains(route))
                    return new("Fixture", "Invalid", hash, steps, [new("ControlFixture", current.Key, "A control fixture must select one declared route.")]);
                fixture = output.Clone();
            }
            else if (WorkflowDefinitionValidator.IsKind(current, WorkflowNodeKinds.Human))
            {
                if (request.ActionOutputs is null || !request.ActionOutputs.TryGetValue(current.Key, out JsonElement response))
                {
                    steps.Add(new(current.Key, current.Kind, "NeedsFixture"));
                    return new("Fixture", "NeedsFixture", hash, steps, diagnostics);
                }
                WorkflowHumanParameters parameters = WorkflowHumanTaskContract.Parameters(current);
                string? key = response.ValueKind == JsonValueKind.Object && response.TryGetProperty("choice", out JsonElement value)
                    && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                route = key switch { "$reject" => parameters.RejectRoute, "$timeout" => parameters.TimeoutRoute,
                    _ => parameters.Choices.SingleOrDefault(choice => choice.Key == key)?.Route };
                if (route is null) return new("Fixture", "Invalid", hash, steps, [new("HumanFixture", current.Key, "Select a published response, $reject or $timeout.")]);
                fixture = response.Clone();
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
