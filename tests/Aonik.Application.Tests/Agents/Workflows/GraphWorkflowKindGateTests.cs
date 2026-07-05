using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Workflows.Graph;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Aonik.Application.Tests.Agents.Workflows;

/// <summary>
/// Pins the M2 fix: the graph runtime implements only a subset of the editor's
/// node vocabulary, so the run path must reject a graph that uses a not-yet-
/// implemented kind UP FRONT — with a clear message naming the offending kinds —
/// instead of executing partway (firing real notify/emit side effects) and then
/// throwing at the first unsupported node deep in the run.
/// </summary>
public class GraphWorkflowKindGateTests
{
    [Theory]
    [InlineData("Trigger")]
    [InlineData("Agent")]
    [InlineData("Notify")]
    [InlineData("Emit")]
    [InlineData("End")]
    [InlineData("agent")]   // case-insensitive
    [InlineData("EMIT")]
    public void IsRuntimeSupported_Should_ReturnTrue_ForRunnableKinds(string kind)
    {
        WorkflowNodeKinds.IsRuntimeSupported(kind).Should().BeTrue();
    }

    [Theory]
    [InlineData("Tool")]
    [InlineData("Decision")]
    [InlineData("Loop")]
    [InlineData("Human")]
    [InlineData("Wait")]
    [InlineData("bogus")]   // unknown kinds are also not runnable
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsRuntimeSupported_Should_ReturnFalse_ForDeferredOrUnknownKinds(string? kind)
    {
        WorkflowNodeKinds.IsRuntimeSupported(kind).Should().BeFalse();
    }

    [Fact]
    public void FindUnrunnableKinds_Should_ReturnEmpty_When_EveryNodeIsRunnable()
    {
        var graph = Graph(
            WorkflowNodeKinds.Trigger,
            WorkflowNodeKinds.Agent,
            WorkflowNodeKinds.Notify,
            WorkflowNodeKinds.Emit,
            WorkflowNodeKinds.End);

        GraphWorkflowBuilder.FindUnrunnableKinds(graph).Should().BeEmpty();
    }

    [Fact]
    public void FindUnrunnableKinds_Should_ReturnDistinctSortedKinds_When_DeferredKindsPresent()
    {
        // Duplicated Tool + mixed-case must collapse to one entry; result is sorted.
        var graph = Graph(
            WorkflowNodeKinds.Trigger,
            WorkflowNodeKinds.Tool,
            WorkflowNodeKinds.Agent,
            WorkflowNodeKinds.Decision,
            "tool",
            WorkflowNodeKinds.Human);

        GraphWorkflowBuilder.FindUnrunnableKinds(graph)
            .Should().Equal("Decision", "Human", "Tool");
    }

    [Fact]
    public void Build_Should_ThrowNamingUnrunnableKinds_BeforeConstructingExecutors_When_GraphUsesDeferredKinds()
    {
        // The graph puts a runnable Notify BEFORE the unsupported Tool. The gate
        // must reject the whole graph up front — so no executor is built and the
        // Notify side effect never fires. We register ONLY IWorkflowService in the
        // container: if Build reached executor construction it would fail resolving
        // ILoggerFactory / IEventBus instead of throwing the gate's NotSupportedException.
        var graph = Graph(
            WorkflowNodeKinds.Trigger,
            WorkflowNodeKinds.Notify,
            WorkflowNodeKinds.Tool);

        var service = new Mock<IWorkflowService>();
        service
            .Setup(s => s.GetBySlugAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph);

        var sp = new ServiceCollection()
            .AddSingleton(service.Object)
            .BuildServiceProvider();

        var act = () => GraphWorkflowBuilder.Build("demo_flow", sp, out _);

        act.Should().Throw<NotSupportedException>()
            .Which.Message.Should()
            .Contain("demo_flow").And
            .Contain("Tool").And
            .Contain("Runnable kinds");
    }

    [Fact]
    public void Build_Should_NotFireKindGate_ForRunnableKinds_ButFailLater_OnMissingEntryWiring()
    {
        // A trigger is runnable, so the kind gate must NOT fire. Build proceeds
        // past the gate and BuildExecutors (the trigger is virtual — no per-node
        // service needed), then fails for an unrelated, expected reason (the
        // trigger has no downstream edge) — proving the gate let a runnable-kind
        // graph through rather than rejecting it. AddLogging() satisfies the
        // ILoggerFactory BuildExecutors resolves before that check.
        var graph = Graph(WorkflowNodeKinds.Trigger);

        var service = new Mock<IWorkflowService>();
        service
            .Setup(s => s.GetBySlugAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph);

        var sp = new ServiceCollection()
            .AddSingleton(service.Object)
            .AddLogging()
            .BuildServiceProvider();

        var act = () => GraphWorkflowBuilder.Build("trigger_only", sp, out _);

        // A NotSupportedException here would mean the gate wrongly rejected a
        // runnable graph. Instead we expect the entry-point check to fail.
        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("no downstream edge");
    }

    private static WorkflowGraphResponse Graph(params string?[] kinds)
    {
        var nodes = kinds
            .Select((k, i) => new WorkflowGraphNode(
                Id: Guid.NewGuid(),
                Kind: k!,
                Label: k ?? "(null)",
                Summary: string.Empty,
                Notes: string.Empty,
                X: i * 100,
                Y: 0,
                ParamsJson: "{}"))
            .ToList();

        return new WorkflowGraphResponse(
            Id: Guid.NewGuid(),
            Slug: "test",
            Name: "Test",
            Description: string.Empty,
            State: "Active",
            Version: "v1",
            AutoRetry: false,
            OwnerColor: "#000000",
            OwnerName: "Test",
            Contributors: Array.Empty<string>(),
            Nodes: nodes,
            Edges: Array.Empty<WorkflowGraphEdge>(),
            Comments: Array.Empty<WorkflowGraphComment>());
    }
}
