using System.Text.Json;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.PersonalFinance;

/// <summary>
/// Spec 030 — parity tests for the FLG approval/rejection handlers. Verify
/// the same observable behaviour the old inline
/// <c>FinancialLifeGraphInferenceService.ApproveProposalAsync</c> /
/// <c>RejectProposalAsync</c> produced, plus the new contract guarantees:
/// idempotent re-apply and Applied = false (instead of throw) when the
/// payload references a deleted entity.
/// </summary>
public class FinancialLifeGraphAnnotationProposalHandlerTests
{
    private sealed class TestTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid GetCurrentTenantId() => tenantId;
        public bool TryGetCurrentTenantId(out Guid id) { id = tenantId; return true; }
    }

    private sealed class TestCurrentUserProvider(Guid userId) : ICurrentUserProvider
    {
        public Guid? GetCurrentUserId() => userId;
        public bool TryGetCurrentUserId(out Guid id) { id = userId; return true; }
    }

    private sealed class NoOpCacheInvalidator : IFinancialLifeGraphCacheInvalidator
    {
        public int InvalidateCurrentUserCallCount;
        public void InvalidateCurrentUserGraph() => Interlocked.Increment(ref InvalidateCurrentUserCallCount);
        public Task InvalidateCurrentUserGraphAsync(CancellationToken _ = default)
        {
            Interlocked.Increment(ref InvalidateCurrentUserCallCount);
            return Task.CompletedTask;
        }
        public Task InvalidateUserGraphAsync(Guid userId, CancellationToken _ = default) => Task.CompletedTask;
        public Task InvalidateUserGraphsAsync(IEnumerable<Guid> userIds, CancellationToken _ = default) => Task.CompletedTask;
        public Task InvalidateAllGraphCachesAsync(CancellationToken _ = default) => Task.CompletedTask;
    }

    private static PersonalFinanceDbContext CreateDbContext(Guid tenantId, string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<PersonalFinanceDbContext>()
            .UseInMemoryDatabase(dbName ?? $"FLGHandlerTestDb_{Guid.NewGuid()}")
            .Options;
        return new PersonalFinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    private static (Guid nodeId, Guid edgeId) SeedProposed(PersonalFinanceDbContext db, Guid tenantId, Guid userId)
    {
        var nodeId = Guid.NewGuid();
        var edgeId = Guid.NewGuid();
        db.FinancialLifeGraphNodes.Add(new FinancialLifeGraphNode
        {
            Id = nodeId,
            TenantId = tenantId,
            UserId = userId,
            NodeType = FinancialLifeGraphNodeTypes.InferredAnnotation,
            DisplayName = "Recurring merchant: Coffee",
            PropertiesJson = "{}",
            Status = FinancialLifeGraphEntityStatus.Proposed,
            IsInferred = true,
            AiRunId = Guid.NewGuid()
        });
        db.FinancialLifeGraphEdges.Add(new FinancialLifeGraphEdge
        {
            Id = edgeId,
            TenantId = tenantId,
            UserId = userId,
            FromNodeKey = $"user:{userId:D}",
            Predicate = FinancialLifeGraphPredicates.AnnotatedAs,
            ToNodeKey = $"native-node:{nodeId:D}",
            PropertiesJson = "{}",
            Status = FinancialLifeGraphEntityStatus.Proposed,
            IsInferred = true,
            AiRunId = Guid.NewGuid()
        });
        db.SaveChanges();
        return (nodeId, edgeId);
    }

    private static AgentProposalDetail BuildDetail(Guid tenantId, Guid nodeId, Guid edgeId, string proposalStatus = "Approved")
    {
        var payload = JsonSerializer.Serialize(new
        {
            GraphNodeId = nodeId,
            GraphEdgeId = edgeId,
            NodeType = FinancialLifeGraphNodeTypes.InferredAnnotation,
            DisplayName = "Recurring merchant: Coffee",
            InferenceType = "RecurringMerchant"
        });
        return new AgentProposalDetail(
            Id: Guid.NewGuid(),
            TenantId: tenantId,
            ProposalType: FinancialLifeGraphAnnotationProposalHandler.ProposalTypeKey,
            Status: proposalStatus,
            PayloadJson: payload,
            ImpactSummary: "Test");
    }

    [Fact]
    public async Task ApprovalHandler_Should_ActivateNodeAndEdge_AndReturnAppliedResource()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var dbName = $"FLG_{Guid.NewGuid()}";

        Guid nodeId;
        Guid edgeId;
        await using (var seedDb = CreateDbContext(tenantId, dbName))
        {
            (nodeId, edgeId) = SeedProposed(seedDb, tenantId, userId);
        }

        await using var db = CreateDbContext(tenantId, dbName);
        var cacheInvalidator = new NoOpCacheInvalidator();
        var handler = new FinancialLifeGraphAnnotationProposalHandler(
            db,
            new TestTenantProvider(tenantId),
            cacheInvalidator);

        var result = await handler.HandleAsync(BuildDetail(tenantId, nodeId, edgeId), CancellationToken.None);

        result.Applied.Should().BeTrue();
        result.AppliedResourceType.Should().Be("FinancialLifeGraphNode");
        result.AppliedResourceId.Should().Be(nodeId);

        await using var verify = CreateDbContext(tenantId, dbName);
        var node = await verify.FinancialLifeGraphNodes.AsNoTracking().SingleAsync(n => n.Id == nodeId);
        var edge = await verify.FinancialLifeGraphEdges.AsNoTracking().SingleAsync(e => e.Id == edgeId);
        node.Status.Should().Be(FinancialLifeGraphEntityStatus.Active);
        edge.Status.Should().Be(FinancialLifeGraphEntityStatus.Active);
        cacheInvalidator.InvalidateCurrentUserCallCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ApprovalHandler_Should_BeIdempotent_When_NodeAlreadyActive()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var dbName = $"FLG_{Guid.NewGuid()}";

        Guid nodeId;
        Guid edgeId;
        await using (var seedDb = CreateDbContext(tenantId, dbName))
        {
            (nodeId, edgeId) = SeedProposed(seedDb, tenantId, userId);
            var existingNode = await seedDb.FinancialLifeGraphNodes.SingleAsync(n => n.Id == nodeId);
            var existingEdge = await seedDb.FinancialLifeGraphEdges.SingleAsync(e => e.Id == edgeId);
            existingNode.Status = FinancialLifeGraphEntityStatus.Active;
            existingEdge.Status = FinancialLifeGraphEntityStatus.Active;
            await seedDb.SaveChangesAsync();
        }

        await using var db = CreateDbContext(tenantId, dbName);
        var handler = new FinancialLifeGraphAnnotationProposalHandler(
            db,
            new TestTenantProvider(tenantId),
            new NoOpCacheInvalidator());

        // Retry path "user clicks Approve again" should converge on the same
        // success result, not raise a duplicate-key or stale-row error.
        var result = await handler.HandleAsync(BuildDetail(tenantId, nodeId, edgeId), CancellationToken.None);

        result.Applied.Should().BeTrue();
        result.Message.Should().Contain("already active");
    }

    [Fact]
    public async Task ApprovalHandler_Should_ReturnAppliedFalse_When_NodeMissing()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);
        var handler = new FinancialLifeGraphAnnotationProposalHandler(
            db,
            new TestTenantProvider(tenantId),
            new NoOpCacheInvalidator());

        // Node never seeded → handler should treat as expected business
        // failure (HTTP 422 via the approval service), not an exception.
        var result = await handler.HandleAsync(BuildDetail(tenantId, Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.Applied.Should().BeFalse();
        result.Message.Should().Contain("no longer exists");
    }

    [Fact]
    public async Task ApprovalHandler_Should_ReturnAppliedFalse_When_PayloadInvalid()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);
        var handler = new FinancialLifeGraphAnnotationProposalHandler(
            db,
            new TestTenantProvider(tenantId),
            new NoOpCacheInvalidator());

        var detail = new AgentProposalDetail(
            Id: Guid.NewGuid(),
            TenantId: tenantId,
            ProposalType: FinancialLifeGraphAnnotationProposalHandler.ProposalTypeKey,
            Status: "Approved",
            PayloadJson: "{ }",
            ImpactSummary: "missing GraphNodeId");

        var result = await handler.HandleAsync(detail, CancellationToken.None);

        result.Applied.Should().BeFalse();
        result.Message.Should().Contain("GraphNodeId");
    }

    [Fact]
    public async Task RejectionHandler_Should_FlipNodeAndEdgeToRejected()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var dbName = $"FLG_{Guid.NewGuid()}";

        Guid nodeId;
        Guid edgeId;
        await using (var seedDb = CreateDbContext(tenantId, dbName))
        {
            (nodeId, edgeId) = SeedProposed(seedDb, tenantId, userId);
        }

        await using var db = CreateDbContext(tenantId, dbName);
        var handler = new FinancialLifeGraphAnnotationProposalRejectionHandler(
            db,
            new TestTenantProvider(tenantId),
            new NoOpCacheInvalidator());

        await handler.HandleRejectionAsync(BuildDetail(tenantId, nodeId, edgeId, proposalStatus: "Rejected"), CancellationToken.None);

        await using var verify = CreateDbContext(tenantId, dbName);
        var node = await verify.FinancialLifeGraphNodes.AsNoTracking().SingleAsync(n => n.Id == nodeId);
        var edge = await verify.FinancialLifeGraphEdges.AsNoTracking().SingleAsync(e => e.Id == edgeId);
        node.Status.Should().Be(FinancialLifeGraphEntityStatus.Rejected);
        edge.Status.Should().Be(FinancialLifeGraphEntityStatus.Rejected);
    }

    [Fact]
    public async Task RejectionHandler_Should_Succeed_When_NodeAlreadyDeleted()
    {
        // Best-effort cleanup: if the FLG node or edge has already been
        // removed by a sibling flow, the rejection handler should not throw
        // (the dismiss endpoint would otherwise return HTTP 500 even though
        // the user's intent was achieved).
        var tenantId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);
        var handler = new FinancialLifeGraphAnnotationProposalRejectionHandler(
            db,
            new TestTenantProvider(tenantId),
            new NoOpCacheInvalidator());

        var act = () => handler.HandleRejectionAsync(BuildDetail(tenantId, Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
