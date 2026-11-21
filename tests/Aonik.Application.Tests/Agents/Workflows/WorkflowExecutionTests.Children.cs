using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services.Workflows;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;

namespace Aonik.Application.Tests.Agents.Workflows;

public partial class WorkflowExecutionTests
{
    [Fact]
    public async Task PinnedChild_Should_UseNativeMafRequestForwarding_AndResumeTheParentOnce_FromItsDurableApprovedReceipt()
    {
        using var host = new Host(); WorkflowVersionPin pin = await host.PublishChild();
        Guid parentId = await host.Start(draft => WithChild(draft, pin));
        Guid sourceBinding = Guid.NewGuid();
        using (IServiceScope scope = host.Scope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
            WorkflowRun parent = await database.WorkflowRuns.SingleAsync(run => run.Id == parentId);
            parent.RootRunId = parent.Id; parent.TriggerPathJson = JsonSerializer.Serialize(new[] { sourceBinding });
            await database.SaveChangesAsync();
        }

        var result = await host.Continue(parentId);

        result.Status.Should().Be(WorkflowRunStatuses.WaitingForChildren, result.ErrorCode);
        (await host.Continue(parentId)).Status.Should().Be(WorkflowRunStatuses.WaitingForChildren);
        Guid childId;
        using (IServiceScope scope = host.Scope())
        {
            AgentsDbContext database = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
            WorkflowRun child = await database.WorkflowRuns.SingleAsync(run => run.ParentRunId == parentId); childId = child.Id;
            child.WorkflowVersionId.Should().Be(pin.VersionId); child.DefinitionHash.Should().Be(pin.DefinitionHash);
            child.Depth.Should().Be(1); child.ExecutionRootRunId.Should().Be(parentId);
            child.RootRunId.Should().Be(parentId); child.CausationRunId.Should().Be(parentId);
            JsonSerializer.Deserialize<Guid[]>(child.TriggerPathJson).Should().Equal(sourceBinding);
            child.DeadlineUtc.Should().Be((await database.WorkflowRuns.SingleAsync(run => run.Id == parentId)).DeadlineUtc);
        }
        (await host.Continue(childId)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);
        using (IServiceScope scope = host.Scope())
        {
            AgentsDbContext database = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
            WorkflowRun child = await database.WorkflowRuns.SingleAsync(run => run.Id == childId);
            ToolApprovalRequest request = await database.ToolApprovalRequests.SingleAsync(request => request.WorkflowStepRunId == child.PendingStepRunId);
            host.Actor.Id = host.Reviewer;
            (await scope.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(request.Id, new(ToolApprovalDecisionType.Approve))).Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
        }
        (await host.Continue(childId)).Status.Should().Be(WorkflowRunStatuses.Completed);
        (await host.Continue(childId)).Status.Should().Be(WorkflowRunStatuses.Completed);
        var resumed = await host.Continue(parentId);
        resumed.Status.Should().Be(WorkflowRunStatuses.Completed, resumed.ErrorCode);
        (await host.Continue(parentId)).Status.Should().Be(WorkflowRunStatuses.Completed);
        using IServiceScope final = host.Scope(); var saved = final.ServiceProvider.GetRequiredService<AgentsDbContext>();
        (await saved.WorkflowRuns.CountAsync(run => run.ParentRunId == parentId)).Should().Be(1);
        (await saved.WorkflowStepRuns.SingleAsync(step => step.RunId == parentId && step.NodeKey == "child")).SelectedRoute.Should().Be("completed");
        var wake = await saved.Set<OutboxMessage>().SingleAsync(value => value.EventId == childId);
        JsonSerializer.Deserialize<WorkflowContinuationRequestedEvent>(wake.Payload, OutboxSerialization.Options)!.RunId.Should().Be(parentId);
        (await final.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).IssueOperationId.Should().NotBeNull();
    }

