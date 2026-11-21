using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services.Workflows;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Events.Outbox;

namespace Aonik.Database.Tests;

public partial class WorkflowRunSqlServerTests
{
    [SkippableFact]
    public async Task HumanResponse_Should_CommitItsExactResponseWithOneWake_AndRollBackTheLosingDecision()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason);
        Guid tenantId = Guid.NewGuid(); Guid requestId;
        await using (AgentsDbContext seed = Context(tenantId))
        {
            var run = new WorkflowRun { TenantId = tenantId, Mode = "Live", WorkflowId = Guid.NewGuid(), StartedAt = DateTime.UtcNow,
                Status = WorkflowRunStatuses.WaitingForApproval, DeadlineUtc = DateTime.UtcNow.AddDays(1) };
            var step = new WorkflowStepRun { TenantId = tenantId, RunId = run.Id, NodeKey = "human", OperationId = Guid.NewGuid(),
                DeadlineUtc = DateTime.UtcNow.AddHours(1), Status = "WaitingForApproval" };
            var request = new ToolApprovalRequest { TenantId = tenantId, WorkflowStepRunId = step.Id, ToolName = WorkflowHumanTaskContract.ToolName,
                ActionKind = "Human task", ArgsHash = new('a', 64), ArgumentsRedactedJson = "{}", RiskTier = "Medium", RequestedAt = DateTime.UtcNow,
                ExpiresAt = step.DeadlineUtc.Value };
            step.ApprovalRequestId = request.Id; run.PendingStepRunId = step.Id;
            seed.WorkflowRuns.Add(run); seed.WorkflowStepRuns.Add(step); seed.ToolApprovalRequests.Add(request);
            await seed.SaveChangesAsync(); requestId = request.Id;
        }
        await using AgentsDbContext approved = Context(tenantId); await using AgentsDbContext rejected = Context(tenantId);
        ToolApprovalRequest winner = await approved.ToolApprovalRequests.SingleAsync(request => request.Id == requestId);
        ToolApprovalRequest loser = await rejected.ToolApprovalRequests.SingleAsync(request => request.Id == requestId);
        winner.Status = ToolApprovalRequestStatus.Approved; winner.DecidedAt = DateTime.UtcNow; winner.DecidedByUserId = Guid.NewGuid();
        winner.ResponseJson = "{\"choice\":\"continue\"}";
        winner.ResponseHash = ToolArgumentsHash.Compute(JsonSerializer.Deserialize<Dictionary<string, object?>>(winner.ResponseJson)!);
        loser.Status = ToolApprovalRequestStatus.Rejected; loser.DecidedAt = DateTime.UtcNow; loser.DecidedByUserId = Guid.NewGuid();
        await new WorkflowContinuationWriter(approved, new Clock()).AfterDecisionAsync(winner);
        await new WorkflowContinuationWriter(rejected, new Clock()).AfterDecisionAsync(loser);
        await approved.SaveChangesAsync();

        Func<Task> staleDecision = async () => await rejected.SaveChangesAsync();

        await staleDecision.Should().ThrowAsync<DbUpdateConcurrencyException>();
        await using AgentsDbContext verify = Context(tenantId);
        ToolApprovalRequest saved = await verify.ToolApprovalRequests.SingleAsync(request => request.Id == requestId);
        saved.ResponseJson.Should().Be(winner.ResponseJson); saved.ResponseHash.Should().Be(winner.ResponseHash);
        saved.Status.Should().Be(ToolApprovalRequestStatus.Approved);
        (await verify.Set<OutboxMessage>().CountAsync(message => message.TenantId == tenantId)).Should().Be(1);
        (await verify.WorkflowStepRuns.SingleAsync()).DeadlineUtc.Should().BeCloseTo(saved.ExpiresAt, TimeSpan.FromMilliseconds(1));
    }
}
