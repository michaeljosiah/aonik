using System.Text.Json;

using FluentAssertions;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.Agents.Workflows.Graph;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Application.Tests.Agents.Workflows;

public class WorkflowCheckpointStoreTests
{
    [Fact]
    public async Task MafCheckpoint_Should_ResumeFromTheDatabase_WithNewContextStoreAndExecutors()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var tenant = new Tenant(Guid.NewGuid());
        var clock = new TestClock();
        var options = new DbContextOptionsBuilder<AgentsDbContext>().UseInMemoryDatabase($"WorkflowCheckpoint_{Guid.NewGuid()}").Options;
        Guid runId = Guid.NewGuid();
        Guid firstLease = Guid.NewGuid();
        CheckpointInfo checkpoint;
        var initialVisits = new List<string>();
        var resumedVisits = new List<string>();

        using (var database = new AgentsDbContext(options, tenant, clock: clock))
        {
            var row = new WorkflowRun { Id = runId, TenantId = tenant.Id, WorkflowVersionId = Guid.NewGuid(),
                Mode = "Live", Status = "Running", LeaseId = firstLease, LeaseExpiresAt = clock.UtcNow.AddMinutes(2) };
            database.WorkflowRuns.Add(row);
            await database.SaveChangesAsync(timeout.Token);
            var store = new WorkflowCheckpointStore(database, row, firstLease, clock, timeout.Token);
            await using StreamingRun initial = await InProcessExecution.RunStreamingAsync(Build(initialVisits), "invoice-reference",
                CheckpointManager.CreateJson(store), cancellationToken: timeout.Token);
            List<WorkflowEvent> events = await Events(initial, timeout.Token);
            events.OfType<RequestInfoEvent>().Should().ContainSingle();
            events.OfType<ExecutorFailedEvent>().Should().BeEmpty();
            checkpoint = initial.Checkpoints[^1];
            row.CheckpointId.Should().Be(checkpoint.CheckpointId);
        }

        using (var database = new AgentsDbContext(options, tenant, clock: clock))
        {
            WorkflowRun row = await database.WorkflowRuns.SingleAsync(timeout.Token);
            row.LeaseId = Guid.NewGuid();
            await database.SaveChangesAsync(timeout.Token);
            var store = new WorkflowCheckpointStore(database, row, row.LeaseId.Value, clock, timeout.Token);
            await using StreamingRun resumed = await InProcessExecution.ResumeStreamingAsync(Build(resumedVisits), checkpoint,
                CheckpointManager.CreateJson(store), timeout.Token);
            List<WorkflowEvent> waiting = await Events(resumed, timeout.Token);
            RequestInfoEvent pending = waiting.OfType<RequestInfoEvent>().Should().ContainSingle().Subject;
            await resumed.SendResponseAsync(pending.Request.CreateResponse(true));
            List<WorkflowEvent> complete = await Events(resumed, timeout.Token);

            complete.OfType<ExecutorFailedEvent>().Should().BeEmpty();
            complete.OfType<WorkflowOutputEvent>().Select(output => output.Data).Should().Equal("approved");
            initialVisits.Should().Equal("prepare");
            resumedVisits.Should().Equal("complete");
            (await database.WorkflowCheckpoints.CountAsync(timeout.Token)).Should().BeGreaterThan(0);
            (await store.RetrieveIndexAsync(checkpoint.SessionId)).Select(info => info.CheckpointId)
                .Should().Equal(await database.WorkflowCheckpoints.OrderBy(saved => saved.Sequence).Select(saved => saved.CheckpointId).ToListAsync(timeout.Token));
        }
    }

    [Fact]
    public async Task Store_Should_RefuseLostLease_IncompatibleFormatAndCrossTenantReads()
    {
        var tenant = new Tenant(Guid.NewGuid());
        var clock = new TestClock();
        var options = new DbContextOptionsBuilder<AgentsDbContext>().UseInMemoryDatabase($"WorkflowCheckpoint_{Guid.NewGuid()}").Options;
        using var database = new AgentsDbContext(options, tenant, clock: clock);
        var row = new WorkflowRun { TenantId = tenant.Id, WorkflowVersionId = Guid.NewGuid(), Mode = "Live",
            LeaseId = Guid.NewGuid(), LeaseExpiresAt = clock.UtcNow.AddMinutes(2) };
        database.WorkflowRuns.Add(row);
        await database.SaveChangesAsync();
        var store = new WorkflowCheckpointStore(database, row, row.LeaseId.Value, clock);
        CheckpointInfo checkpoint = await store.CreateCheckpointAsync("session-1", JsonSerializer.SerializeToElement(new { state = "waiting" }));
        WorkflowCheckpoint saved = await database.WorkflowCheckpoints.SingleAsync();
        saved.FormatVersion++;
        await database.SaveChangesAsync();

        Func<Task> incompatible = async () => { await store.RetrieveCheckpointAsync("session-1", checkpoint); };
        await incompatible.Should().ThrowAsync<WorkflowExecutionException>().WithMessage("CheckpointIncompatible");
        using var foreignDatabase = new AgentsDbContext(options, new Tenant(Guid.NewGuid()), clock: clock);
        var foreignStore = new WorkflowCheckpointStore(foreignDatabase, row, row.LeaseId.Value, clock);
        Func<Task> crossTenant = async () => { await foreignStore.RetrieveCheckpointAsync("session-1", checkpoint); };
        await crossTenant.Should().ThrowAsync<WorkflowExecutionException>().WithMessage("CheckpointMissing");
        row.LeaseId = Guid.NewGuid();
        await database.SaveChangesAsync();
        Func<Task> lost = async () => { await store.CreateCheckpointAsync("session-1", JsonSerializer.SerializeToElement(new { state = "invalid" })); };
        await lost.Should().ThrowAsync<WorkflowExecutionException>().WithMessage("RunLeaseLost");
    }

    private static Microsoft.Agents.AI.Workflows.Workflow Build(List<string> visits)
    {
        var prepare = new Prepare(visits);
        RequestPort request = RequestPort.Create<string, bool>("approval");
        var complete = new Complete(visits);
        return new WorkflowBuilder(prepare).AddEdge(prepare, request).AddEdge(request, complete).WithOutputFrom(complete).Build();
    }

    private static async Task<List<WorkflowEvent>> Events(StreamingRun run, CancellationToken cancellationToken)
    {
        var events = new List<WorkflowEvent>();
        await foreach (WorkflowEvent workflowEvent in run.WatchStreamAsync(blockOnPendingRequest: false, cancellationToken)) events.Add(workflowEvent);
        return events;
    }

    private sealed class Prepare(List<string> visits) : Executor<string, string>("prepare")
    {
        public override ValueTask<string> HandleAsync(string message, IWorkflowContext context, CancellationToken cancellationToken = default)
        { visits.Add(Id); return ValueTask.FromResult(message); }
    }
    private sealed class Complete(List<string> visits) : Executor<bool>("complete")
    {
        protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocol) => base.ConfigureProtocol(protocol).YieldsOutput<string>();
        public override async ValueTask HandleAsync(bool message, IWorkflowContext context, CancellationToken cancellationToken = default)
        { visits.Add(Id); await context.YieldOutputAsync(message ? "approved" : "rejected", cancellationToken); }
    }
    private sealed class Tenant(Guid id) : ITenantProvider
    {
        public Guid Id => id;
        public Guid GetCurrentTenantId() => id;
        public bool TryGetCurrentTenantId(out Guid tenantId) { tenantId = id; return true; }
    }
    private sealed class TestClock : IClock { public DateTime UtcNow => new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc); }
}