    [Fact]
    public async Task ParentCancellation_Should_WithdrawItsChildApproval_AndPreventLateChildAndParentEffects()
    {
        using var host = new Host(); WorkflowVersionPin pin = await host.PublishChild();
        Guid parentId = await host.Start(draft => WithChild(draft, pin)); await host.Continue(parentId);
        Guid childId;
        using (IServiceScope scope = host.Scope()) childId = (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowRuns.SingleAsync(run => run.ParentRunId == parentId)).Id;
        await host.Continue(childId);
        using (IServiceScope scope = host.Scope())
        {
            var runs = scope.ServiceProvider.GetRequiredService<IWorkflowRunService>(); var parent = (await runs.GetAsync(parentId))!;
            parent.Children.Should().ContainSingle(child => child.Id == childId && child.Status == WorkflowRunStatuses.WaitingForApproval);
            await runs.CancelAsync(parentId, new(parent.ExpectedVersion));
            (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().ToolApprovalRequests.SingleAsync(request => request.WorkflowStepRunId != null)).Status.Should().Be(ToolApprovalRequestStatus.Cancelled);
        }
        (await host.Continue(childId)).Status.Should().Be(WorkflowRunStatuses.Cancelled);
        (await host.Continue(parentId, 1)).Status.Should().Be(WorkflowRunStatuses.Cancelled);
        using IServiceScope final = host.Scope();
        (await final.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Draft");
    }

    [Fact]
    public async Task ChildFinalCheckpoint_Should_RecoverItsRecordedEnd_WithoutRepeatingTheCommittedEffect()
    {
        using var host = new Host(); WorkflowVersionPin pin = await host.PublishChild();
        Guid parentId = await host.Start(draft => WithChild(draft, pin)); await host.Continue(parentId);
        Guid childId; Guid operationId;
        using (IServiceScope scope = host.Scope()) childId = (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowRuns.SingleAsync(run => run.ParentRunId == parentId)).Id;
        await host.Continue(childId);
        using (IServiceScope scope = host.Scope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
            WorkflowRun child = await database.WorkflowRuns.SingleAsync(run => run.Id == childId);
            Guid requestId = (await database.ToolApprovalRequests.SingleAsync(request => request.WorkflowStepRunId == child.PendingStepRunId)).Id;
            host.Actor.Id = host.Reviewer;
            await scope.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(requestId, new(ToolApprovalDecisionType.Approve));
        }
        (await host.Continue(childId)).Status.Should().Be(WorkflowRunStatuses.Completed);
        using (IServiceScope scope = host.Scope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
            WorkflowRun child = await database.WorkflowRuns.SingleAsync(run => run.Id == childId);
            child.CheckpointId.Should().NotBeNull();
            // The native final checkpoint and End evidence committed, but the host died before finalizing the run and its parent wake.
            child.Status = WorkflowRunStatuses.Queued; child.CompletedAt = null; child.Outcome = null; child.ErrorCode = null;
            database.Set<OutboxMessage>().RemoveRange(await database.Set<OutboxMessage>().Where(message => message.EventId == childId).ToListAsync());
            await database.SaveChangesAsync();
            operationId = (await scope.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).IssueOperationId!.Value;
        }

        var recovered = await host.Continue(childId);

        recovered.Status.Should().Be(WorkflowRunStatuses.Completed, recovered.ErrorCode); recovered.ErrorCode.Should().BeNull();
        (await host.Continue(parentId)).Status.Should().Be(WorkflowRunStatuses.Completed);
        using IServiceScope final = host.Scope(); var saved = final.ServiceProvider.GetRequiredService<AgentsDbContext>();
        (await saved.Set<OutboxMessage>().CountAsync(message => message.EventId == childId)).Should().Be(1);
        (await saved.WorkflowStepRuns.SingleAsync(step => step.RunId == childId && step.ActionKey == "finance.invoices.issue")).Attempts.Should().Be(1);
        (await final.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).IssueOperationId.Should().Be(operationId);
    }

    [Fact]
    public async Task NestedChildFailure_Should_RemainAFailure_WhenAnErrorRouteUsesAnUnconfiguredEnd()
    {
        using var host = new Host(); WorkflowVersionPin leafPin = await host.PublishChild();
        WorkflowVersionPin wrapperPin = await host.PublishChild(draft =>
        {
            WorkflowSaveRequest wrapper = WithChild(draft, leafPin);
            return wrapper with { Nodes = wrapper.Nodes.Select(node => node.ClientId == "manual" ? node with { ParamsJson = "{}" } : node).ToList() };
        }, "child-wrapper");
        Guid parentId = await host.Start(draft => WithChild(draft, wrapperPin)); await host.Continue(parentId);
        Guid wrapperId; Guid leafId;
        using (IServiceScope scope = host.Scope()) wrapperId = (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowRuns.SingleAsync(run => run.ParentRunId == parentId)).Id;
        await host.Continue(wrapperId);
        using (IServiceScope scope = host.Scope()) leafId = (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowRuns.SingleAsync(run => run.ParentRunId == wrapperId)).Id;
        await host.Continue(leafId);
        using (IServiceScope scope = host.Scope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
            WorkflowRun leaf = await database.WorkflowRuns.SingleAsync(run => run.Id == leafId);
            Guid requestId = (await database.ToolApprovalRequests.SingleAsync(request => request.WorkflowStepRunId == leaf.PendingStepRunId)).Id;
            host.Actor.Id = host.Reviewer;
            await scope.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(requestId, new(ToolApprovalDecisionType.Reject));
        }
        (await host.Continue(leafId)).ErrorCode.Should().Be("ExecutionStopped");

        var wrapperResult = await host.Continue(wrapperId);

        wrapperResult.Status.Should().Be(WorkflowRunStatuses.Completed); wrapperResult.ErrorCode.Should().Be("ExecutionStopped");
        (await host.Continue(parentId)).Outcome.Should().Be("NeedsManualReview");
        using IServiceScope final = host.Scope(); var saved = final.ServiceProvider.GetRequiredService<AgentsDbContext>();
        (await saved.WorkflowStepRuns.SingleAsync(step => step.RunId == parentId && step.NodeKey == "child")).SelectedRoute.Should().Be("error");
        (await final.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Draft");
    }

    [Fact]
    public async Task ChildCancellation_Should_TakeTheParentsExplicitErrorRoute_AndNeverTreatItAsSuccess()
    {
        using var host = new Host(); WorkflowVersionPin pin = await host.PublishChild();
        Guid parentId = await host.Start(draft => WithChild(draft, pin)); await host.Continue(parentId);
        using (IServiceScope scope = host.Scope())
        {
            Guid childId = (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowRuns.SingleAsync(run => run.ParentRunId == parentId)).Id;
            var runs = scope.ServiceProvider.GetRequiredService<IWorkflowRunService>(); var child = (await runs.GetAsync(childId))!;
            await runs.CancelAsync(childId, new(child.ExpectedVersion));
            await host.Continue(childId, 1);
        }
        var parentResult = await host.Continue(parentId);
        parentResult.Status.Should().Be(WorkflowRunStatuses.Completed); parentResult.Outcome.Should().Be("NeedsManualReview");
        using IServiceScope final = host.Scope();
        (await final.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Draft");
    }

    [Fact]
    public async Task RejectedChildAction_Should_TakeTheErrorRoute_WithoutBeingReportedAsASuccessfulChild()
    {
        using var host = new Host(); WorkflowVersionPin pin = await host.PublishChild();
        Guid parentId = await host.Start(draft => WithChild(draft, pin)); await host.Continue(parentId);
        Guid childId;
        using (IServiceScope scope = host.Scope()) childId = (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowRuns.SingleAsync(child => child.ParentRunId == parentId)).Id;
        await host.Continue(childId);
        using (IServiceScope scope = host.Scope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AgentsDbContext>(); WorkflowRun child = await database.WorkflowRuns.SingleAsync(child => child.Id == childId);
            Guid requestId = (await database.ToolApprovalRequests.SingleAsync(request => request.WorkflowStepRunId == child.PendingStepRunId)).Id;
            host.Actor.Id = host.Reviewer;
            await scope.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(requestId, new(ToolApprovalDecisionType.Reject));
        }
        var stopped = await host.Continue(childId);
        stopped.Status.Should().Be(WorkflowRunStatuses.Completed); stopped.ErrorCode.Should().Be("ExecutionStopped");
        var parentResult = await host.Continue(parentId);
        parentResult.Status.Should().Be(WorkflowRunStatuses.Completed); parentResult.Outcome.Should().Be("NeedsManualReview");
        using IServiceScope final = host.Scope();
        (await final.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Draft");
    }

    [Fact]
    public async Task ChildDecisionWithBlockedFacts_Should_ReportManualReview_ThroughTheNativeStopEvent()
    {
        using var host = new Host(); WorkflowVersionPin pin = await host.PublishChild(draft => draft);
        Guid blockedId = await host.AddInvoice();
        using (IServiceScope scope = host.Scope())
        {
            var finance = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
            (await finance.Invoices.SingleAsync(invoice => invoice.Id == blockedId)).Currency = ""; await finance.SaveChangesAsync();
        }
        Guid parentId = await host.Start(draft =>
        {
            WorkflowSaveRequest parent = WithChild(draft, pin);
            return parent with { Nodes = parent.Nodes.Select(node => node.ClientId == "child" ? node with { ParamsJson = JsonSerializer.Serialize(
                new WorkflowChildParameters(1, pin, new(Literal: JsonSerializer.SerializeToElement(blockedId))), WorkflowDefinitionCodec.JsonOptions) } : node).ToList() };
        });
        await host.Continue(parentId); Guid childId;
        using (IServiceScope scope = host.Scope()) childId = (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowRuns.SingleAsync(child => child.ParentRunId == parentId)).Id;
        var child = await host.Continue(childId);
        child.Status.Should().Be(WorkflowRunStatuses.Completed, child.ErrorCode); child.Outcome.Should().Be("NeedsManualReview"); child.ErrorCode.Should().Be("ExecutionStopped");
        (await host.Continue(parentId)).Outcome.Should().Be("NeedsManualReview");
    }

    private static WorkflowSaveRequest WithChild(WorkflowSaveRequest draft, WorkflowVersionPin pin) => draft with
    {
        SchemaVersion = 2,
        Nodes = draft.Nodes.Where(node => node.ClientId is "start" or "done" or "manual")
            .Append(new("child", "Child", "Pinned invoice child", "", "", 1, 0, JsonSerializer.Serialize(new WorkflowChildParameters(1, pin, new(Fact: "invoice.id")), WorkflowDefinitionCodec.JsonOptions))).ToList(),
        Edges = [new("start", "child", 0, ""), new("child", "done", 0, "", "completed"), new("child", "manual", 1, "", "error")],
    };
}
