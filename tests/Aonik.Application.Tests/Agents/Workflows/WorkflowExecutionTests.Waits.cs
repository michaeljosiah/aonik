using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services.Workflows;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;

namespace Aonik.Application.Tests.Agents.Workflows;

public partial class WorkflowExecutionTests
{
    [Fact]
    public async Task Timer_Should_KeepItsOriginalDeadlineAcrossFreshScopesAndDuplicateWakes_ThenReachTheFinancialGate()
    {
        using var host = new Host(); DateTime started = host.Clock.UtcNow;
        Guid runId = await host.Start(draft => WithWait(draft, new(1, DelaySeconds: 120)));

        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.WaitingForTimer);
        using (IServiceScope scope = host.Scope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
            WorkflowStepRun wait = await database.WorkflowStepRuns.SingleAsync(step => step.NodeKey == "wait");
            wait.DeadlineUtc.Should().Be(started.AddSeconds(120));
            (await database.WorkflowRuns.SingleAsync()).DeadlineUtc.Should().Be(started.AddDays(7));
            (await database.WorkflowCheckpoints.Select(checkpoint => checkpoint.RuntimeSchemaVersion).Distinct().ToListAsync()).Should().Equal(2);
            var wake = (await database.Set<OutboxMessage>().Where(value => value.EventType == typeof(WorkflowContinuationRequestedEvent).FullName).ToListAsync())
                .Select(value => JsonSerializer.Deserialize<WorkflowContinuationRequestedEvent>(value.Payload, OutboxSerialization.Options)!).Single(value => value.EventId == wait.Id);
            wake.NotBeforeUtc.Should().Be(wait.DeadlineUtc);
        }
        host.Clock.UtcNow = started.AddSeconds(60);
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.WaitingForTimer);
        host.Clock.UtcNow = started.AddSeconds(120);

        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);

        using IServiceScope final = host.Scope(); var saved = final.ServiceProvider.GetRequiredService<AgentsDbContext>();
        (await saved.WorkflowStepRuns.CountAsync(step => step.NodeKey == "wait")).Should().Be(1);
        (await saved.WorkflowStepRuns.SingleAsync(step => step.NodeKey == "wait")).DeadlineUtc.Should().Be(started.AddSeconds(120));
        (await saved.ToolApprovalRequests.CountAsync(request => request.WorkflowStepRunId != null)).Should().Be(1);
        (await final.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Draft");
    }

    [Fact]
    public async Task Timer_Should_NotBeShortenedByManualResume_AndCancellation_Should_BlockItsLateWake()
    {
        using var host = new Host(); Guid runId = await host.Start(draft => WithWait(draft, new(1, DelaySeconds: 120)));
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.WaitingForTimer);
        using (IServiceScope scope = host.Scope())
        {
            var runs = scope.ServiceProvider.GetRequiredService<IWorkflowRunService>(); WorkflowRunDetail run = (await runs.GetAsync(runId))!;
            Func<Task> early = () => runs.ResumeAsync(runId, new(run.ExpectedVersion));
            await early.Should().ThrowAsync<WorkflowConflictException>();
            await runs.CancelAsync(runId, new(run.ExpectedVersion));
        }
        host.Clock.UtcNow = host.Clock.UtcNow.AddMinutes(5);

        (await host.Continue(runId, continuation: 1)).Status.Should().Be(WorkflowRunStatuses.Cancelled);

        using IServiceScope final = host.Scope(); var database = final.ServiceProvider.GetRequiredService<AgentsDbContext>();
        (await database.ToolApprovalRequests.CountAsync(request => request.WorkflowStepRunId != null)).Should().Be(0);
        (await database.WorkflowStepRuns.SingleAsync(step => step.NodeKey == "wait")).Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task Timer_Should_AllowAnExplicitDueResume_WithoutResettingItsPersistedDeadline()
    {
        using var host = new Host(); Guid runId = await host.Start(draft => WithWait(draft, new(1, DelaySeconds: 120)));
        await host.Continue(runId); host.Clock.UtcNow = host.Clock.UtcNow.AddSeconds(120);
        using (IServiceScope scope = host.Scope())
        {
            var runs = scope.ServiceProvider.GetRequiredService<IWorkflowRunService>(); WorkflowRunDetail run = (await runs.GetAsync(runId))!;
            run.WaitUntilUtc.Should().Be(host.Clock.UtcNow);
            (await runs.ResumeAsync(runId, new(run.ExpectedVersion))).Status.Should().Be(WorkflowRunStatuses.Queued);
        }

        (await host.Continue(runId, 1)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);
    }

    [Fact]
    public async Task LifetimeLimit_Should_ExpireAHeldRunWithoutReplayingItsOldWake()
    {
        using var host = new Host();
        DateTime started = host.Clock.UtcNow;
        Guid runId = await host.Start(draft => WithWait(draft, new(1, DelaySeconds: 120)));
        host.Inactive.Add(host.Actor.Author);
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.NeedsAttention);
        host.Inactive.Remove(host.Actor.Author);
        host.Clock.UtcNow = started.AddMinutes(5);

        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.NeedsAttention);
        host.Clock.UtcNow = started.AddDays(7);
        var expired = await host.Continue(runId);

        expired.Status.Should().Be(WorkflowRunStatuses.Cancelled);
        expired.ErrorCode.Should().Be("RunDeadlineExceeded");
        using IServiceScope scope = host.Scope();
        (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowStepRuns.AnyAsync(step => step.RunId == runId)).Should().BeFalse();
        (await scope.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Draft");
    }

    [Fact]
    public async Task LifetimeLimit_Should_CancelAnExpiredWait_BeforeAQueuedFinancialEffect()
    {
        using var host = new Host(); Guid runId = await host.Start(draft => WithWait(draft, new(1, DelaySeconds: 120)));
        await host.Continue(runId); host.Clock.UtcNow = host.Clock.UtcNow.AddDays(7);

        var result = await host.Continue(runId);

        result.Status.Should().Be(WorkflowRunStatuses.Cancelled); result.ErrorCode.Should().Be("RunDeadlineExceeded");
        using IServiceScope scope = host.Scope();
        (await scope.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Draft");
        (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().ToolApprovalRequests.CountAsync(request => request.WorkflowStepRunId != null)).Should().Be(0);
    }

    [Fact]
    public void WaitSimulation_Should_BePure_AndRequireAnExplicitBoundedSchema()
    {
        var wait = new WorkflowDefinition(2, "Workflow", "wait-route", "Wait route", "",
            [new("start", "Trigger", "Start", JsonSerializer.SerializeToElement(new { })), new("wait", "Wait", "Wait", JsonSerializer.SerializeToElement(new WorkflowWaitParameters(1, DelaySeconds: 60), WorkflowDefinitionCodec.JsonOptions)),
                new("done", "End", "Done", JsonSerializer.SerializeToElement(new { }))],
            [new("one", "start", "wait", 0, null), new("two", "wait", "done", 0, null)]);
        WorkflowSimulator.Simulate(wait, new(new Dictionary<string, JsonElement>())).Status.Should().Be("Completed");
        WorkflowDefinitionValidator.Validate(wait with { SchemaVersion = 1 }, true).Should().Contain(value => value.Code == "CapabilityUnavailable");
        foreach (WorkflowWaitParameters invalid in new[] { new WorkflowWaitParameters(1), new(1, 0), new(1, 604801), new(1, 1, DateTime.UtcNow), new(1, UntilUtc: DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)) })
        {
            var bad = wait with { Nodes = wait.Nodes.Select(node => node.Key == "wait" ? node with { Parameters = JsonSerializer.SerializeToElement(invalid, WorkflowDefinitionCodec.JsonOptions) } : node).ToList() };
            WorkflowDefinitionValidator.Validate(bad, true).Should().Contain(value => value.Code == "WaitParameters");
        }
    }

    private static WorkflowSaveRequest WithWait(WorkflowSaveRequest draft, WorkflowWaitParameters wait) => draft with
    {
        SchemaVersion = 2,
        Nodes = draft.Nodes.Append(new("wait", "Wait", "Recorded wait", "", "", 0, 1, JsonSerializer.Serialize(wait, WorkflowDefinitionCodec.JsonOptions))).ToList(),
        Edges = draft.Edges.Select(edge => edge.FromClientId == "start" ? edge with { ToClientId = "wait" } : edge).Append(new("wait", "route", 0, "")).ToList(),
    };
}
