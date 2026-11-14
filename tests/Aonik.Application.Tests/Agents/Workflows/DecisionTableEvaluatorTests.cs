using System.Text.Json;

using FluentAssertions;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Services.Workflows;

namespace Aonik.Application.Tests.Agents.Workflows;

public class DecisionTableEvaluatorTests
{
    [Theory]
    [InlineData("4999.99", "standard")]
    [InlineData("5000.00", "elevated")]
    [InlineData("5000.01", "elevated")]
    public void Evaluate_Should_UseDecimalBoundaries_AndExplainTheFirstMatchingRow(string amount, string route)
    {
        // Arrange
        DecisionTable table = InvoiceTable();

        // Act
        DecisionEvaluation result = DecisionTableEvaluator.Evaluate(table, Facts(amount));

        // Assert
        result.Status.Should().Be("Completed");
        result.Routes.Should().Equal(route);
        result.MatchedRowIds.Should().Equal(route);
        result.Trace.Last().Matched.Should().BeTrue();
        result.Trace.Should().HaveCount(route == "elevated" ? 1 : 2);
    }

    [Theory]
    [InlineData("invoice.id")]
    [InlineData("invoice.total")]
    [InlineData("invoice.currency")]
    [InlineData("invoice.status")]
    public void Evaluate_Should_RequireManualReview_When_ARequiredFactIsAbsent(string field)
    {
        // Arrange
        Dictionary<string, JsonElement> facts = Facts("100.00");
        facts.Remove(field);

        // Act
        DecisionEvaluation result = DecisionTableEvaluator.Evaluate(InvoiceTable(), facts);

        // Assert
        result.Status.Should().Be("NeedsManualReview");
        result.Routes.Should().BeEmpty();
        result.Reason.Should().Contain(field);
    }

    [Theory]
    [InlineData("ne", "manual")]
    [InlineData("isMissing", "specialist")]
    public void Evaluate_Should_NotInventOptionalFacts(string operation, string expected)
    {
        // Arrange
        var table = new DecisionTable(1, DecisionTableEvaluator.InvoiceSchema, "FirstMatch",
            [new("risk", 1, [new("invoice.hasRiskConcern", operation, JsonSerializer.SerializeToElement(true))], "specialist")], "manual");

        // Act
        DecisionEvaluation result = DecisionTableEvaluator.Evaluate(table, Facts("12.00"));

        // Assert
        result.Routes.Should().Equal(expected);
    }

    [Theory]
    [InlineData("invoice.total", "gte", "5000.00", "ComparisonType")]
    [InlineData("invoice.total", "gte", "\"5e3\"", "ComparisonType")]
    [InlineData("invoice.total", "gte", "\"5,000.00\"", "ComparisonType")]
    [InlineData("invoice.currency", "gt", "\"GBP\"", "ComparisonOperator")]
    [InlineData("invoice.currency", "in", "[\"GBP\",1]", "ComparisonType")]
    [InlineData("invoice.unknown", "eq", "\"x\"", "UnknownComparison")]
    [InlineData("invoice.total", "script", "\"5000\"", "UnknownComparison")]
    public void Validate_Should_RejectUnknownOrMistypedComparisons(string field, string operation, string json, string code)
    {
        // Arrange
        var table = new DecisionTable(1, DecisionTableEvaluator.InvoiceSchema, "FirstMatch",
            [new("row", 1, [new(field, operation, JsonSerializer.Deserialize<JsonElement>(json))], "route")], "manual");

        // Act
        IReadOnlyList<WorkflowDiagnostic> result = DecisionTableEvaluator.Validate(table);

        // Assert
        result.Should().Contain(diagnostic => diagnostic.Code == code);
        DecisionTableEvaluator.Evaluate(table, Facts("5000.00")).Status.Should().Be("Blocked");
    }

    [Fact]
    public void Validate_Should_RejectDuplicateIdsPriorities_AndMissingDefault()
    {
        // Arrange
        DecisionTable original = InvoiceTable();
        DecisionTable invalid = original with { Rows = [original.Rows[0], original.Rows[0]], DefaultRoute = "" };

        // Act
        IReadOnlyList<WorkflowDiagnostic> errors = DecisionTableEvaluator.Validate(invalid);

        // Assert
        errors.Should().Contain(error => error.Code == "DuplicateRow");
        errors.Should().Contain(error => error.Code == "DefaultRouteRequired");
        DecisionTableEvaluator.Validate(original with
        {
            Rows = [original.Rows[0], original.Rows[1] with { Priority = original.Rows[0].Priority }],
        }).Should().Contain(error => error.Code == "DuplicatePriority");
    }

    [Fact]
    public void Evaluate_Should_UseTheDefault_ForAnUnmatchedCurrency()
    {
        // Arrange
        Dictionary<string, JsonElement> facts = Facts("6000.00");
        facts["invoice.currency"] = JsonSerializer.SerializeToElement("USD");

        // Act
        DecisionEvaluation result = DecisionTableEvaluator.Evaluate(InvoiceTable(), facts);

        // Assert
        result.Routes.Should().Equal("manual");
        result.MatchedRowIds.Should().BeEmpty();
    }

    internal static DecisionTable InvoiceTable() => new(1, DecisionTableEvaluator.InvoiceSchema, "FirstMatch",
        [
            new("standard", 20,
                [new("invoice.currency", "eq", JsonSerializer.SerializeToElement("GBP")),
                 new("invoice.total", "lt", JsonSerializer.SerializeToElement("5000.00"))], "standard"),
            new("elevated", 10,
                [new("invoice.currency", "eq", JsonSerializer.SerializeToElement("GBP")),
                 new("invoice.total", "gte", JsonSerializer.SerializeToElement("5000.00"))], "elevated"),
        ], "manual");

    internal static Dictionary<string, JsonElement> Facts(string amount) => new(StringComparer.Ordinal)
    {
        ["invoice.id"] = JsonSerializer.SerializeToElement("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        ["invoice.status"] = JsonSerializer.SerializeToElement("Draft"),
        ["invoice.total"] = JsonSerializer.SerializeToElement(amount),
        ["invoice.currency"] = JsonSerializer.SerializeToElement("GBP"),
    };
}
