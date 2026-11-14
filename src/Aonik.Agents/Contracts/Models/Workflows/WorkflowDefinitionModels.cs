using System.Text.Json;

namespace Aonik.Agents.Contracts.Models.Workflows;

public sealed record WorkflowDefinition(
    int SchemaVersion,
    string DefinitionKind,
    string Slug,
    string Name,
    string Description,
    IReadOnlyList<WorkflowDefinitionNode> Nodes,
    IReadOnlyList<WorkflowDefinitionEdge> Edges);

public sealed record WorkflowDefinitionNode(string Key, string Kind, string Label, JsonElement Parameters);
public sealed record WorkflowDefinitionEdge(string Key, string From, string To, int FromIndex, string? RouteKey);
public sealed record WorkflowDiagnostic(string Code, string Path, string Message, string Severity = "Error");
public sealed record WorkflowValidation(
    bool IsValid, string DefinitionHash, IReadOnlyList<WorkflowDiagnostic> Diagnostics);
public sealed record WorkflowSimulationRequest(
    IReadOnlyDictionary<string, JsonElement> Facts,
    IReadOnlyDictionary<string, JsonElement>? ActionOutputs = null);
public sealed record WorkflowSimulationStep(
    string NodeKey, string Kind, string Status, DecisionEvaluation? Decision = null, JsonElement? FixtureOutput = null);
public sealed record WorkflowSimulation(
    string Mode, string Status, string DefinitionHash,
    IReadOnlyList<WorkflowSimulationStep> Steps, IReadOnlyList<WorkflowDiagnostic> Diagnostics);
public sealed record WorkflowCapability(string Kind, bool CanSimulate, bool CanExecute);
public sealed record WorkflowCapabilities(
    int SchemaVersion, int MaxNodes, int MaxEdges,
    IReadOnlyList<WorkflowCapability> NodeKinds, IReadOnlyList<string> FactSchemas,
    IReadOnlyList<DecisionFactField> InvoiceFacts,
    IReadOnlyList<string> ComparisonOperators,
    JsonElement DecisionParametersSchema);

public sealed record DecisionFactField(string Name, string Type, bool Required);
