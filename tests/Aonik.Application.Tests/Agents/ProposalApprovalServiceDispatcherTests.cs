using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Agents;

/// <summary>
/// Spec 030 — covers the revert-on-failure and applied-resource-response
/// behaviour of <see cref="ProposalApprovalService"/> as it composes with the
/// new dispatcher. <see cref="Insights.ProposalApprovalServiceTests"/> stays
/// focused on the status-flip / stamping logic.
/// </summary>
public class ProposalApprovalServiceDispatcherTests
{
    private const string ProposalTypeKey = "TestProposalType";

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

    private sealed class FixedClock(DateTime utcNow) : IClock { public DateTime UtcNow { get; } = utcNow; }

    private sealed class DelegateDispatcher : IProposalDispatcher
    {
        public Func<AgentProposalDetail, Task<ProposalHandlerResult>> Impl { get; init; } =
            _ => Task.FromResult(new ProposalHandlerResult(Applied: true));

        public Task<ProposalHandlerResult> DispatchAsync(AgentProposalDetail proposal, CancellationToken ct)
            => Impl(proposal);
    }

    private sealed class DelegateRejectionDispatcher : IProposalRejectionDispatcher
    {
        public Func<AgentProposalDetail, Task> Impl { get; init; } = _ => Task.CompletedTask;

        public Task DispatchAsync(AgentProposalDetail proposal, CancellationToken ct) => Impl(proposal);
    }

