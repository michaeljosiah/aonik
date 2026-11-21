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
using Aonik.SharedKernel.Abstractions.Tasks;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;

namespace Aonik.Application.Tests.Agents.Workflows;

public partial class WorkflowExecutionTests
{
    [Fact]
    public async Task HumanTask_Should_RecordAnExactTypedResponseAcrossScopes_AndStillRequireItsOwnFinancialApproval()
    {
        using var host = new Host();
        Guid runId = await host.Start(draft => WithHuman(draft, host.Reviewer));
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);
        Guid requestId;
        using (IServiceScope scope = host.Scope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
            ToolApprovalRequest request = await database.ToolApprovalRequests.SingleAsync(request => request.WorkflowStepRunId != null);
            requestId = request.Id;
            request.ExpiresAt.Should().Be(host.Clock.UtcNow.AddHours(1));
            WorkflowContinuationRequestedEvent expiry = (await database.Set<OutboxMessage>().Where(value => value.EventType == typeof(WorkflowContinuationRequestedEvent).FullName).ToListAsync())
                .Select(value => JsonSerializer.Deserialize<WorkflowContinuationRequestedEvent>(value.Payload, OutboxSerialization.Options)!).Single(value => value.EventId == request.Id);
            expiry.AssignedUserId.Should().Be(host.Reviewer);
            await new WorkflowContinuationRequestedHandler(scope.ServiceProvider.GetRequiredService<ITaskService>(), scope.ServiceProvider.GetRequiredService<Aonik.SharedKernel.Abstractions.Multitenancy.ITenantProvider>()).HandleAsync(expiry);
            host.ScheduledTasks.Single().AssigneeId.Should().Be(host.Reviewer);
            host.Actor.Id = host.Reviewer;
            var approvals = scope.ServiceProvider.GetRequiredService<IToolApprovalService>();
            (await approvals.DecideAsync(requestId, new(ToolApprovalDecisionType.Approve))).Outcome.Should().Be(ToolApprovalDecisionOutcome.Forbidden);
            (await approvals.DecideAsync(requestId, new(ToolApprovalDecisionType.Approve, Response: JsonSerializer.SerializeToElement(new { choice = "continue", invoiceId = Guid.NewGuid() })))).Outcome.Should().Be(ToolApprovalDecisionOutcome.Forbidden);
            (await approvals.DecideAsync(requestId, new(ToolApprovalDecisionType.Approve, Response: JsonSerializer.SerializeToElement(new { choice = "continue" })))).Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
            (await approvals.DecideAsync(requestId, new(ToolApprovalDecisionType.Approve, Response: JsonSerializer.SerializeToElement(new { choice = "continue" })))).Outcome.Should().Be(ToolApprovalDecisionOutcome.AlreadyDecided);
        }
        host.Clock.UtcNow = host.Clock.UtcNow.AddHours(2);
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);
        using IServiceScope final = host.Scope(); var saved = final.ServiceProvider.GetRequiredService<AgentsDbContext>();
        ToolApprovalRequest human = await saved.ToolApprovalRequests.SingleAsync(request => request.Id == requestId);
        human.ResponseHash.Should().HaveLength(64); human.DecidedByUserId.Should().Be(host.Reviewer); human.ConsumedAt.Should().NotBeNull();
        (await saved.WorkflowStepRuns.SingleAsync(step => step.NodeKey == "human")).SelectedRoute.Should().Be("responded");
        ToolApprovalRequest financial = await saved.ToolApprovalRequests.SingleAsync(request => request.WorkflowStepRunId != null && request.Id != requestId);
        financial.Status.Should().Be(ToolApprovalRequestStatus.Pending);
        (await final.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Draft");
        (await final.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(financial.Id,
            new(ToolApprovalDecisionType.Approve, Response: JsonSerializer.SerializeToElement(new { choice = "continue" })))).Outcome.Should().Be(ToolApprovalDecisionOutcome.Forbidden);
    }

    [Theory]
    [InlineData("reject", "rejected")]
    [InlineData("timeout", "timeout")]
    public async Task HumanTask_Should_TakeItsPublishedRejectionOrTimeoutRoute_WithoutPreparingAnInvoiceWrite(string decision, string route)
    {
        using var host = new Host(); Guid runId = await host.Start(draft => WithHuman(draft, host.Reviewer)); await host.Continue(runId);
        using (IServiceScope scope = host.Scope())
        {
            ToolApprovalRequest request = await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().ToolApprovalRequests.SingleAsync(request => request.WorkflowStepRunId != null);
            host.Actor.Id = host.Reviewer;
            if (decision == "reject") (await scope.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(request.Id, new(ToolApprovalDecisionType.Reject))).Outcome.Should().Be(ToolApprovalDecisionOutcome.Rejected);
            else host.Clock.UtcNow = request.ExpiresAt;
        }
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.Completed);
        using IServiceScope final = host.Scope(); var saved = final.ServiceProvider.GetRequiredService<AgentsDbContext>();
        (await saved.WorkflowStepRuns.SingleAsync(step => step.NodeKey == "human")).SelectedRoute.Should().Be(route);
        (await saved.WorkflowStepRuns.AnyAsync(step => step.NodeKey == "issue")).Should().BeFalse();
        (await final.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Draft");
    }

    [Theory]
    [InlineData("wrong-user")]
    [InlineData("inactive")]
    [InlineData("permission-revoked")]
    [InlineData("expired")]
    [InlineData("cancelled")]
    public async Task HumanTask_Should_RejectStaleOrUnauthorizedResponses(string change)
    {
        using var host = new Host(); Guid runId = await host.Start(draft => WithHuman(draft, host.Reviewer)); await host.Continue(runId);
        using IServiceScope scope = host.Scope(); var services = scope.ServiceProvider;
        ToolApprovalRequest request = await services.GetRequiredService<AgentsDbContext>().ToolApprovalRequests.SingleAsync(request => request.WorkflowStepRunId != null);
        if (change == "cancelled")
        {
            var runs = services.GetRequiredService<IWorkflowRunService>(); var run = (await runs.GetAsync(runId))!;
            await runs.CancelAsync(runId, new(run.ExpectedVersion));
        }
        host.Actor.Id = change == "wrong-user" ? Guid.NewGuid() : host.Reviewer;
        if (change == "inactive") host.Inactive.Add(host.Reviewer);
        if (change == "permission-revoked") host.Denied.Add((host.Reviewer, "Workflow.Run"));
        if (change == "expired") host.Clock.UtcNow = request.ExpiresAt;
        ToolApprovalDecisionResult result = await services.GetRequiredService<IToolApprovalService>().DecideAsync(request.Id,
            new(ToolApprovalDecisionType.Approve, Response: JsonSerializer.SerializeToElement(new { choice = "continue" })));
        result.Outcome.Should().BeOneOf(ToolApprovalDecisionOutcome.Forbidden, ToolApprovalDecisionOutcome.Expired, ToolApprovalDecisionOutcome.AlreadyDecided);
        request.ResponseJson.Should().BeNull();
    }

    private static WorkflowSaveRequest WithHuman(WorkflowSaveRequest draft, Guid reviewer) => draft with
    {
        SchemaVersion = 2,
        Nodes = draft.Nodes.Append(new("human", "Human", "Human review", "", "", 0, 1, JsonSerializer.Serialize(
            new WorkflowHumanParameters(1, "Review invoice evidence", new(UserId: reviewer), 3600, [new("continue", "Continue review", "responded")], "rejected", "timeout"), WorkflowDefinitionCodec.JsonOptions))).ToList(),
        Edges = draft.Edges.Select(edge => edge.FromClientId == "start" ? edge with { ToClientId = "human" } : edge)
            .Concat(new[] { new WorkflowSaveEdge("human", "route", 0, "", "responded"), new("human", "manual", 1, "", "rejected"), new("human", "manual", 2, "", "timeout") }).ToList(),
    };
}
