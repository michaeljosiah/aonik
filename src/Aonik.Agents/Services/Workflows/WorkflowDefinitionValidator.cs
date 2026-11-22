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
        WorkflowNodeKinds.Child, WorkflowNodeKinds.Parallel,
    };

    // Expand only alongside each governed executor and its recovery tests.
    internal static readonly IReadOnlySet<string> GovernedKinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        WorkflowNodeKinds.Trigger, WorkflowNodeKinds.Decision, WorkflowNodeKinds.Tool, WorkflowNodeKinds.End,
        WorkflowNodeKinds.Wait, WorkflowNodeKinds.Human, WorkflowNodeKinds.Child, WorkflowNodeKinds.Loop, WorkflowNodeKinds.Parallel,
    };

    public static WorkflowCapabilities Capabilities() => new(2, MaxNodes, MaxEdges,
        KnownKinds.Order(StringComparer.Ordinal).Select(kind => new WorkflowCapability(kind,
            true,
            GovernedKinds.Contains(kind))).ToList(), [DecisionTableEvaluator.InvoiceSchema, SharedKernel.Abstractions.Documents.FinancialDocumentActions.FactSchema],
        DecisionTableEvaluator.FactFields(), DecisionTableEvaluator.ComparisonOperators(), WorkflowDefinitionCodec.DecisionSchema(),
        SubjectTypes: ["Invoice", "Document"], SubjectFacts: new Dictionary<string, IReadOnlyList<DecisionFactField>>
        { ["Invoice"] = WorkflowSubjectContract.Facts("Invoice"), ["Document"] = WorkflowSubjectContract.Facts("Document") });

    public static IReadOnlyList<WorkflowDiagnostic> Validate(WorkflowDefinition definition, bool forPublication)
    {
        var errors = new List<WorkflowDiagnostic>();
        if (definition.SchemaVersion is not (1 or 2) || definition.DefinitionKind is not ("Workflow" or "DecisionLibrary"))
            errors.Add(new("DefinitionSchema", "definition", "Use a supported workflow definition schema."));
        DecisionLibraryContract? libraryContract = null;
        if (definition.DefinitionKind == "DecisionLibrary")
        {
            try { libraryContract = WorkflowDecisionLibraries.Read(definition).Contract; }
            catch (Exception exception) when (exception is ArgumentException or JsonException)
            { errors.Add(new("DecisionLibrary", "definition", exception.Message)); }
        }
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
        if (triggers.Count == 1 && definition.DefinitionKind == "Workflow")
        {
            try
            {
                if (WorkflowSubjectContract.Type(definition) == "Document" && definition.Nodes.Any(node => IsKind(node, WorkflowNodeKinds.Decision)))
                    errors.Add(new("DocumentDecision", "nodes", "Extract and confirm the document, then pass its invoice ID to an invoice decision child workflow."));
            }
            catch (ArgumentException exception) { errors.Add(new("SubjectContract", triggers[0].Key, exception.Message)); }
        }
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
            bool versionTwoControl = IsKind(node, WorkflowNodeKinds.Wait) || IsKind(node, WorkflowNodeKinds.Human) || WorkflowControlContracts.IsComposition(node);
            if (forPublication && (!GovernedKinds.Contains(node.Kind) || (definition.SchemaVersion == 1 && versionTwoControl)))
                errors.Add(new("CapabilityUnavailable", node.Key, "This governed executor is not yet available."));
            if (!forPublication && definition.SchemaVersion == 1 && versionTwoControl)
                errors.Add(new("CapabilityUnavailable", node.Key, "Simulation for this control-flow node is not yet available."));
            if (IsKind(node, WorkflowNodeKinds.Wait))
            {
                try { ValidateWait(node.Parameters.Deserialize<WorkflowWaitParameters>(WorkflowDefinitionCodec.JsonOptions)); }
                catch (Exception exception) when (exception is JsonException or ArgumentException)
                { errors.Add(new("WaitParameters", node.Key, "Provide exactly one UTC deadline or a delay of 1–604800 seconds, using wait schema 1 in workflow schema 2.")); }
            }
            List<WorkflowDefinitionEdge> outgoing = definition.Edges.Where(edge => edge.From == node.Key).ToList();
            if (IsKind(node, WorkflowNodeKinds.End))
            {
                if (outgoing.Count != 0) errors.Add(new("TerminalEdge", node.Key, "End cannot have outgoing edges."));
            }
            else if (WorkflowControlContracts.IsComposition(node))
            {
                try
                {
                    IReadOnlyList<string> routes = WorkflowControlContracts.Routes(node);
                    if (outgoing.Count != routes.Count || outgoing.Any(edge => edge.RouteKey is null || !routes.Contains(edge.RouteKey))
                        || outgoing.Select(edge => edge.RouteKey).Distinct(StringComparer.Ordinal).Count() != routes.Count)
                        errors.Add(new("ControlRoutes", node.Key, "Map every declared completion, failure and exhaustion route exactly once."));
                }
                catch (Exception exception) when (exception is JsonException or ArgumentException)
                { errors.Add(new("ControlParameters", node.Key, exception.Message)); }
            }
            else if (IsKind(node, WorkflowNodeKinds.Human))
            {
                try
                {
                    WorkflowHumanParameters human = WorkflowHumanTaskContract.Parameters(node);
                    HashSet<string> routes = human.Choices.Select(choice => choice.Route).Append(human.RejectRoute).Append(human.TimeoutRoute).ToHashSet(StringComparer.Ordinal);
                    if (outgoing.Count != routes.Count || outgoing.Any(edge => edge.RouteKey is null || !routes.Contains(edge.RouteKey))
                        || outgoing.Select(edge => edge.RouteKey).Distinct(StringComparer.Ordinal).Count() != routes.Count)
                        errors.Add(new("HumanRoutes", node.Key, "Map every response, rejection and timeout route to exactly one outgoing edge."));
                }
                catch (Exception exception) when (exception is JsonException or ArgumentException)
                { errors.Add(new("HumanParameters", node.Key, exception.Message)); }
            }
            else if (IsKind(node, WorkflowNodeKinds.Decision))
            {
                if (WorkflowDecisionLibraries.IsReference(node))
                {
                    try
                    {
                        DecisionLibraryReference reference = WorkflowDecisionLibraries.Reference(node);
                        if (definition.SchemaVersion != 2) errors.Add(new("DecisionSchema", node.Key, "A library consumer requires workflow schema 2."));
                        if (outgoing.Count != reference.Routes.Count || outgoing.Any(edge => edge.RouteKey is null || !reference.Routes.Contains(edge.RouteKey))
                            || outgoing.Select(edge => edge.RouteKey).Distinct(StringComparer.Ordinal).Count() != reference.Routes.Count)
                            errors.Add(new("DecisionRoutes", node.Key, "Map every pinned library route exactly once."));
                    }
                    catch (Exception exception) when (exception is ArgumentException or JsonException)
                    { errors.Add(new("DecisionLibrary", node.Key, exception.Message)); }
                    continue;
                }
                var tableDiagnostics = new List<WorkflowDiagnostic>();
                DecisionTable? table = ReadDecision(node, tableDiagnostics);
                errors.AddRange(tableDiagnostics);
                if (table is not null && !tableDiagnostics.Any(error => error.Severity == "Error"))
                {
                    if (table.SchemaVersion == 2 && definition.SchemaVersion != 2)
                        errors.Add(new("DecisionSchema", node.Key, "Advanced decisions require workflow schema 2."));
                    HashSet<string> routes = DecisionTableEvaluator.Routes(table).ToHashSet(StringComparer.Ordinal);
                    if (outgoing.Count != routes.Count || outgoing.Any(edge => edge.RouteKey is null || !routes.Contains(edge.RouteKey))
                        || outgoing.Select(edge => edge.RouteKey).Distinct(StringComparer.Ordinal).Count() != routes.Count)
                        errors.Add(new("DecisionRoutes", node.Key, "Map each decision route, including default, to exactly one outgoing edge."));
                    if (forPublication) ValidateFixtures(table, node.Key, errors, libraryContract);
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

        if (definition.DefinitionKind == "Workflow" && !errors.Any(error => error.Severity == "Error"))
        {
            foreach (WorkflowDefinitionNode loop in definition.Nodes.Where(node => IsKind(node, WorkflowNodeKinds.Loop)))
            {
                WorkflowLoopParameters parameters = WorkflowControlContracts.Loop(loop);
                if (parameters.ItemsFromDecision is not { } source) continue;
                WorkflowDefinitionNode? decision = definition.Nodes.SingleOrDefault(node => node.Key == source);
                if (decision is null || !IsCollectedDecision(decision) || !definition.Edges.Any(edge => edge.From == source && edge.To == loop.Key)
                    || definition.Edges.Any(edge => edge.To == loop.Key && edge.From != source))
                    errors.Add(new("CollectConsumer", loop.Key, "A decision collection must feed its explicitly bound foreach directly on every path."));
            }
            foreach (WorkflowDefinitionNode decision in definition.Nodes.Where(IsCollectedDecision))
                if (!definition.Nodes.Any(node => IsKind(node, WorkflowNodeKinds.Loop) && WorkflowControlContracts.Loop(node).ItemsFromDecision == decision.Key
                    && definition.Edges.Any(edge => edge.From == decision.Key && edge.To == node.Key)))
                    errors.Add(new("CollectConsumer", decision.Key, "Collect requires an explicit typed foreach consumer; matching rows never implicitly launch actions."));
        }

        return errors;
    }

    private static bool IsCollectedDecision(WorkflowDefinitionNode node) => IsKind(node, WorkflowNodeKinds.Decision)
        && (WorkflowDecisionLibraries.IsReference(node) ? WorkflowDecisionLibraries.Reference(node).OutputType == "InvoiceList"
            : node.Parameters.Deserialize<DecisionTable>(WorkflowDefinitionCodec.JsonOptions)?.HitPolicy == "Collect");

    private static void ValidateFixtures(DecisionTable table, string path, List<WorkflowDiagnostic> diagnostics, DecisionLibraryContract? library = null)
    {
        string[] required = ["Normal", "Boundary", "MissingData"];
        if (table.Fixtures is null || required.Any(kind => !table.Fixtures.Any(fixture => fixture?.Kind == kind)))
        {
            diagnostics.Add(new("PublicationFixtures", path, "Save Normal, Boundary and MissingData invoice fixtures before publication."));
            return;
        }
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < table.Fixtures.Count; index++)
        {
            DecisionFixture? fixture = table.Fixtures[index];
            string fixturePath = $"{path}.fixtures[{index}]";
            if (fixture is null)
            {
                diagnostics.Add(new("InvalidFixture", fixturePath, "Provide a fixture object."));
                continue;
            }
            if (!WorkflowDefinitionCodec.IsKey(fixture.Name) || !names.Add(fixture.Name))
            {
                diagnostics.Add(new("InvalidFixture", fixturePath + ".name", "Use a unique fixture name of 1–128 letters, numbers, dots, hyphens or underscores, starting with a letter or number. Spaces are not allowed."));
                continue;
            }
            if (fixture.Facts is null || fixture.Facts.Count > 100)
            {
                diagnostics.Add(new("InvalidFixture", fixturePath + ".facts", "Provide a facts object containing at most 100 facts."));
                continue;
            }
            if (fixture.ExpectedRoutes is null)
            {
                diagnostics.Add(new("InvalidFixture", fixturePath + ".expectedRoutes", "Provide an expectedRoutes array; use an empty array when manual review is expected."));
                continue;
            }
            if (!required.Contains(fixture.Kind))
            {
                diagnostics.Add(new("InvalidFixture", fixturePath + ".kind", "Choose Normal, Boundary or MissingData as the fixture kind."));
                continue;
            }
            DecisionEvaluation actual = library is null ? DecisionTableEvaluator.Evaluate(table, fixture.Facts)
                : fixture.Facts.Keys.Any(name => !library.Inputs.Any(field => field.Name == name))
                    ? new("NeedsManualReview", [], [], [], "UndeclaredLibraryInput") : DecisionTableEvaluator.EvaluateAdvanced(table, fixture.Facts, library.Inputs);
            if (actual.Status != fixture.ExpectedStatus)
                diagnostics.Add(new("FixtureMismatch", fixturePath + ".expectedStatus", "The expected status differs from this fixture's decision result. Review the simulation trace."));
            else if (!actual.Routes.SequenceEqual(fixture.ExpectedRoutes, StringComparer.Ordinal))
                diagnostics.Add(new("FixtureMismatch", fixturePath + ".expectedRoutes", "The expected routes differ from the routes selected by this decision table. Review the simulation trace."));
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

    internal static void ValidateWait(WorkflowWaitParameters? wait)
    {
        if (wait is null || wait.SchemaVersion != 1 || wait.DelaySeconds.HasValue == wait.UntilUtc.HasValue
            || wait.DelaySeconds is < 1 or > WorkflowControlLimits.MaximumLifetimeSeconds || wait.UntilUtc is { Kind: not DateTimeKind.Utc })
            throw new ArgumentException("Invalid bounded wait contract.");
    }
}
