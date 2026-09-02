using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services;
using Aonik.Finance.Agents.Proposals;
using Aonik.Finance.Contracts.Services.Payments;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Modules;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Moq;

namespace Aonik.Application.Tests.Agents;

/// <summary>
/// Spec 097 §12.1: the proposal executor is the one execution seam the HTTP gate cannot see
/// (the approve endpoint is core). <see cref="ProposalDispatcher"/> refuses to run a handler whose
/// module is off for the proposal's tenant, and <see cref="ProposalApprovalService"/> lands such a
/// proposal in its terminal Failed state with the reason on the audit trail.
/// </summary>
public class ProposalDispatcherModuleGateTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    // ── ProposalDispatcher ────────────────────────────────────────────────────────────────────

    [Fact]
    public void GatedModuleId_Should_ResolveFinance_When_HandlerLivesInTheFinanceAssembly()
    {
        ProposalDispatcher.GatedModuleId(typeof(CapturePaymentProposalHandler)).Should().Be(ModuleIds.Finance);
        ProposalDispatcher.GatedModuleId(typeof(UnattributedHandler)).Should().BeNull("this test assembly carries no module attribute");
    }

    [Fact]
    public async Task DispatchAsync_Should_ThrowModuleDisabledAndNeverRunHandler_When_HandlerModuleIsOffForTenant()
    {
        // Arrange
        var payments = new Mock<IPaymentService>(MockBehavior.Strict);
        var dispatcher = CreateDispatcher(payments.Object, new FakeReader(disabled: ModuleIds.Finance));
        var proposal = Detail(CapturePaymentProposalHandler.ProposalTypeKey, $"{{\"paymentIntentId\":\"{Guid.NewGuid()}\"}}");

        // Act
        var act = () => dispatcher.DispatchAsync(proposal, CancellationToken.None);

        // Assert
        var thrown = await act.Should().ThrowAsync<ModuleDisabledException>();
        thrown.Which.ModuleId.Should().Be(ModuleIds.Finance);
        thrown.Which.Code.Should().Be(ModuleErrorCodes.Disabled);
        payments.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DispatchAsync_Should_RunHandler_When_HandlerModuleIsOnForTenant()
    {
        // Arrange — a payload without a paymentIntentId makes the real handler answer "not applied"
        // before it touches the payment service, which is enough to prove it ran.
        var payments = new Mock<IPaymentService>(MockBehavior.Strict);
        var dispatcher = CreateDispatcher(payments.Object, new FakeReader(disabled: ModuleIds.Workspaces));

        // Act
        var result = await dispatcher.DispatchAsync(Detail(CapturePaymentProposalHandler.ProposalTypeKey, "{}"), CancellationToken.None);

        // Assert
        result.Applied.Should().BeFalse();
        result.Message.Should().Contain("paymentIntentId");
    }

    [Fact]
    public async Task DispatchAsync_Should_RunHandler_When_NoReaderIsRegistered()
    {
        // Arrange
        var payments = new Mock<IPaymentService>(MockBehavior.Strict);
        var dispatcher = CreateDispatcher(payments.Object, reader: null);

        // Act
        var result = await dispatcher.DispatchAsync(Detail(CapturePaymentProposalHandler.ProposalTypeKey, "{}"), CancellationToken.None);

        // Assert
        result.Applied.Should().BeFalse("a host without the module graph dispatches as before");
    }

    // ── ProposalApprovalService ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Low")]
    [InlineData("High")]
    public async Task ApproveAsync_Should_MarkProposalFailedAndAuditTheReason_When_HandlerModuleIsDisabled(string riskTier)
    {
        // Arrange
        var userId = Guid.NewGuid();
        await using var db = CreateAgentsDb();
        var proposal = SeedProposed(db, riskTier);
        var audit = new Mock<IAuditLogWriter>();
        var dispatcher = new ThrowingDispatcher(new ModuleDisabledException(ModuleIds.Finance));
        var service = new ProposalApprovalService(
            db,
            new TestCurrentUserProvider(userId),
            new FixedClock(DateTime.UtcNow),
            dispatcher,
            new NoOpRejectionDispatcher(),
            audit.Object);

        // Act
        var act = () => service.ApproveAsync(proposal.Id);

        // Assert
        (await act.Should().ThrowAsync<ModuleDisabledException>()).Which.ModuleId.Should().Be(ModuleIds.Finance);

        var stored = await db.Proposals.AsNoTracking().SingleAsync(p => p.Id == proposal.Id);
        stored.Status.Should().Be(ProposalStatus.Failed, "a proposal blocked by the module gate is terminal for every tier");
        stored.ApprovedByUserId.Should().Be(userId, "a human did approve; only execution was refused");

        audit.Verify(a => a.LogAsync(
            AuditEventNames.ProposalBlockedByModuleGate,
            "Proposal",
            proposal.Id,
            TenantId,
            userId,
            It.IsAny<string?>(),
            It.Is<string?>(details => details != null
                && details.Contains(ModuleErrorCodes.Disabled)
                && details.Contains(ModuleIds.Finance)
                && details.Contains(nameof(ProposalStatus.Failed))),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveAsync_Should_StillMarkFailed_When_NoAuditWriterIsRegistered()
    {
        // Arrange
        await using var db = CreateAgentsDb();
        var proposal = SeedProposed(db, "High");
        var service = new ProposalApprovalService(
            db,
            new TestCurrentUserProvider(Guid.NewGuid()),
            new FixedClock(DateTime.UtcNow),
            new ThrowingDispatcher(new ModuleDisabledException(ModuleIds.Commerce)),
            new NoOpRejectionDispatcher());

        // Act
        var act = () => service.ApproveAsync(proposal.Id);

        // Assert
        await act.Should().ThrowAsync<ModuleDisabledException>();
        (await db.Proposals.AsNoTracking().SingleAsync(p => p.Id == proposal.Id)).Status.Should().Be(ProposalStatus.Failed);
    }

    // ── helpers ───────────────────────────────────────────────────────────────────────────────

    private static ProposalDispatcher CreateDispatcher(IPaymentService payments, IModuleEnablementReader? reader)
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IProposalHandler>(
            CapturePaymentProposalHandler.ProposalTypeKey,
            new CapturePaymentProposalHandler(payments));
        if (reader is not null)
            services.AddSingleton(reader);
        return new ProposalDispatcher(services.BuildServiceProvider());
    }

    private static AgentProposalDetail Detail(string proposalType, string payloadJson)
        => new(Guid.NewGuid(), TenantId, proposalType, "Approved", payloadJson, "test");

    private static AgentsDbContext CreateAgentsDb()
    {
        var options = new DbContextOptionsBuilder<AgentsDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;
        return new AgentsDbContext(options, new TestTenantProvider(TenantId));
    }

    private static Proposal SeedProposed(AgentsDbContext db, string riskTier)
    {
        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            Name = "finance-agent",
            Domain = "Finance",
            Description = "test",
            InstructionsText = string.Empty,
            ToolsetIdsJson = "[]",
            InputSchemaJson = "{}",
            OutputSchemaJson = "{}",
            PermissionsProfileJson = "{}",
            RiskTier = riskTier,
            IsActive = true,
        };
        var proposal = new Proposal
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            ProposalType = CapturePaymentProposalHandler.ProposalTypeKey,
            ProposedByAgentId = agent.Id,
            AiRunId = Guid.NewGuid(),
            ImpactSummary = "Capture payment",
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

    private sealed class UnattributedHandler : IProposalHandler
    {
        public string ProposalType => "Test.Unattributed";

        public Task<ProposalHandlerResult> HandleAsync(AgentProposalDetail proposal, CancellationToken cancellationToken)
            => Task.FromResult(new ProposalHandlerResult(true));
    }

    private sealed class ThrowingDispatcher(Exception exception) : IProposalDispatcher
    {
        public Task<ProposalHandlerResult> DispatchAsync(AgentProposalDetail proposal, CancellationToken ct)
            => throw exception;
    }

    private sealed class NoOpRejectionDispatcher : IProposalRejectionDispatcher
    {
        public Task DispatchAsync(AgentProposalDetail proposal, CancellationToken ct) => Task.CompletedTask;
    }

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

    private sealed class FakeReader(params string[] disabled) : IModuleEnablementReader
    {
        public Task<ModuleEnablementSet> GetAsync(Guid tenantId, CancellationToken ct = default)
        {
            var enabled = ModuleCatalog.All.Select(m => m.Id).Except(disabled, StringComparer.Ordinal)
                .ToHashSet(StringComparer.Ordinal);
            return Task.FromResult(new ModuleEnablementSet(tenantId, enabled));
        }

        public Task<IReadOnlyList<Guid>> FilterEnabledTenantsAsync(
            IEnumerable<Guid> tenantIds, string moduleId, CancellationToken ct = default)
        {
            IReadOnlyList<Guid> result = disabled.Contains(moduleId, StringComparer.Ordinal) ? [] : tenantIds.Distinct().ToList();
            return Task.FromResult(result);
        }
    }
}
