using System.Text.Json;

using FluentAssertions;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Services.Workflows;

namespace Aonik.Application.Tests.Agents.Workflows;

public class WorkflowDefinitionTests
{
    [Fact]
    public void Codec_Should_HashCanonicalParameters_IndependentlyOfPropertyOrderAndWhitespace()
    {
        // Arrange
        WorkflowDefinition first = Definition();
        WorkflowDefinition second = first with { Nodes = first.Nodes.Select(node => node.Key == "start"
            ? node with { Parameters = JsonSerializer.Deserialize<JsonElement>("{ \"b\": 2, \"a\": 1 }") } : node).ToList() };

        // Act
        string firstJson = WorkflowDefinitionCodec.Serialize(first);
        string secondJson = WorkflowDefinitionCodec.Serialize(second);

        // Assert
        firstJson.Should().Be(secondJson);
        WorkflowDefinitionCodec.Hash(firstJson).Should().Be(WorkflowDefinitionCodec.Hash(secondJson));
    }

    [Theory]
    [InlineData("4999.99", "standard")]
    [InlineData("5000.00", "elevated")]
    public void Simulation_Should_SelectOnePath_AndReturnItsExplanation(string amount, string route)
    {
        // Arrange
        WorkflowDefinition definition = Definition();

        // Act
        WorkflowSimulation result = WorkflowSimulator.Simulate(definition, new(DecisionTableEvaluatorTests.Facts(amount)));

        // Assert
        result.Mode.Should().Be("Fixture");
        result.Status.Should().Be("Completed");
        result.Steps.Select(step => step.NodeKey).Should().Equal("start", "decision", route);
        result.Steps[1].Decision!.MatchedRowIds.Should().Equal(route);
    }

    [Fact]
    public void Validation_Should_RequireEveryDecisionRoute_AndRejectCyclesOrUnreachableNodes()
    {
        // Arrange
        WorkflowDefinition definition = Definition();
        WorkflowDefinition missingDefault = definition with { Edges = definition.Edges.Where(edge => edge.RouteKey != "manual").ToList() };
        WorkflowDefinition cycle = definition with { Edges = definition.Edges.Select(edge => edge.RouteKey == "standard"
            ? edge with { To = "decision" } : edge).ToList() };

        // Act
        IReadOnlyList<WorkflowDiagnostic> missing = WorkflowDefinitionValidator.Validate(missingDefault, false);
        IReadOnlyList<WorkflowDiagnostic> cyclic = WorkflowDefinitionValidator.Validate(cycle, false);

        // Assert
        missing.Should().Contain(error => error.Code == "DecisionRoutes");
        cyclic.Should().Contain(error => error.Code == "Cycle");
        cyclic.Should().Contain(error => error.Code == "UnreachableNode");
    }

    [Fact]
    public void Simulation_Should_RequireExplicitActionFixtures_WithoutExecutingTheAction()
    {
        // Arrange
        WorkflowDefinition original = Definition();
        WorkflowDefinition definition = original with
        {
            Nodes = original.Nodes.Append(new("action", "Tool", "Issue invoice",
                JsonSerializer.Deserialize<JsonElement>("{\"actionKey\":\"finance_issue_invoice\"}"))).ToList(),
            Edges = original.Edges.Select(edge => edge.RouteKey == "standard" ? edge with { To = "action" } : edge)
                .Append(new("action-end", "action", "standard", 0, null)).ToList(),
        };
        var request = new WorkflowSimulationRequest(DecisionTableEvaluatorTests.Facts("100.00"));

        // Act
        WorkflowSimulation held = WorkflowSimulator.Simulate(definition, request);
        WorkflowSimulation simulated = WorkflowSimulator.Simulate(definition, request with
        {
            ActionOutputs = new Dictionary<string, JsonElement> { ["action"] = JsonSerializer.SerializeToElement(new { status = "fixture-only" }) },
        });

        // Assert
        held.Status.Should().Be("NeedsFixture");
        simulated.Status.Should().Be("Completed");
        simulated.Steps.Single(step => step.NodeKey == "action").FixtureOutput!.Value.GetProperty("status").GetString()
            .Should().Be("fixture-only");
    }

