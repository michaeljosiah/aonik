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

namespace Aonik.Application.Tests.Agents.Workflows;

public partial class WorkflowExecutionTests
{
    [Fact]
    public async Task ForEach_Should_KeepStableIterationIdentitiesAcrossNativeMafResumes_AndRouteExhaustionWithoutAnotherChild()
    {
        using var host = new Host(); WorkflowVersionPin pin = await host.PublishChild(PureChild);
        Guid parentId = await host.Start(draft => WithLoop(draft, new(1, pin,
            [new("first", host.InvoiceId), new("second", host.InvoiceId), new("third", host.InvoiceId)], 2, 3600)));
        for (int index = 0; index < 2; index++)
        {
            var pending = await host.Continue(parentId); pending.Status.Should().Be(WorkflowRunStatuses.WaitingForChildren, pending.ErrorCode);
            (await host.Continue(parentId)).Status.Should().Be(WorkflowRunStatuses.WaitingForChildren);
            Guid childId;
            using (IServiceScope scope = host.Scope()) childId = (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowRuns
                .SingleAsync(child => child.ParentRunId == parentId && child.Status == WorkflowRunStatuses.Queued)).Id;
            (await host.Continue(childId)).Status.Should().Be(WorkflowRunStatuses.Completed);
        }
        var result = await host.Continue(parentId);
        result.Status.Should().Be(WorkflowRunStatuses.Completed, result.ErrorCode); result.Outcome.Should().Be("NeedsManualReview");
        using IServiceScope final = host.Scope(); AgentsDbContext database = final.ServiceProvider.GetRequiredService<AgentsDbContext>();
        (await database.WorkflowRuns.CountAsync(child => child.ParentRunId == parentId)).Should().Be(2);
        WorkflowStepRun loop = await database.WorkflowStepRuns.SingleAsync(step => step.RunId == parentId && step.NodeKey == "each" && step.InvocationPath == "root");
        loop.SelectedRoute.Should().Be("exhausted");
        (await database.WorkflowStepRuns.Where(step => step.RunId == parentId && step.NodeKey == "each" && step.InvocationPath != "root").Select(step => step.InvocationPath).OrderBy(path => path).ToListAsync())
            .Should().Equal("root/first", "root/second");
    }

    [Fact]
    public async Task ForEachDeadline_Should_CancelAPendingChildAndItsTimer_AndNeverAdmitTheNextItem()
    {
        using var host = new Host(); WorkflowVersionPin pin = await host.PublishChild(draft => WithWait(PureChild(draft), new(1, DelaySeconds: 120)) with
        { Edges = [new("start", "wait", 0, ""), new("wait", "done", 0, "")] });
        Guid parentId = await host.Start(draft => WithLoop(draft, new(1, pin, [new("first", host.InvoiceId), new("second", host.InvoiceId)], 2, 30)));
        await host.Continue(parentId); Guid childId;
        using (IServiceScope scope = host.Scope()) childId = (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowRuns.SingleAsync(child => child.ParentRunId == parentId)).Id;
        (await host.Continue(childId)).Status.Should().Be(WorkflowRunStatuses.WaitingForTimer);
        host.Clock.UtcNow = host.Clock.UtcNow.AddSeconds(30);

        var result = await host.Continue(parentId);

        result.Status.Should().Be(WorkflowRunStatuses.Completed, result.ErrorCode);
        using IServiceScope final = host.Scope(); var database = final.ServiceProvider.GetRequiredService<AgentsDbContext>();
        (await database.WorkflowRuns.CountAsync(child => child.ParentRunId == parentId)).Should().Be(1);
        (await database.WorkflowRuns.SingleAsync(child => child.Id == childId)).Status.Should().Be(WorkflowRunStatuses.Cancelled);
        (await database.WorkflowStepRuns.SingleAsync(step => step.RunId == parentId && step.NodeKey == "each" && step.InvocationPath == "root")).SelectedRoute.Should().Be("exhausted");
    }

