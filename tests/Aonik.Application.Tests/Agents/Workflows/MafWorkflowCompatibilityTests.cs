using System.Globalization;

using FluentAssertions;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;

namespace Aonik.Application.Tests.Agents.Workflows;

public class MafWorkflowCompatibilityTests
{
    [Theory]
    [InlineData("4999.99", "standard")]
    [InlineData("5000.00", "elevated")]
    [InlineData("5000.01", "elevated")]
    public async Task ConditionalEdges_Should_ExecuteOnlyTheMatchingRoute_AtTheDecimalBoundary(
        string amountText,
        string expectedRoute)
    {
        // Arrange
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var visited = new List<string>();
        var entry = new PassThroughExecutor<decimal>("invoice-amount", visited);
        var standard = new OutputExecutor<decimal>("standard", visited, _ => "standard");
        var elevated = new OutputExecutor<decimal>("elevated", visited, _ => "elevated");
        Workflow workflow = new WorkflowBuilder(entry)
            .AddEdge<decimal>(entry, standard, amount => amount < 5000m)
            .AddEdge<decimal>(entry, elevated, amount => amount >= 5000m)
            .WithOutputFrom(standard, elevated)
            .Build();

        // Act
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
            workflow,
            decimal.Parse(amountText, CultureInfo.InvariantCulture),
            cancellationToken: timeout.Token);
        List<WorkflowEvent> events = await ReadUntilHaltAsync(run, timeout.Token);

        // Assert
        events.OfType<ExecutorFailedEvent>().Should().BeEmpty();
        events.OfType<WorkflowOutputEvent>().Select(e => e.Data)
            .Should().Equal(expectedRoute);
        visited.Should().Equal("invoice-amount", expectedRoute);
    }

    [Theory]
    [InlineData(true, "approved")]
    [InlineData(false, "rejected")]
    public async Task JsonCheckpoint_Should_RestorePendingRequest_WithNewWorkflowAndStoreInstances(
        bool response,
        string expectedOutput)
    {
        // Arrange
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        string checkpointDirectory = Path.Combine(
            Path.GetTempPath(), $"aonik-maf-compatibility-{Guid.NewGuid():N}");
        Directory.CreateDirectory(checkpointDirectory);
        var firstVisits = new List<string>();
        var resumedVisits = new List<string>();

        try
        {
            CheckpointInfo checkpoint;
            using (var store = new FileSystemJsonCheckpointStore(new DirectoryInfo(checkpointDirectory)))
            {
                CheckpointManager manager = CheckpointManager.CreateJson(store);
                await using StreamingRun initialRun = await InProcessExecution.RunStreamingAsync(
                    BuildRequestWorkflow(firstVisits),
                    "invoice-reference",
                    manager,
                    cancellationToken: timeout.Token);
                List<WorkflowEvent> initialEvents = await ReadUntilHaltAsync(initialRun, timeout.Token);

                initialEvents.OfType<ExecutorFailedEvent>().Should().BeEmpty();
                initialEvents.OfType<RequestInfoEvent>().Should().ContainSingle();
                initialEvents.OfType<WorkflowOutputEvent>().Should().BeEmpty();
                initialRun.Checkpoints.Should().NotBeEmpty();
                checkpoint = initialRun.Checkpoints[^1];
            }

            Directory.EnumerateFiles(checkpointDirectory, "*", SearchOption.AllDirectories)
                .Should().NotBeEmpty("the new store must restore serialized state, not process memory");

            // Act: rebuild every executor and the JSON store before restoring the pending request.
            using var resumedStore = new FileSystemJsonCheckpointStore(new DirectoryInfo(checkpointDirectory));
            CheckpointManager resumedManager = CheckpointManager.CreateJson(resumedStore);
            await using StreamingRun resumedRun = await InProcessExecution.ResumeStreamingAsync(
                BuildRequestWorkflow(resumedVisits), checkpoint, resumedManager, timeout.Token);
            List<WorkflowEvent> pendingEvents = await ReadUntilHaltAsync(resumedRun, timeout.Token);
            RequestInfoEvent pendingRequest = pendingEvents.OfType<RequestInfoEvent>().Should().ContainSingle().Subject;
            await resumedRun.SendResponseAsync(pendingRequest.Request.CreateResponse(response));
            List<WorkflowEvent> completedEvents = await ReadUntilHaltAsync(resumedRun, timeout.Token);

            // Assert
            pendingEvents.Concat(completedEvents).OfType<ExecutorFailedEvent>().Should().BeEmpty();
            completedEvents.OfType<WorkflowOutputEvent>().Select(e => e.Data)
                .Should().Equal(expectedOutput);
            firstVisits.Should().Equal("prepare");
            resumedVisits.Should().Equal(["result"], "the completed preparation must not run again");
        }
        finally
        {
            Directory.Delete(checkpointDirectory, recursive: true);
        }
    }

    private static Workflow BuildRequestWorkflow(List<string> visited)
    {
        var prepare = new PassThroughExecutor<string>("prepare", visited);
        RequestPort request = RequestPort.Create<string, bool>("human-response");
        var result = new OutputExecutor<bool>("result", visited, approved => approved ? "approved" : "rejected");
        return new WorkflowBuilder(prepare)
            .AddEdge(prepare, request)
            .AddEdge(request, result)
            .WithOutputFrom(result)
            .Build();
    }

    private static async Task<List<WorkflowEvent>> ReadUntilHaltAsync(
        StreamingRun run,
        CancellationToken cancellationToken = default)
    {
        var events = new List<WorkflowEvent>();
        await foreach (WorkflowEvent workflowEvent in run.WatchStreamAsync(
            blockOnPendingRequest: false, cancellationToken))
        {
            events.Add(workflowEvent);
        }

        return events;
    }

    private sealed class PassThroughExecutor<T>(string id, List<string> visited) : Executor<T, T>(id)
        where T : notnull
    {
        public override ValueTask<T> HandleAsync(
            T message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            visited.Add(Id);
            return ValueTask.FromResult(message);
        }
    }

    private sealed class OutputExecutor<T>(string id, List<string> visited, Func<T, string> output) : Executor<T>(id)
        where T : notnull
    {
        protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocol)
            => base.ConfigureProtocol(protocol).YieldsOutput<string>();

        public override async ValueTask HandleAsync(
            T message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            visited.Add(Id);
            await context.YieldOutputAsync(output(message), cancellationToken);
        }
    }
}
