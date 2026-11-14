using System.Text.Json;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities.Workflows;

namespace Aonik.Agents.Services.Workflows;

internal static class WorkflowDefinitionValidator
{
    public const int MaxNodes = 500;
    public const int MaxEdges = 2000;
    public const int MaxDefinitionCharacters = 1_048_576;
    private static readonly HashSet<string> KnownKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        WorkflowNodeKinds.Trigger, WorkflowNodeKinds.Decision, WorkflowNodeKinds.Tool, WorkflowNodeKinds.End,
        WorkflowNodeKinds.Agent, WorkflowNodeKinds.Notify, WorkflowNodeKinds.Emit,
        WorkflowNodeKinds.Human, WorkflowNodeKinds.Wait, WorkflowNodeKinds.Loop,
    };

    // Expand only alongside each governed executor and its recovery tests.
    internal static readonly IReadOnlySet<string> GovernedKinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        WorkflowNodeKinds.Trigger, WorkflowNodeKinds.End,
    };

    public static WorkflowCapabilities Capabilities() => new(1, MaxNodes, MaxEdges,
        KnownKinds.Order(StringComparer.Ordinal).Select(kind => new WorkflowCapability(kind,
            kind is not (WorkflowNodeKinds.Loop or WorkflowNodeKinds.Wait or WorkflowNodeKinds.Human),
            GovernedKinds.Contains(kind))).ToList(), [DecisionTableEvaluator.InvoiceSchema],
        DecisionTableEvaluator.FactFields(), DecisionTableEvaluator.ComparisonOperators(), WorkflowDefinitionCodec.DecisionSchema());

    public static IReadOnlyList<WorkflowDiagnostic> Validate(WorkflowDefinition definition, bool forPublication)
    {
        var errors = new List<WorkflowDiagnostic>();
        if (definition.SchemaVersion != 1 || definition.DefinitionKind != "Workflow")
            errors.Add(new("DefinitionSchema", "definition", "Use workflow definition schema 1."));
        if (definition.Nodes.Count == 0 || definition.Nodes.Count > MaxNodes || definition.Edges.Count > MaxEdges)
        {
            errors.Add(new("GraphSize", "nodes", $"Provide 1–{MaxNodes} nodes and at most {MaxEdges} edges."));
            return errors;
        }
        if (definition.Nodes.Select(node => node.Key).Distinct(StringComparer.Ordinal).Count() != definition.Nodes.Count)
        {
            errors.Add(new("DuplicateNode", "nodes", "Logical node keys must be unique."));
            return errors;
        }
        Dictionary<string, WorkflowDefinitionNode> nodes = definition.Nodes.ToDictionary(node => node.Key, StringComparer.Ordinal);
        var triggers = definition.Nodes.Where(node => IsKind(node, WorkflowNodeKinds.Trigger)).ToList();
        if (triggers.Count != 1) errors.Add(new("TriggerRequired", "nodes", "Exactly one trigger is required."));
        if (!definition.Nodes.Any(node => IsKind(node, WorkflowNodeKinds.End)))
            errors.Add(new("EndRequired", "nodes", "At least one terminal End is required."));
        if (definition.Edges.Select(edge => edge.Key).Distinct(StringComparer.Ordinal).Count() != definition.Edges.Count)
            errors.Add(new("DuplicateEdge", "edges", "Logical edge keys must be unique."));
        foreach (WorkflowDefinitionEdge edge in definition.Edges)
        {
            if (!nodes.ContainsKey(edge.From) || !nodes.ContainsKey(edge.To))
                errors.Add(new("DanglingEdge", edge.Key, "Both endpoints must exist in the graph."));
            if (edge.From == edge.To) errors.Add(new("Cycle", edge.Key, "Self edges are not supported."));
        }
        foreach (WorkflowDefinitionNode node in definition.Nodes)
        {
            if (!KnownKinds.Contains(node.Kind)) errors.Add(new("UnknownKind", node.Key, "Unknown node kind."));
            if (forPublication && !GovernedKinds.Contains(node.Kind))
                errors.Add(new("CapabilityUnavailable", node.Key, "This governed executor is not yet available."));
            if (!forPublication && (IsKind(node, WorkflowNodeKinds.Loop) || IsKind(node, WorkflowNodeKinds.Wait) || IsKind(node, WorkflowNodeKinds.Human)))
                errors.Add(new("CapabilityUnavailable", node.Key, "Simulation for this control-flow node is not yet available."));
            List<WorkflowDefinitionEdge> outgoing = definition.Edges.Where(edge => edge.From == node.Key).ToList();
            if (IsKind(node, WorkflowNodeKinds.End))
            {
                if (outgoing.Count != 0) errors.Add(new("TerminalEdge", node.Key, "End cannot have outgoing edges."));
            }
            else if (IsKind(node, WorkflowNodeKinds.Decision))
            {
                var tableDiagnostics = new List<WorkflowDiagnostic>();
                DecisionTable? table = ReadDecision(node, tableDiagnostics);
                errors.AddRange(tableDiagnostics);
                if (table is not null && !tableDiagnostics.Any(error => error.Severity == "Error"))
                {
                    HashSet<string> routes = table.Rows.Select(row => row.Route).Append(table.DefaultRoute).ToHashSet(StringComparer.Ordinal);
                    if (outgoing.Count != routes.Count || outgoing.Any(edge => edge.RouteKey is null || !routes.Contains(edge.RouteKey))
                        || outgoing.Select(edge => edge.RouteKey).Distinct(StringComparer.Ordinal).Count() != routes.Count)
                        errors.Add(new("DecisionRoutes", node.Key, "Map each decision route, including default, to exactly one outgoing edge."));
                    if (forPublication) ValidateFixtures(table, node.Key, errors);
                }
            }
            else if (outgoing.Count != 1)
                errors.Add(new("SinglePath", node.Key, "This node requires exactly one outgoing edge."));
            if (IsKind(node, WorkflowNodeKinds.Trigger) && definition.Edges.Any(edge => edge.To == node.Key))
                errors.Add(new("TriggerInput", node.Key, "The trigger cannot have incoming edges."));
        }

        if (triggers.Count == 1 && !errors.Any(error => error.Code == "DanglingEdge"))
        {
            var colors = new Dictionary<string, int>(StringComparer.Ordinal);
            Visit(triggers[0].Key);
            foreach (WorkflowDefinitionNode node in definition.Nodes)
                if (!colors.ContainsKey(node.Key)) errors.Add(new("UnreachableNode", node.Key, "Node is not reachable from the trigger."));

            void Visit(string key)
            {
                if (colors.TryGetValue(key, out int color))
                {
                    if (color == 1) errors.Add(new("Cycle", key, "Arbitrary graph cycles are not supported."));
                    return;
                }
                colors[key] = 1;
                foreach (WorkflowDefinitionEdge edge in definition.Edges.Where(edge => edge.From == key)) Visit(edge.To);
                colors[key] = 2;
            }
        }

        return errors;
    }

    private static void ValidateFixtures(DecisionTable table, string path, List<WorkflowDiagnostic> diagnostics)
    {
        string[] required = ["Normal", "Boundary", "MissingData"];
        if (table.Fixtures is null || required.Any(kind => !table.Fixtures.Any(fixture => fixture?.Kind == kind)))
        {
            diagnostics.Add(new("PublicationFixtures", path, "Save Normal, Boundary and MissingData invoice fixtures before publication."));
            return;
        }
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (DecisionFixture fixture in table.Fixtures)
        {
            if (fixture is null || !WorkflowDefinitionCodec.IsKey(fixture.Name) || !names.Add(fixture.Name)
                || fixture.Facts is null || fixture.Facts.Count > 100 || fixture.ExpectedRoutes is null || !required.Contains(fixture.Kind))
            {
                diagnostics.Add(new("InvalidFixture", path, "Use unique fixture names, bounded facts and a supported fixture kind."));
                continue;
            }
            DecisionEvaluation actual = DecisionTableEvaluator.Evaluate(table, fixture.Facts);
            if (actual.Status != fixture.ExpectedStatus || !actual.Routes.SequenceEqual(fixture.ExpectedRoutes, StringComparer.Ordinal))
                diagnostics.Add(new("FixtureMismatch", $"{path}.fixtures[{fixture.Name}]", "Fixture outcome differs from its expected status or routes."));
        }
    }

    internal static DecisionTable? ReadDecision(WorkflowDefinitionNode node, List<WorkflowDiagnostic> errors)
    {
        try
        {
            DecisionTable table = node.Parameters.Deserialize<DecisionTable>(WorkflowDefinitionCodec.JsonOptions)
                ?? throw new JsonException("Decision parameters are required.");
            errors.AddRange(DecisionTableEvaluator.Validate(table, node.Key));
            return table;
        }
        catch (JsonException)
        {
            errors.Add(new("DecisionParameters", node.Key, "Provide typed decision-table parameters; expression strings are not supported."));
            return null;
        }
    }

    internal static bool IsKind(WorkflowDefinitionNode node, string kind) => string.Equals(node.Kind, kind, StringComparison.OrdinalIgnoreCase);
}