    [Fact]
    public async Task ForEachFinancialItems_Should_KeepSeparateApprovalsAndOperations_AndReconcileAnEffectBeforeRetryingItsChild()
    {
        using var host = new Host(); WorkflowVersionPin pin = await host.PublishChild(); Guid second = await host.AddInvoice();
        Guid parentId = await host.Start(draft => WithLoop(draft, new(1, pin, [new("first", host.InvoiceId), new("second", second)], 2, 3600)));
        for (int index = 0; index < 2; index++)
        {
            (await host.Continue(parentId)).Status.Should().Be(WorkflowRunStatuses.WaitingForChildren);
            Guid childId;
            using (IServiceScope scope = host.Scope()) childId = (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowRuns
                .SingleAsync(child => child.ParentRunId == parentId && child.Status == WorkflowRunStatuses.Queued)).Id;
            (await host.Continue(childId)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);
            using (IServiceScope scope = host.Scope())
            {
                var database = scope.ServiceProvider.GetRequiredService<AgentsDbContext>(); WorkflowRun child = await database.WorkflowRuns.SingleAsync(child => child.Id == childId);
                var request = await database.ToolApprovalRequests.SingleAsync(request => request.WorkflowStepRunId == child.PendingStepRunId);
                host.Actor.Id = host.Reviewer;
                (await scope.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(request.Id, new(ToolApprovalDecisionType.Approve))).Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
            }
            if (index == 0)
            {
                host.CrashAfterIssue = true;
                (await host.Continue(childId)).Status.Should().Be(WorkflowRunStatuses.NeedsAttention);
                using IServiceScope scope = host.Scope(); var runs = scope.ServiceProvider.GetRequiredService<IWorkflowRunService>(); var child = (await runs.GetAsync(childId))!;
                await runs.ResumeAsync(childId, new(child.ExpectedVersion));
            }
            (await host.Continue(childId, index == 0 ? 1 : 0)).Status.Should().Be(WorkflowRunStatuses.Completed);
        }
        var result = await host.Continue(parentId);
        result.Status.Should().Be(WorkflowRunStatuses.Completed, result.ErrorCode);
        using IServiceScope final = host.Scope(); var finance = final.ServiceProvider.GetRequiredService<FinanceDbContext>();
        (await finance.Invoices.Select(invoice => invoice.Status).ToListAsync()).Should().OnlyContain(status => status == "Issued");
        (await finance.Invoices.Select(invoice => invoice.IssueOperationId).Distinct().CountAsync()).Should().Be(2);
        (await final.ServiceProvider.GetRequiredService<AgentsDbContext>().ToolApprovalRequests.CountAsync(request => request.WorkflowStepRunId != null)).Should().Be(2);
    }

    [Fact]
    public async Task ThreeIterationLoop_Should_ExposeOnePublicResult_AndReachOnlyOneFollowingFinancialGate()
    {
        using var host = new Host(); WorkflowVersionPin pin = await host.PublishChild(PureChild);
        Guid parentId = await host.Start(draft =>
        {
            WorkflowSaveRequest loop = WithLoop(draft, new(1, pin, [new("first", host.InvoiceId), new("second", host.InvoiceId), new("third", host.InvoiceId)], 3, 3600));
            return loop with { Nodes = loop.Nodes.Append(draft.Nodes.Single(node => node.ClientId == "issue")).ToList(),
                Edges = loop.Edges.Select(edge => edge.FromClientId == "each" && edge.RouteKey == "completed" ? edge with { ToClientId = "issue" } : edge).Append(new("issue", "done", 0, "")).ToList() };
        });
        for (int index = 0; index < 3; index++)
        {
            (await host.Continue(parentId)).Status.Should().Be(WorkflowRunStatuses.WaitingForChildren);
            Guid childId;
            using (IServiceScope scope = host.Scope()) childId = (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowRuns.SingleAsync(child => child.ParentRunId == parentId && child.Status == WorkflowRunStatuses.Queued)).Id;
            await host.Continue(childId);
        }
        var result = await host.Continue(parentId); result.Status.Should().Be(WorkflowRunStatuses.WaitingForApproval, result.ErrorCode);
        using IServiceScope final = host.Scope();
        (await final.ServiceProvider.GetRequiredService<AgentsDbContext>().ToolApprovalRequests.CountAsync(request => request.WorkflowStepRunId != null)).Should().Be(1);
    }

    private static WorkflowSaveRequest PureChild(WorkflowSaveRequest draft) => draft with
    { Nodes = draft.Nodes.Where(node => node.ClientId is "start" or "done").ToList(), Edges = [new("start", "done", 0, "")] };

    private static WorkflowSaveRequest WithLoop(WorkflowSaveRequest draft, WorkflowLoopParameters parameters) => draft with
    {
        SchemaVersion = 2,
        Nodes = draft.Nodes.Where(node => node.ClientId is "start" or "done" or "manual")
            .Append(new("each", "Loop", "Bounded foreach", "", "", 1, 0, JsonSerializer.Serialize(parameters, WorkflowDefinitionCodec.JsonOptions))).ToList(),
        Edges = [new("start", "each", 0, ""), new("each", "done", 0, "", parameters.CompletedRoute), new("each", "manual", 1, "", parameters.ExhaustedRoute), new("each", "manual", 2, "", parameters.ErrorRoute)],
    };
}
