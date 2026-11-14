using System.Text.Json;

namespace Aonik.Agents.Contracts.Models.Workflows;

public sealed record DecisionTable(
    int SchemaVersion,
    string FactSchema,
    string HitPolicy,
    IReadOnlyList<DecisionRow> Rows,
    string DefaultRoute,
    IReadOnlyList<DecisionFixture>? Fixtures = null,
    string? Currency = null,
    string? OptionalFactBehavior = null,
    string? CollectRoute = null,
    IReadOnlyList<DecisionScoreBand>? ScoreBands = null);

public sealed record DecisionFixture(
    string Name,
    string Kind,
    IReadOnlyDictionary<string, JsonElement> Facts,
    string ExpectedStatus,
    IReadOnlyList<string> ExpectedRoutes);

public sealed record DecisionRow(
    string Id,
    int Priority,
    IReadOnlyList<DecisionComparison> All,
    string Route,
    DecisionPredicate? Condition = null,
    string? ScoreWeight = null,
    WorkflowInputBinding? CollectInvoiceId = null);

public sealed record DecisionPredicate(string Kind, DecisionComparison? Comparison = null, IReadOnlyList<DecisionPredicate>? Children = null);
public sealed record DecisionScoreBand(string MinimumInclusive, string MaximumExclusive, string Route);

public sealed record DecisionComparison(string Field, string Operator, JsonElement? Value = null);

public sealed record DecisionComparisonTrace(
    string Field, string Operator, bool Matched, JsonElement? Actual, JsonElement? Expected, string? State = null);
public sealed record DecisionPredicateTrace(string Kind, string State, DecisionComparisonTrace? Comparison = null,
    IReadOnlyList<DecisionPredicateTrace>? Children = null);

public sealed record DecisionRowTrace(
    string RowId, bool Matched, IReadOnlyList<DecisionComparisonTrace> Comparisons,
    DecisionPredicateTrace? Predicate = null, string? State = null, string? ScoreContribution = null);

public sealed record DecisionEvaluation(
    string Status,
    IReadOnlyList<string> Routes,
    IReadOnlyList<string> MatchedRowIds,
    IReadOnlyList<DecisionRowTrace> Trace,
    string? Reason = null,
    IReadOnlyList<WorkflowLoopItem>? Items = null,
    string? ScoreTotal = null,
    DecisionLibraryEvidence? Library = null);

public sealed record DecisionLibraryContract(int SchemaVersion, IReadOnlyList<DecisionFactField> Inputs,
    IReadOnlyList<string> Routes, string OutputType);
public sealed record DecisionLibraryTriggerParameters(DecisionLibraryContract LibraryContract);
public sealed record DecisionLibraryReference(int SchemaVersion, WorkflowVersionPin Library,
    IReadOnlyDictionary<string, WorkflowInputBinding> Inputs, IReadOnlyList<string> Routes, string OutputType);
public sealed record DecisionLibraryEvidence(Guid VersionId, string DefinitionHash, IReadOnlyDictionary<string, JsonElement> Inputs);