    [Fact]
    public void Validation_Should_RefuseLegacyExpressions_AndUnavailableGovernedExecutors()
    {
        // Arrange
        WorkflowDefinition definition = Definition();
        WorkflowDefinition expression = definition with { Nodes = definition.Nodes.Select(node => node.Key == "decision"
            ? node with { Parameters = JsonSerializer.Deserialize<JsonElement>("{\"expr\":\"amount > 5000\"}") } : node).ToList() };

        // Act
        IReadOnlyList<WorkflowDiagnostic> errors = WorkflowDefinitionValidator.Validate(expression, true);

        // Assert
        errors.Should().Contain(error => error.Code == "DecisionParameters");
        errors.Should().Contain(error => error.Code == "CapabilityUnavailable");
    }

    [Fact]
    public void PublicationValidation_Should_RequireSavedFixtures_AndRefuseAnIncorrectExpectedRoute()
    {
        // Arrange
        WorkflowDefinition original = Definition();
        Dictionary<string, JsonElement> missingFacts = DecisionTableEvaluatorTests.Facts("100.00");
        missingFacts.Remove("invoice.currency");
        DecisionTable table = DecisionTableEvaluatorTests.InvoiceTable() with
        {
            Fixtures =
            [
                new("normal", "Normal", DecisionTableEvaluatorTests.Facts("100.00"), "Completed", ["standard"]),
                new("boundary", "Boundary", DecisionTableEvaluatorTests.Facts("5000.00"), "Completed", ["elevated"]),
                new("missing", "MissingData", missingFacts, "NeedsManualReview", []),
            ],
        };
        WorkflowDefinition validFixtures = WithTable(table);
        WorkflowDefinition wrongFixture = WithTable(table with
        {
            Fixtures = table.Fixtures!.Select(fixture => fixture.Kind == "Boundary"
                ? fixture with { ExpectedRoutes = ["standard"] } : fixture).ToList(),
        });

        // Act
        IReadOnlyList<WorkflowDiagnostic> missing = WorkflowDefinitionValidator.Validate(original, true);
        IReadOnlyList<WorkflowDiagnostic> valid = WorkflowDefinitionValidator.Validate(validFixtures, true);
        IReadOnlyList<WorkflowDiagnostic> wrong = WorkflowDefinitionValidator.Validate(wrongFixture, true);

        // Assert
        missing.Should().Contain(error => error.Code == "PublicationFixtures");
        valid.Should().NotContain(error => error.Code == "PublicationFixtures" || error.Code == "FixtureMismatch");
        wrong.Should().Contain(error => error.Code == "FixtureMismatch");

        WorkflowDefinition WithTable(DecisionTable value) => original with
        {
            Nodes = original.Nodes.Select(node => node.Key == "decision"
                ? node with { Parameters = JsonSerializer.SerializeToElement(value, WorkflowDefinitionCodec.JsonOptions) } : node).ToList(),
        };
    }

    private static WorkflowDefinition Definition() => new(1, "Workflow", "invoice-routing", "Invoice routing", "",
        [
            new("start", "Trigger", "Start", JsonSerializer.Deserialize<JsonElement>("{\"a\":1,\"b\":2}")),
            new("decision", "Decision", "Invoice policy", JsonSerializer.SerializeToElement(DecisionTableEvaluatorTests.InvoiceTable(), WorkflowDefinitionCodec.JsonOptions)),
            new("standard", "End", "Standard", JsonSerializer.Deserialize<JsonElement>("{}")),
            new("elevated", "End", "Elevated", JsonSerializer.Deserialize<JsonElement>("{}")),
            new("manual", "End", "Manual", JsonSerializer.Deserialize<JsonElement>("{}")),
        ],
        [
            new("entry", "start", "decision", 0, null),
            new("standard", "decision", "standard", 0, "standard"),
            new("elevated", "decision", "elevated", 1, "elevated"),
            new("manual", "decision", "manual", 2, "manual"),
        ]);
}
