using System.Text.Json;

using FluentAssertions;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Services.Workflows;

namespace Aonik.Application.Tests.Agents.Workflows;

public sealed class AdvancedDecisionTableTests
{
    [Theory]
    [InlineData("FirstMatch")]
    [InlineData("Unique")]
    [InlineData("Collect")]
    [InlineData("Score")]
    public void MissingOptionalFact_Should_RemainUnknownUnderNegation_AndNeverSelectAnApprovalRoute(string policy)
    {
        DecisionTable table = Table(policy, new DecisionRow("risk", 1, [], "risk", new("Not", Children: [Compare("invoice.hasRiskConcern", "eq", true)]),
            policy == "Score" ? "1" : null, policy == "Collect" ? new(Fact: "invoice.id") : null));

        DecisionEvaluation result = DecisionTableEvaluator.Evaluate(table, DecisionTableEvaluatorTests.Facts("42.00"));

        result.Status.Should().Be("NeedsManualReview"); result.Routes.Should().BeEmpty(); result.Reason.Should().Be("UnknownPredicate");
        result.Trace.Single().Predicate!.State.Should().Be("Unknown");
    }

    [Fact]
    public void CompoundPredicates_Should_ExplainAllAnyNot_AndUseExplicitIsMissing()
    {
        DecisionPredicate condition = new("All", Children: [Compare("invoice.currency", "eq", "GBP"),
            new("Any", Children: [Compare("invoice.total", "gt", "100.00"), Compare("invoice.hasRiskConcern", "isMissing")]),
            new("Not", Children: [Compare("invoice.status", "eq", "Cancelled")])]);
        DecisionTable table = Table("FirstMatch", new DecisionRow("normal", 1, [], "review", condition));

        DecisionEvaluation result = DecisionTableEvaluator.Evaluate(table, DecisionTableEvaluatorTests.Facts("42.00"));

        result.Status.Should().Be("Completed"); result.Routes.Should().Equal("review");
        result.Trace.Single().Comparisons.Should().HaveCount(4); result.Trace.Single().State.Should().Be("True");
    }

    [Fact]
    public void FirstMatch_Should_NotSkipAnEarlierUnknownRow_ForALaterTrueRow()
    {
        DecisionTable table = Table("FirstMatch", new("unknown", 1, [], "risk", Compare("invoice.hasRiskConcern", "eq", false)),
            new("later", 2, [new("invoice.status", "eq", JsonSerializer.SerializeToElement("Draft"))], "approve"));

        DecisionEvaluation result = DecisionTableEvaluator.Evaluate(table, DecisionTableEvaluatorTests.Facts("42.00"));

        result.Status.Should().Be("NeedsManualReview"); result.Trace.Should().ContainSingle();
    }

    [Fact]
    public void Unique_Should_ReportEveryMatchingRow_AsAConflictEvenWhenRoutesAgree()
    {
        DecisionTable table = Table("Unique", Match("first", 2, "review"), Match("second", 1, "review"));

        DecisionEvaluation result = DecisionTableEvaluator.Evaluate(table, DecisionTableEvaluatorTests.Facts("42.00"));

        result.Status.Should().Be("NeedsManualReview"); result.Reason.Should().Be("UniqueConflict"); result.MatchedRowIds.Should().Equal("second", "first");
    }