    private static AgentsDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AgentsDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;
        return new AgentsDbContext(options, new TestTenantProvider(tenantId));
    }

    private static Proposal SeedProposed(AgentsDbContext db, Guid tenantId, string riskTier = "Low")
    {
        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "TestAgent",
            Domain = "Test",
            Description = "test",
            InstructionsText = string.Empty,
            ToolsetIdsJson = "[]",
            InputSchemaJson = "{}",
            OutputSchemaJson = "{}",
            PermissionsProfileJson = "{}",
            RiskTier = "Low",
            IsActive = true,
        };
        var proposal = new Proposal
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProposalType = ProposalTypeKey,
            ProposedByAgentId = agent.Id,
            AiRunId = Guid.NewGuid(),
            ImpactSummary = "test",
            // The proposal's RiskTier — not the agent's — drives the Spec 032 §8.1
            // Applied/Failed terminal model vs the Spec 030 revert model.
            RiskTier = riskTier,
            Status = ProposalStatus.Proposed,
            PayloadJson = "{}",
            CreatedAt = DateTime.UtcNow,
        };
        db.Agents.Add(agent);
        db.Proposals.Add(proposal);
        db.SaveChanges();
        return proposal;
    }

    [Fact]
    public async Task ApproveAsync_Should_ReturnAppliedResourceMetadata_When_HandlerReportsSuccess()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);
        var proposal = SeedProposed(db, tenantId);

        var dispatcher = new DelegateDispatcher
        {
            Impl = _ => Task.FromResult(new ProposalHandlerResult(
                Applied: true,
                AppliedResourceType: "Widget",
                AppliedResourceId: resourceId,
                Message: "ok"))
        };

        var service = new ProposalApprovalService(
            db,
            new TestCurrentUserProvider(userId),
            new FixedClock(DateTime.UtcNow),
            dispatcher,
            new DelegateRejectionDispatcher());

        var detail = await service.ApproveAsync(proposal.Id);

        detail.Status.Should().Be("Approved");
        detail.AppliedResourceType.Should().Be("Widget");
        detail.AppliedResourceId.Should().Be(resourceId);
        detail.AppliedMessage.Should().Be("ok");
    }

    [Fact]
    public async Task ApproveAsync_Should_RevertToProposed_When_HandlerThrows()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);
        var proposal = SeedProposed(db, tenantId);

        var dispatcher = new DelegateDispatcher
        {
            Impl = _ => throw new InvalidOperationException("partner outage")
        };

        var service = new ProposalApprovalService(
            db,
            new TestCurrentUserProvider(userId),
            new FixedClock(DateTime.UtcNow),
            dispatcher,
            new DelegateRejectionDispatcher());

        await FluentActions.Invoking(() => service.ApproveAsync(proposal.Id))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("partner outage");

        var persisted = await db.Proposals.AsNoTracking().FirstAsync(p => p.Id == proposal.Id);
        persisted.Status.Should().Be(ProposalStatus.Proposed);
        persisted.ApprovedByUserId.Should().BeNull();
        persisted.ApprovedAt.Should().BeNull();
    }

    [Fact]
    public async Task ApproveAsync_Should_RevertAndThrowExecutionFailed_When_HandlerReportsAppliedFalse()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);
        var proposal = SeedProposed(db, tenantId);

        var dispatcher = new DelegateDispatcher
        {
            Impl = _ => Task.FromResult(new ProposalHandlerResult(
                Applied: false,
                Message: "payload references a deleted entity"))
        };

        var service = new ProposalApprovalService(
            db,
            new TestCurrentUserProvider(userId),
            new FixedClock(DateTime.UtcNow),
            dispatcher,
            new DelegateRejectionDispatcher());

        var thrown = await FluentActions.Invoking(() => service.ApproveAsync(proposal.Id))
            .Should().ThrowAsync<ProposalExecutionFailedException>();
        thrown.Which.ProposalId.Should().Be(proposal.Id);
        thrown.Which.Message.Should().Contain("payload references a deleted entity");

        var persisted = await db.Proposals.AsNoTracking().FirstAsync(p => p.Id == proposal.Id);
        persisted.Status.Should().Be(ProposalStatus.Proposed);
        persisted.ApprovedByUserId.Should().BeNull();
        persisted.ApprovedAt.Should().BeNull();
    }

    [Fact]
    public async Task ApproveAsync_Should_RevertAndPropagate_When_DispatcherThrowsNoHandlerRegistered()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);
        var proposal = SeedProposed(db, tenantId);

        var dispatcher = new DelegateDispatcher
        {
            Impl = _ => throw new NoProposalHandlerRegisteredException(ProposalTypeKey)
        };

        var service = new ProposalApprovalService(
            db,
            new TestCurrentUserProvider(userId),
            new FixedClock(DateTime.UtcNow),
            dispatcher,
            new DelegateRejectionDispatcher());

        await FluentActions.Invoking(() => service.ApproveAsync(proposal.Id))
            .Should().ThrowAsync<NoProposalHandlerRegisteredException>();

        var persisted = await db.Proposals.AsNoTracking().FirstAsync(p => p.Id == proposal.Id);
        persisted.Status.Should().Be(ProposalStatus.Proposed);
    }

    [Fact]
    public async Task DismissAsync_Should_KeepStatusRejected_When_RejectionHandlerThrows()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);
        var proposal = SeedProposed(db, tenantId);

        var rejection = new DelegateRejectionDispatcher
        {
            Impl = _ => throw new InvalidOperationException("cleanup broken")
        };

        var service = new ProposalApprovalService(
            db,
            new TestCurrentUserProvider(userId),
            new FixedClock(DateTime.UtcNow),
            new DelegateDispatcher(),
            rejection);

        await FluentActions.Invoking(() => service.DismissAsync(proposal.Id))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("cleanup broken");

        // V1 contract: dismissal stays in place even when cleanup fails; the
        // user's explicit intent is preserved so we don't un-dismiss.
        var persisted = await db.Proposals.AsNoTracking().FirstAsync(p => p.Id == proposal.Id);
        persisted.Status.Should().Be(ProposalStatus.Rejected);
        persisted.ApprovedByUserId.Should().Be(userId);
    }

    [Fact]
    public async Task DismissAsync_Should_Succeed_When_NoRejectionHandlerRuns()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);
        var proposal = SeedProposed(db, tenantId);

        var service = new ProposalApprovalService(
            db,
            new TestCurrentUserProvider(userId),
            new FixedClock(DateTime.UtcNow),
            new DelegateDispatcher(),
            new DelegateRejectionDispatcher());

        var detail = await service.DismissAsync(proposal.Id);

        detail.Status.Should().Be("Rejected");
        detail.ApprovedByUserId.Should().Be(userId);
    }

    [Fact]
    public async Task ApproveAsync_Should_SetStatusApplied_When_HighRiskHandlerSucceeds()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);
        var proposal = SeedProposed(db, tenantId, riskTier: "High");

        var service = new ProposalApprovalService(
            db,
            new TestCurrentUserProvider(userId),
            new FixedClock(DateTime.UtcNow),
            new DelegateDispatcher { Impl = _ => Task.FromResult(new ProposalHandlerResult(Applied: true)) },
            new DelegateRejectionDispatcher());

        var detail = await service.ApproveAsync(proposal.Id);

        // Spec 032 §8.1: a High-risk proposal whose handler confirms execution
        // lands in the terminal Applied state, distinct from the Approved decision.
        detail.Status.Should().Be("Applied");

        var persisted = await db.Proposals.AsNoTracking().FirstAsync(p => p.Id == proposal.Id);
        persisted.Status.Should().Be(ProposalStatus.Applied);
        persisted.ApprovedByUserId.Should().Be(userId, "the approver stamp is kept on a successful High dispatch");
    }

    [Fact]
    public async Task ApproveAsync_Should_MarkFailedTerminally_When_HighRiskHandlerThrows()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);
        var proposal = SeedProposed(db, tenantId, riskTier: "High");

        var service = new ProposalApprovalService(
            db,
            new TestCurrentUserProvider(userId),
            new FixedClock(DateTime.UtcNow),
            new DelegateDispatcher { Impl = _ => throw new InvalidOperationException("partner timeout") },
            new DelegateRejectionDispatcher());

        await FluentActions.Invoking(() => service.ApproveAsync(proposal.Id))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("partner timeout");

        // Spec 032 §8.1: a High-risk dispatch whose outcome is unknown must NOT revert
        // to Proposed (re-approving could double-move funds). It is terminal Failed,
        // with the approver stamp preserved for the audit trail.
        var persisted = await db.Proposals.AsNoTracking().FirstAsync(p => p.Id == proposal.Id);
        persisted.Status.Should().Be(ProposalStatus.Failed);
        persisted.ApprovedByUserId.Should().Be(userId);
        persisted.ApprovedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ApproveAsync_Should_MarkFailedTerminally_When_HighRiskHandlerReportsAppliedFalse()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);
        var proposal = SeedProposed(db, tenantId, riskTier: "High");

        var service = new ProposalApprovalService(
            db,
            new TestCurrentUserProvider(userId),
            new FixedClock(DateTime.UtcNow),
            new DelegateDispatcher
            {
                Impl = _ => Task.FromResult(new ProposalHandlerResult(Applied: false, Message: "intent already captured"))
            },
            new DelegateRejectionDispatcher());

        var thrown = await FluentActions.Invoking(() => service.ApproveAsync(proposal.Id))
            .Should().ThrowAsync<ProposalExecutionFailedException>();
        thrown.Which.ProposalId.Should().Be(proposal.Id);

        // Even an expected business failure is terminal for High — no revert-to-Proposed.
        var persisted = await db.Proposals.AsNoTracking().FirstAsync(p => p.Id == proposal.Id);
        persisted.Status.Should().Be(ProposalStatus.Failed);
        persisted.ApprovedByUserId.Should().Be(userId);
    }
}
