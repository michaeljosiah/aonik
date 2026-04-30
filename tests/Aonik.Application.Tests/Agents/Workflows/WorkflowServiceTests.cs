using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services.Workflows;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Application.Tests.Agents.Workflows;

public class WorkflowServiceTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private sealed class TestTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid GetCurrentTenantId() => tenantId;
        public bool TryGetCurrentTenantId(out Guid id) { id = tenantId; return true; }
    }

    private sealed class FixedClock(DateTime now) : IClock { public DateTime UtcNow => now; }

    private static (IWorkflowService Service, AgentsDbContext Db) BuildService(
        Guid tenantId,
        DateTime? now = null)
    {
        var services = new ServiceCollection();
        var dbName = $"WorkflowServiceTests_{Guid.NewGuid()}";

        services.AddSingleton<ITenantProvider>(new TestTenantProvider(tenantId));
        services.AddSingleton<IClock>(new FixedClock(now ?? DateTime.UtcNow));
        services.AddDbContext<AgentsDbContext>(options =>
        {
            options.UseInMemoryDatabase(dbName);
        });

        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<AgentsDbContext>();
        var service = new WorkflowService(db, sp.GetRequiredService<ITenantProvider>(), sp.GetRequiredService<IClock>());
        return (service, db);
    }

    [Fact]
    public async Task ListAsync_Should_ReturnEmpty_When_NoWorkflowsSeeded()
    {
        var (service, _) = BuildService(TenantA);

        var result = await service.ListAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAsync_Should_ReturnWorkflowsForCurrentTenant_With_OwnerNameAndStepRail()
    {
        var now = new DateTime(2026, 04, 30, 12, 0, 0, DateTimeKind.Utc);
        var (service, db) = BuildService(TenantA, now);

        var ownerId = Guid.NewGuid();
        db.Agents.Add(new Agent
        {
            Id = ownerId,
            TenantId = TenantA,
            Name = "Billing",
            CreatedAt = now,
        });

        var workflowId = Guid.NewGuid();
        db.Workflows.Add(new Workflow
        {
            Id = workflowId,
            TenantId = TenantA,
            Slug = "match_and_apply",
            Name = "Match & apply",
            Description = "Reconcile invoice → bank txn",
            OwnerAgentId = ownerId,
            OwnerColor = "#eb5c37",
            ContributorsJson = "[]",
            State = WorkflowStates.Active,
            Version = "v1.4",
            AutoRetry = true,
            TriggerCount = 4,
            CreatedAt = now,
        });

        db.WorkflowNodes.AddRange(
            new WorkflowNode { Id = Guid.NewGuid(), TenantId = TenantA, WorkflowId = workflowId, Kind = WorkflowNodeKinds.Trigger, Label = "On bank txn", X = 0, Y = 0, CreatedAt = now },
            new WorkflowNode { Id = Guid.NewGuid(), TenantId = TenantA, WorkflowId = workflowId, Kind = WorkflowNodeKinds.Tool, Label = "search_invoices", X = 200, Y = 0, CreatedAt = now },
            new WorkflowNode { Id = Guid.NewGuid(), TenantId = TenantA, WorkflowId = workflowId, Kind = WorkflowNodeKinds.End, Label = "Done", X = 400, Y = 0, CreatedAt = now });

        db.WorkflowRuns.AddRange(
            new WorkflowRun { Id = Guid.NewGuid(), TenantId = TenantA, WorkflowId = workflowId, Status = WorkflowRunStatuses.Success, DurationMs = 2000, StartedAt = now.AddMinutes(-30), CreatedAt = now.AddMinutes(-30) },
            new WorkflowRun { Id = Guid.NewGuid(), TenantId = TenantA, WorkflowId = workflowId, Status = WorkflowRunStatuses.Success, DurationMs = 3000, StartedAt = now.AddHours(-1), CreatedAt = now.AddHours(-1) },
            new WorkflowRun { Id = Guid.NewGuid(), TenantId = TenantA, WorkflowId = workflowId, Status = WorkflowRunStatuses.Failed, DurationMs = 800, StartedAt = now.AddHours(-2), CreatedAt = now.AddHours(-2) });

        await db.SaveChangesAsync();

        var result = await service.ListAsync();

        result.Should().HaveCount(1);
        var wf = result[0];
        wf.Slug.Should().Be("match_and_apply");
        wf.OwnerName.Should().Be("Billing");
        wf.OwnerColor.Should().Be("#eb5c37");
        wf.RunsToday.Should().Be(3);
        wf.Success.Should().BeApproximately(2.0 / 3.0, 0.01);
        // Avg of 2000 + 3000 + 800 = 5800 / 3 ≈ 1933
        wf.AvgMs.Should().BeInRange(1900, 1950);
        wf.Steps.Should().HaveCount(3);
        wf.Steps[0].Kind.Should().Be(WorkflowNodeKinds.Trigger);
    }

    [Fact]
    public async Task ListAsync_Should_NotReturnOtherTenantWorkflows()
    {
        var (service, db) = BuildService(TenantA);

        db.Workflows.Add(new Workflow
        {
            Id = Guid.NewGuid(),
            TenantId = TenantB, // different tenant
            Slug = "tenantb_only",
            Name = "Other tenant",
            ContributorsJson = "[]",
            State = WorkflowStates.Active,
            Version = "v1.0",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await service.ListAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBySlugAsync_Should_ReturnGraph_With_NodesEdgesComments()
    {
        var (service, db) = BuildService(TenantA);

        var workflowId = Guid.NewGuid();
        var nodeAId = Guid.NewGuid();
        var nodeBId = Guid.NewGuid();

        db.Workflows.Add(new Workflow
        {
            Id = workflowId,
            TenantId = TenantA,
            Slug = "test_flow",
            Name = "Test flow",
            ContributorsJson = "[]",
            State = WorkflowStates.Draft,
            Version = "v0.1",
            CreatedAt = DateTime.UtcNow,
        });
        db.WorkflowNodes.AddRange(
            new WorkflowNode { Id = nodeAId, TenantId = TenantA, WorkflowId = workflowId, Kind = WorkflowNodeKinds.Trigger, Label = "Start", CreatedAt = DateTime.UtcNow },
            new WorkflowNode { Id = nodeBId, TenantId = TenantA, WorkflowId = workflowId, Kind = WorkflowNodeKinds.End, Label = "Done", CreatedAt = DateTime.UtcNow });
        db.WorkflowEdges.Add(new WorkflowEdge { Id = Guid.NewGuid(), TenantId = TenantA, WorkflowId = workflowId, FromNodeId = nodeAId, ToNodeId = nodeBId, CreatedAt = DateTime.UtcNow });
        db.WorkflowComments.Add(new WorkflowComment { Id = Guid.NewGuid(), TenantId = TenantA, WorkflowId = workflowId, Author = "Maria", Body = "Approved", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var graph = await service.GetBySlugAsync("test_flow");

        graph.Should().NotBeNull();
        graph!.Nodes.Should().HaveCount(2);
        graph.Edges.Should().HaveCount(1);
        graph.Comments.Should().HaveCount(1);
        graph.Comments[0].Author.Should().Be("Maria");
    }

    [Fact]
    public async Task GetBySlugAsync_Should_ReturnNull_When_SlugNotFound()
    {
        var (service, _) = BuildService(TenantA);
        var graph = await service.GetBySlugAsync("does_not_exist");
        graph.Should().BeNull();
    }

    [Fact]
    public async Task ListRunsAsync_Should_ReturnNewestFirst_With_FormattedDuration()
    {
        var now = new DateTime(2026, 04, 30, 12, 0, 0, DateTimeKind.Utc);
        var (service, db) = BuildService(TenantA, now);

        var workflowId = Guid.NewGuid();
        db.WorkflowRuns.AddRange(
            new WorkflowRun { Id = Guid.NewGuid(), TenantId = TenantA, WorkflowId = workflowId, Status = WorkflowRunStatuses.Success, DurationMs = 2400, StartedAt = now.AddMinutes(-2), CreatedAt = now.AddMinutes(-2), StartedBy = "auto · banking.transaction.received" },
            new WorkflowRun { Id = Guid.NewGuid(), TenantId = TenantA, WorkflowId = workflowId, Status = WorkflowRunStatuses.Held, DurationMs = 434000, StartedAt = now.AddMinutes(-38), CreatedAt = now.AddMinutes(-38), StartedBy = "held · over ceiling" });
        await db.SaveChangesAsync();

        var runs = await service.ListRunsAsync(workflowId);

        runs.Should().HaveCount(2);
        runs[0].Duration.Should().Be("2.4s");
        runs[0].When.Should().Be("2m ago");
        runs[1].Status.Should().Be(WorkflowRunStatuses.Held);
    }
}
