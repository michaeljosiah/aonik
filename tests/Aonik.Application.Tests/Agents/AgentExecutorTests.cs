using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Workflows.Graph;
using Aonik.Agents.Workflows.Graph.Executors;
using FluentAssertions;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Moq;

namespace Aonik.Application.Tests.Agents;

/// <summary>
/// Unit tests for <see cref="AgentExecutor"/> — the MAF workflow node
/// that runs a domain agent for an editor "agent" node. Covers the
/// ParseParams pure helper plus the resolver-failure fallback path,
/// since spinning up a real <see cref="AIAgent"/> in a unit test would
/// cross from "unit" into "integration" territory.
/// xUnit + Moq + FluentAssertions per the project's standard testing stack.
/// </summary>
public class AgentExecutorTests
{
    private static readonly Guid NodeId = Guid.Parse("e1000000-0000-0000-0000-000000000001");

    // ── ParseParams (static, pure) ──────────────────────────────────────

    [Fact]
    public void ParseParams_Should_ExtractAgentAndTask_From_ValidJson()
    {
        var (agent, task) = AgentExecutor.ParseParams(
            "{\"agent\":\"Billing\",\"task\":\"Summarise outstanding invoices\"}");

        agent.Should().Be("Billing");
        task.Should().Be("Summarise outstanding invoices");
    }

    [Fact]
    public void ParseParams_Should_ReturnEmpty_When_JsonOmitsAgent()
    {
        var (agent, task) = AgentExecutor.ParseParams("{\"task\":\"do thing\"}");

        agent.Should().BeEmpty();
        task.Should().Be("do thing");
    }

    [Fact]
    public void ParseParams_Should_ReturnEmpty_When_JsonOmitsTask()
    {
        var (agent, task) = AgentExecutor.ParseParams("{\"agent\":\"Billing\"}");

        agent.Should().Be("Billing");
        task.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseParams_Should_ReturnEmpty_For_BlankInput(string? input)
    {
        var (agent, task) = AgentExecutor.ParseParams(input!);

        agent.Should().BeEmpty();
        task.Should().BeEmpty();
    }

    [Fact]
    public void ParseParams_Should_ReturnEmpty_For_MalformedJson()
    {
        // The "{ malformed" payload is invalid JSON; the helper swallows
        // JsonException and returns empty so a typo in editor params
        // doesn't crash the workflow run — the agent-not-found fallback
        // then kicks in downstream.
        var (agent, task) = AgentExecutor.ParseParams("{ malformed");

        agent.Should().BeEmpty();
        task.Should().BeEmpty();
    }

    // ── HandleAsync resolver-failure fallback ───────────────────────────

    [Fact]
    public async Task HandleAsync_Should_ReturnAdvisoryString_When_ResolverThrows()
    {
        // Resolver throws when the named agent is not registered. The
        // executor must catch that and yield a [agent:Name] not found
        // string so the workflow run continues rather than failing.
        var resolver = new Mock<IDomainAgentResolver>();
        resolver
            .Setup(r => r.ResolveAsync("UnknownAgent", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("no such descriptor"));

        var recorder = new Mock<IWorkflowRunRecorder>();

        var executor = new AgentExecutor(
            NodeId,
            agentName: "UnknownAgent",
            task: "irrelevant",
            resolver: resolver.Object,
            recorder: recorder.Object);

        var workflowContext = Mock.Of<IWorkflowContext>();

        var result = await executor.HandleAsync("upstream", workflowContext);

        result.Should().Be("[agent:UnknownAgent] not found — workflow advisory.");
        // Every executor records its visit on entry, even on the fallback path.
        recorder.Verify(r => r.RecordVisit(NodeId), Times.Once);
        resolver.Verify(r => r.ResolveAsync("UnknownAgent", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_RecordVisit_BeforeAttemptingResolution()
    {
        // RecordVisit must fire before ResolveAsync — otherwise a
        // resolver hang would leave the run trace missing the node and
        // produce a misleading replay in the editor.
        var resolveStarted = false;
        var visitedBeforeResolve = false;
        var resolver = new Mock<IDomainAgentResolver>();
        resolver
            .Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                resolveStarted = true;
                return Task.FromException<DomainAgentResolution>(new InvalidOperationException());
            });

        var recorder = new Mock<IWorkflowRunRecorder>();
        recorder
            .Setup(r => r.RecordVisit(NodeId))
            .Callback(() => visitedBeforeResolve = !resolveStarted);

        var executor = new AgentExecutor(
            NodeId,
            agentName: "AnyAgent",
            task: "",
            resolver: resolver.Object,
            recorder: recorder.Object);

        await executor.HandleAsync("input", Mock.Of<IWorkflowContext>());

        visitedBeforeResolve.Should().BeTrue(
            because: "the recorder must capture the node visit before the resolver runs");
    }
}
