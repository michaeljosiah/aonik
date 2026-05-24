using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Insights;

/// <summary>
/// Coverage for the Wave 4c.2 approval pipeline:
/// GET / Approve / Dismiss flows on Proposal, including stamping
/// ApprovedByUserId + ApprovedAt and rejecting transitions on already
/// resolved proposals.
/// </summary>
public class ProposalApprovalServiceTests
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

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    // Default dispatcher returns Applied = true with no resource metadata so
    // these tests stay focused on the agent-side flip-status logic. Tests that
    // need to exercise revert paths (handler throws, handler returns
    // Applied = false) live in the dispatcher-focused test class.
    private sealed class StubProposalDispatcher : IProposalDispatcher
    {
        public Task<ProposalHandlerResult> DispatchAsync(AgentProposalDetail proposal, CancellationToken ct)
            => Task.FromResult(new ProposalHandlerResult(Applied: true));
    }

    private sealed class StubProposalRejectionDispatcher : IProposalRejectionDispatcher
    {
        public Task DispatchAsync(AgentProposalDetail proposal, CancellationToken ct) => Task.CompletedTask;
    }

    private static AgentsDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AgentsDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;
        return new AgentsDbContext(options, new TestTenantProvider(tenantId));
    }

    private static ProposalApprovalService CreateService(
        AgentsDbContext db,
        Guid userId,
        DateTime now) =>
        new(db,
            new TestCurrentUserProvider(userId),
            new FixedClock(now),
            new StubProposalDispatcher(),
            new StubProposalRejectionDispatcher());

    private static (Agent agent, Proposal proposal) Seed(AgentsDbContext db, Guid tenantId, ProposalStatus status = ProposalStatus.Proposed)
    {
        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Billing",
            Domain = "Billing",
            Description = "Billing agent",
            InstructionsText = string.Empty,
            ToolsetIdsJson = "[]",
            InputSchemaJson = "{}",
            OutputSchemaJson = "{}",
            PermissionsProfileJson = "{}",
            RiskTier = "Low",
            IsActive = true,
            IconUrl = "/icons/billing.svg",
        };
        var proposal = new Proposal
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProposalType = "InvoiceMatch",
            ProposedByAgentId = agent.Id,
            AiRunId = Guid.NewGuid(),
            ImpactSummary = "Match INV-2041 to bank txn",
            RiskTier = "Low",
            Confidence = 0.94m,
            Status = status,
            PayloadJson = "{\"invoice\":\"INV-2041\"}",
            CreatedAt = DateTime.UtcNow,
        };
        db.Agents.Add(agent);
        db.Proposals.Add(proposal);
        db.SaveChanges();
        return (agent, proposal);
    }

    [Fact]
    public async Task GetByIdAsync_Should_ReturnDetailWithAgentMetadata_When_ProposalExists()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);
        var (_, proposal) = Seed(db, tenantId);

        var service = CreateService(db, Guid.NewGuid(), DateTime.UtcNow);

        var detail = await service.GetByIdAsync(proposal.Id);

        detail.Should().NotBeNull();
        detail!.Id.Should().Be(proposal.Id);
        detail.AgentName.Should().Be("Billing");
        detail.AgentIconUrl.Should().Be("/icons/billing.svg");
        detail.Confidence.Should().Be(0.94m);
        detail.Status.Should().Be("Proposed");
        detail.PayloadJson.Should().Contain("INV-2041");
    }

    [Fact]
    public async Task GetByIdAsync_Should_ReturnNull_When_ProposalMissing()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);
        var service = CreateService(db, Guid.NewGuid(), DateTime.UtcNow);

        var detail = await service.GetByIdAsync(Guid.NewGuid());

        detail.Should().BeNull();
    }

    [Fact]
    public async Task ApproveAsync_Should_TransitionToApproved_AndStampUserAndTimestamp()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = new DateTime(2026, 4, 27, 10, 0, 0, DateTimeKind.Utc);
        await using var db = CreateDbContext(tenantId);
        var (_, proposal) = Seed(db, tenantId);

        var service = CreateService(db, userId, now);

        var detail = await service.ApproveAsync(proposal.Id);

        detail.Status.Should().Be("Approved");
        detail.ApprovedByUserId.Should().Be(userId);
        detail.ApprovedAt.Should().Be(now);

        var persisted = await db.Proposals.AsNoTracking().FirstAsync(p => p.Id == proposal.Id);
        persisted.Status.Should().Be(ProposalStatus.Approved);
    }

    [Fact]
    public async Task DismissAsync_Should_TransitionToRejected_WithSameStamping()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using var db = CreateDbContext(tenantId);
        var (_, proposal) = Seed(db, tenantId);

        var service = CreateService(db, userId, now);

        var detail = await service.DismissAsync(proposal.Id);

        detail.Status.Should().Be("Rejected");
        detail.ApprovedByUserId.Should().Be(userId);
    }

    [Fact]
    public async Task ApproveAsync_Should_Throw_When_ProposalAlreadyResolved()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);
        var (_, proposal) = Seed(db, tenantId, ProposalStatus.Approved);

        var service = CreateService(db, Guid.NewGuid(), DateTime.UtcNow);

        await FluentActions.Invoking(() => service.ApproveAsync(proposal.Id))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ApproveAsync_Should_ThrowKeyNotFound_When_ProposalMissing()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);

        var service = CreateService(db, Guid.NewGuid(), DateTime.UtcNow);

        await FluentActions.Invoking(() => service.ApproveAsync(Guid.NewGuid()))
            .Should().ThrowAsync<KeyNotFoundException>();
    }
}