    [Fact]
    public void Collect_Should_ProduceOrderedDeduplicatedTypedItems_OnOneExplicitConsumerRoute()
    {
        DecisionTable table = Table("Collect", Match("last", 3, "last") with { CollectInvoiceId = new(Fact: "invoice.id") },
            Match("first", 1, "first") with { CollectInvoiceId = new(Fact: "invoice.id") },
            Match("same", 2, "first") with { CollectInvoiceId = new(Fact: "invoice.id") });

        DecisionEvaluation result = DecisionTableEvaluator.Evaluate(table, DecisionTableEvaluatorTests.Facts("42.00"));

        result.Status.Should().Be("Completed"); result.Routes.Should().Equal("consume"); result.Items!.Select(item => item.Key).Should().Equal("first", "last");
        result.Items.Should().OnlyContain(item => item.InvoiceId == Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        result.MatchedRowIds.Should().Equal("first", "same", "last");
    }

    [Fact]
    public void Collect_Should_RejectTheSameStableKey_WithDifferentInvoiceReferences()
    {
        DecisionTable table = Table("Collect", Match("one", 1, "same") with { CollectInvoiceId = new(Fact: "invoice.id") },
            Match("two", 2, "same") with { CollectInvoiceId = new(Literal: JsonSerializer.SerializeToElement(Guid.NewGuid())) });

        DecisionEvaluation result = DecisionTableEvaluator.Evaluate(table, DecisionTableEvaluatorTests.Facts("42.00"));

        result.Status.Should().Be("NeedsManualReview"); result.Reason.Should().Be("CollectionKeyConflict"); result.Items.Should().BeNull();
    }

    [Theory]
    [InlineData("0.1", "0.2", "0.3")]
    [InlineData("-1.25", "2.5", "1.25")]
    [InlineData("0.0001", "0.0002", "0.0003")]
    public void Score_Should_SumExactDecimalContributions_AndRecordEachContribution(string first, string second, string expected)
    {
        DecisionTable table = Table("Score", Match("one", 1, "unused") with { ScoreWeight = first }, Match("two", 2, "unused") with { ScoreWeight = second });

        DecisionEvaluation result = DecisionTableEvaluator.Evaluate(table, DecisionTableEvaluatorTests.Facts("42.00"));

        result.Status.Should().Be("Completed"); result.Routes.Should().Equal("score"); result.ScoreTotal.Should().Be(expected);
        result.Trace.Select(row => row.ScoreContribution).Should().Equal(first, second);
    }

    [Theory]
    [InlineData("0.00001")]
    [InlineData("10000.0001")]
    [InlineData("79228162514264337593543950335")]
    [InlineData("-79228162514264337593543950335")]
    [InlineData("1e2")]
    public void Score_Should_FailClosed_OnUnboundedOrImpreciseWeights(string weight)
    {
        DecisionTable table = Table("Score", Match("one", 1, "unused") with { ScoreWeight = weight });

        DecisionTableEvaluator.Evaluate(table, DecisionTableEvaluatorTests.Facts("42.00")).Status.Should().Be("Blocked");
    }

    [Theory]
    [InlineData("-1", "1")]
    [InlineData("1", "3")]
    [InlineData("-2", "0")]
    public void Score_Should_RejectOverlapsGapsAndUncoveredMaximums(string secondLower, string secondUpper)
    {
        DecisionTable table = Table("Score", Match("one", 1, "unused") with { ScoreWeight = "2" }) with
        { ScoreBands = [new("-2", "0", "one"), new(secondLower, secondUpper, "two")] };

        DecisionTableEvaluator.Validate(table).Should().Contain(error => error.Code == "ScoreBands");
    }

    [Fact]
    public void CurrencyMismatch_Should_RequireManualReview_InsteadOfTakingTheDefaultOrScoreBand()
    {
        DecisionTable table = Table("Score", Match("one", 1, "unused") with { ScoreWeight = "1" });
        Dictionary<string, JsonElement> facts = DecisionTableEvaluatorTests.Facts("42.00"); facts["invoice.currency"] = JsonSerializer.SerializeToElement("USD");

        DecisionEvaluation result = DecisionTableEvaluator.Evaluate(table, facts);

        result.Status.Should().Be("NeedsManualReview"); result.Reason.Should().Be("InvoiceCurrencyMismatch");
    }

    [Fact]
    public void PredicateBounds_Should_RejectDepthFive_AndMoreThanOneHundredNodes()
    {
        DecisionPredicate condition = Compare("invoice.status", "eq", "Draft");
        for (int index = 0; index < 4; index++) condition = new("Not", Children: [condition]);
        DecisionTableEvaluator.Validate(Table("FirstMatch", new DecisionRow("deep", 1, [], "review", condition))).Should().Contain(error => error.Code == "PredicateLimit");
        var wide = new DecisionPredicate("All", Children: Enumerable.Range(0, 100).Select(_ => Compare("invoice.status", "eq", "Draft")).ToList());
        DecisionTableEvaluator.Validate(Table("FirstMatch", new DecisionRow("wide", 1, [], "review", wide))).Should().Contain(error => error.Code == "PredicateLimit");
    }

    [Fact]
    public void VersionOne_Should_KeepItsMissingOptionalSemantics_AndRejectVersionTwoFeatures()
    {
        DecisionTable original = DecisionTableEvaluatorTests.InvoiceTable();
        DecisionTable advancedInV1 = original with { Rows = [original.Rows[0] with { Condition = Compare("invoice.status", "eq", "Draft") }] };

        DecisionTableEvaluator.Evaluate(original, DecisionTableEvaluatorTests.Facts("42.00")).Routes.Should().Equal("standard");
        DecisionTableEvaluator.Evaluate(advancedInV1, DecisionTableEvaluatorTests.Facts("42.00")).Status.Should().Be("Blocked");
    }

    internal static DecisionTable Table(string policy, params DecisionRow[] rows) => new(2, DecisionTableEvaluator.InvoiceSchema, policy, rows, "manual",
        Currency: "GBP", OptionalFactBehavior: "Unknown", CollectRoute: policy == "Collect" ? "consume" : null,
        ScoreBands: policy == "Score" ? [new("-500000", "500001", "score")] : null);
    private static DecisionPredicate Compare(string field, string operation, object? value = null) => new("Comparison", new(field, operation, value is null ? null : JsonSerializer.SerializeToElement(value)));
    private static DecisionRow Match(string key, int priority, string route) => new(key, priority, [new("invoice.status", "eq", JsonSerializer.SerializeToElement("Draft"))], route);
}
