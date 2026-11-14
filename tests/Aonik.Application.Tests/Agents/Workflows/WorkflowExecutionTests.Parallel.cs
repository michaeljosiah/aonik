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
    public async Task ParallelAll_Should_AdmitIndependentChildren_AndJoinTheirNamespacedResultsOnceAcrossScopes()
    {
        using var host = new Host(); WorkflowVersionPin pin = await host.PublishChild(PureChild);
        Guid parentId = await host.Start(draft => WithParallel(draft, new(1,
            [new("alpha", pin, new(Fact: "invoice.id")), new("beta", pin, new(Fact: "invoice.id"))], 2, "All")));
        var waiting = await host.Continue(parentId); waiting.Status.Should().Be(WorkflowRunStatuses.WaitingForChildren, waiting.ErrorCode);
        Guid[] children;
        using (IServiceScope scope = host.Scope()) children = await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowRuns
            .Where(child => child.ParentRunId == parentId).Select(child => child.Id).ToArrayAsync();
        children.Should().HaveCount(2);
        (await Task.WhenAll(children.Select(child => host.Continue(child)))).Should().OnlyContain(result => result.Status == WorkflowRunStatuses.Completed);

        var joined = await host.Continue(parentId);

        joined.Status.Should().Be(WorkflowRunStatuses.Completed, joined.ErrorCode);
        (await host.Continue(parentId)).Status.Should().Be(WorkflowRunStatuses.Completed);
        using IServiceScope final = host.Scope(); var database = final.ServiceProvider.GetRequiredService<AgentsDbContext>();
        WorkflowStepRun join = await database.WorkflowStepRuns.SingleAsync(step => step.RunId == parentId && step.NodeKey == "fork" && step.InvocationPath == "root");
        JsonElement output = JsonSerializer.Deserialize<JsonElement>(join.ResultJson!).GetProperty("output").GetProperty("branches");
        output.EnumerateObject().Select(property => property.Name).Should().Equal("alpha", "beta");
        output.GetProperty("alpha").GetProperty("status").GetString().Should().Be(WorkflowRunStatuses.Completed);
        (await database.WorkflowRuns.CountAsync(child => child.ParentRunId == parentId)).Should().Be(2);
    }

    [Fact]
    public async Task AnySuccess_Should_CancelTheRemainingReadBranch_AndKeepOneIndependentFinancialApprovalAfterTheJoin()
    {
        using var host = new Host(); WorkflowVersionPin pin = await host.PublishChild(PureChild);
        Guid parentId = await host.Start(draft => WithParallel(draft, new(1,
            [new("alpha", pin, new(Fact: "invoice.id")), new("beta", pin, new(Fact: "invoice.id"))], 2, "AnySuccess"), thenIssue: true));
        (await host.Continue(parentId)).Status.Should().Be(WorkflowRunStatuses.WaitingForChildren);
        Guid winnerId;
        using (IServiceScope scope = host.Scope()) winnerId = (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowRuns
            .SingleAsync(child => child.ParentRunId == parentId && child.InvocationPath.EndsWith("root/beta"))).Id;
        await host.Continue(winnerId);

        var joined = await host.Continue(parentId);

        joined.Status.Should().Be(WorkflowRunStatuses.WaitingForApproval, joined.ErrorCode);
        (await host.Continue(parentId)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);
        using IServiceScope final = host.Scope(); var database = final.ServiceProvider.GetRequiredService<AgentsDbContext>();
        (await database.WorkflowRuns.SingleAsync(child => child.ParentRunId == parentId && child.Id != winnerId)).Status.Should().Be(WorkflowRunStatuses.Cancelled);
        WorkflowStepRun owner = await database.WorkflowStepRuns.SingleAsync(step => step.RunId == parentId && step.NodeKey == "fork" && step.InvocationPath == "root");
        JsonSerializer.Deserialize<JsonElement>(owner.ResultJson!).GetProperty("output").GetProperty("winner").GetString().Should().Be("beta");
        (await database.ToolApprovalRequests.CountAsync(request => request.WorkflowStepRunId != null)).Should().Be(1);
        (await final.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Draft");
    }

    [Theory]
    [InlineData("AnySuccess", 1)]
    [InlineData("All", 2)]
    public async Task Publication_Should_RejectUnsafeWriteJoinsOrConflictingConcurrency(string join, int concurrency)
    {
        using var host = new Host(); WorkflowVersionPin pin = await host.PublishChild();
        using IServiceScope scope = host.Scope(); var workflows = scope.ServiceProvider.GetRequiredService<IWorkflowService>();
        WorkflowGraphResponse draft = await workflows.CreateAsync(WithParallel(host.Draft(), new(1,
            [new("first", pin, new(Fact: "invoice.id")), new("second", pin, new(Fact: "invoice.id"))], concurrency, join)));

        WorkflowValidation validation = (await workflows.ValidateAsync(draft.Slug))!;

        validation.IsValid.Should().BeFalse(); validation.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "WorkflowDependency");
    }

    [Fact]
    public async Task SerializedWriteBranches_Should_StartOneAtATime_AndReportTheSecondAlreadyChangedInvoiceAsFailure()
    {
        using var host = new Host(); WorkflowVersionPin pin = await host.PublishChild();
        Guid parentId = await host.Start(draft => WithParallel(draft, new(1,
            [new("first", pin, new(Fact: "invoice.id")), new("second", pin, new(Fact: "invoice.id"))], 1, "All")));
        await host.Continue(parentId); Guid firstId;
        using (IServiceScope scope = host.Scope()) firstId = (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowRuns.SingleAsync(child => child.ParentRunId == parentId)).Id;
        (await host.Continue(firstId)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);
        using (IServiceScope scope = host.Scope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AgentsDbContext>(); WorkflowRun child = await database.WorkflowRuns.SingleAsync(child => child.Id == firstId);
            var request = await database.ToolApprovalRequests.SingleAsync(request => request.WorkflowStepRunId == child.PendingStepRunId);
            host.Actor.Id = host.Reviewer;
            await scope.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(request.Id, new(ToolApprovalDecisionType.Approve));
        }
        (await host.Continue(firstId)).Status.Should().Be(WorkflowRunStatuses.Completed);
        (await host.Continue(parentId)).Status.Should().Be(WorkflowRunStatuses.WaitingForChildren);
        Guid secondId;
        using (IServiceScope scope = host.Scope()) secondId = (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowRuns.SingleAsync(child => child.ParentRunId == parentId && child.Id != firstId)).Id;
        var second = await host.Continue(secondId); second.Status.Should().Be(WorkflowRunStatuses.Completed); second.ErrorCode.Should().Be("ExecutionStopped");
        var parentResult = await host.Continue(parentId);
        parentResult.Status.Should().Be(WorkflowRunStatuses.Completed); parentResult.Outcome.Should().Be("NeedsManualReview");
        using IServiceScope final = host.Scope();
        (await final.ServiceProvider.GetRequiredService<AgentsDbContext>().ToolApprovalRequests.CountAsync(request => request.WorkflowStepRunId != null)).Should().Be(1);
        (await final.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Issued");
    }

    [Fact]
    public async Task AnySuccessTie_Should_UseStableBranchKeys_WhenChildrenFinishAtTheSameRecordedTime()
    {
        using var host = new Host(); WorkflowVersionPin pin = await host.PublishChild(PureChild);
        Guid parentId = await host.Start(draft => WithParallel(draft, new(1,
            [new("beta", pin, new(Fact: "invoice.id")), new("alpha", pin, new(Fact: "invoice.id"))], 2, "AnySuccess")));
        await host.Continue(parentId); Guid[] children;
        using (IServiceScope scope = host.Scope()) children = await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowRuns.Where(child => child.ParentRunId == parentId).Select(child => child.Id).ToArrayAsync();
        await Task.WhenAll(children.Select(child => host.Continue(child)));
        var result = await host.Continue(parentId); result.Status.Should().Be(WorkflowRunStatuses.Completed, result.ErrorCode);
        using IServiceScope final = host.Scope();
        WorkflowStepRun join = await final.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowStepRuns.SingleAsync(step => step.RunId == parentId && step.NodeKey == "fork" && step.InvocationPath == "root");
        JsonSerializer.Deserialize<JsonElement>(join.ResultJson!).GetProperty("output").GetProperty("winner").GetString().Should().Be("alpha");
    }

    private static WorkflowSaveRequest WithParallel(WorkflowSaveRequest draft, WorkflowParallelParameters parameters, bool thenIssue = false) => draft with
    {
        SchemaVersion = 2,
        Nodes = draft.Nodes.Where(node => node.ClientId is "start" or "done" or "manual" || (thenIssue && node.ClientId == "issue"))
            .Append(new("fork", "Parallel", "Bounded branches", "", "", 1, 0, JsonSerializer.Serialize(parameters, WorkflowDefinitionCodec.JsonOptions))).ToList(),
        Edges = new List<WorkflowSaveEdge> { new("start", "fork", 0, ""), new("fork", thenIssue ? "issue" : "done", 0, "", parameters.CompletedRoute),
            new("fork", "manual", 1, "", parameters.ErrorRoute) }.Concat(thenIssue ? new[] { new WorkflowSaveEdge("issue", "done", 0, "") } : []).ToList(),
    };
}
