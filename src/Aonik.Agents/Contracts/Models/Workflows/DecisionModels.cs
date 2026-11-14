using System.Text.Json;

namespace Aonik.Agents.Contracts.Models.Workflows;

public sealed record DecisionTable(
    int SchemaVersion,
    string FactSchema,
    string HitPolicy,
    IReadOnlyList<DecisionRow> Rows,
    string DefaultRoute,
    IReadOnlyList<DecisionFixture>? Fixtures = null);

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
    string Route);

public sealed record DecisionComparison(string Field, string Operator, JsonElement? Value = null);

public sealed record DecisionComparisonTrace(
    string Field, string Operator, bool Matched, JsonElement? Actual, JsonElement? Expected);

public sealed record DecisionRowTrace(
    string RowId, bool Matched, IReadOnlyList<DecisionComparisonTrace> Comparisons);

public sealed record DecisionEvaluation(
    string Status,
    IReadOnlyList<string> Routes,
    IReadOnlyList<string> MatchedRowIds,
    IReadOnlyList<DecisionRowTrace> Trace,
    string? Reason = null);
