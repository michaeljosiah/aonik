using Aonik.PersonalFinance.Contracts.Services;
using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Persistence;
using Aonik.PersonalFinance.Services;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Modules;
using Aonik.SharedKernel.Persistence;
using Aonik.Worker.Jobs;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using Quartz;

namespace Aonik.Application.Tests.PersonalFinance;

/// <summary>
/// Spec 097 §12.2 / acceptance 11 for the Personal Finance recurring sync, and the starvation
/// regression: the module gate runs BEFORE the batch is taken, so a disabled tenant whose due
/// connections outnumber the batch can never crowd out an enabled tenant. The disabled tenant's
/// connections are neither synced nor rescheduled, and the skip is recorded in the result.
/// </summary>
public class FinancialConnectionRecurringSyncJobModuleGateTests
{
    private static readonly Guid EnabledTenant = Guid.NewGuid();
    private static readonly Guid DisabledTenant = Guid.NewGuid();
    private const string Provider = "TestBank";

    [Fact]
    public async Task Execute_Should_SyncEnabledTenant_When_DisabledTenantHoldsMoreDueConnectionsThanTheBatch()
    {
        // Arrange — batch size 1; the disabled tenant has two connections that are due EARLIER than the
        // enabled tenant's one, which is exactly the shape that starved the enabled tenant before.
        var tenantContext = new MutableTenantContext();
        var tenantProvider = new ContextTenantProvider(tenantContext);
        await using var db = new PersonalFinanceDbContext(
            new DbContextOptionsBuilder<PersonalFinanceDbContext>().UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}").Options,
            tenantProvider);

        var disabledUser = Guid.NewGuid();
        var disabledDueAt = DateTime.UtcNow.AddHours(-3);
        var disabledA = await SeedConnectionAsync(db, tenantContext, DisabledTenant, disabledUser, disabledDueAt, withLinkedAccount: false);
        var disabledB = await SeedConnectionAsync(db, tenantContext, DisabledTenant, disabledUser, disabledDueAt.AddMinutes(1), withLinkedAccount: false);
        var enabledUser = Guid.NewGuid();
        var enabled = await SeedConnectionAsync(db, tenantContext, EnabledTenant, enabledUser, DateTime.UtcNow.AddMinutes(-5), withLinkedAccount: true);

        var gateway = new Mock<IPersonalAccountLinkProviderGateway>();
        gateway.SetupGet(g => g.ProviderCode).Returns(Provider);
        gateway.SetupGet(g => g.DisplayName).Returns(Provider);
        var syncedAt = DateTime.UtcNow;
        gateway.Setup(g => g.SyncTransactionsAsync(It.IsAny<AccountLinkProviderTransactionsSyncRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountLinkProviderTransactionsSyncResult(null, syncedAt, "Synced", null, [], []));

        var syncOptions = new FinancialConnectionSyncOptions { EnableRecurringSync = true, DefaultSyncIntervalMinutes = 60, BatchSize = 1 };
        var orchestrator = new FinancialConnectionTransactionSyncOrchestrator(
            db,
            tenantContext,
            [gateway.Object],
            Microsoft.Extensions.Options.Options.Create(syncOptions),
            NullLogger<FinancialConnectionTransactionSyncOrchestrator>.Instance,
            new NoOpGraphCacheInvalidator());

        var jobOptions = new ScheduledJobOptions();
        jobOptions.FinancialConnectionSync.BatchSize = 1;

        var job = new FinancialConnectionRecurringSyncJob(
            db,
            orchestrator,
            tenantContext,
            Microsoft.Extensions.Options.Options.Create(jobOptions),
            Microsoft.Extensions.Options.Options.Create(syncOptions),
            NullLogger<FinancialConnectionRecurringSyncJob>.Instance,
            new FakeReader(DisabledTenant));
        var context = JobContext();

        // Act
        await job.Execute(context);

        // Assert
        gateway.Verify(
            g => g.SyncTransactionsAsync(It.Is<AccountLinkProviderTransactionsSyncRequest>(r => r.ConnectionId == enabled && r.TenantId == EnabledTenant), It.IsAny<CancellationToken>()),
            Times.Once);
        gateway.Verify(
            g => g.SyncTransactionsAsync(It.Is<AccountLinkProviderTransactionsSyncRequest>(r => r.TenantId == DisabledTenant), It.IsAny<CancellationToken>()),
            Times.Never);

        db.ChangeTracker.Clear();
        var connections = await db.FinancialConnections.AcrossTenants().AsNoTracking().ToListAsync();
        connections.Single(c => c.Id == enabled).LastSyncedAt.Should().Be(syncedAt, "the enabled tenant's connection was synced");
        connections.Single(c => c.Id == enabled).NextScheduledSyncAt.Should().Be(syncedAt.AddMinutes(60));
        connections.Single(c => c.Id == disabledA).NextScheduledSyncAt.Should().Be(disabledDueAt, "a disabled tenant's connection is neither synced nor rescheduled");
        connections.Single(c => c.Id == disabledB).LastSyncedAt.Should().BeNull();

        context.Result.Should().BeOfType<string>().Which.Should()
            .Contain("Synced 1")
            .And.Contain($"Skipped 1 tenant(s) with module '{ModuleIds.PersonalFinance}' disabled");
    }

    [Fact]
    public async Task Execute_Should_ReportOnlyTheSkip_When_EveryDueTenantHasPersonalFinanceOff()
    {
        // Arrange
        var tenantContext = new MutableTenantContext();
        var tenantProvider = new ContextTenantProvider(tenantContext);
        await using var db = new PersonalFinanceDbContext(
            new DbContextOptionsBuilder<PersonalFinanceDbContext>().UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}").Options,
            tenantProvider);
        await SeedConnectionAsync(db, tenantContext, DisabledTenant, Guid.NewGuid(), DateTime.UtcNow.AddHours(-1), withLinkedAccount: false);

        var gateway = new Mock<IPersonalAccountLinkProviderGateway>(MockBehavior.Strict);
        gateway.SetupGet(g => g.ProviderCode).Returns(Provider);
        var syncOptions = new FinancialConnectionSyncOptions { EnableRecurringSync = true };
        var orchestrator = new FinancialConnectionTransactionSyncOrchestrator(
            db, tenantContext, [gateway.Object], Microsoft.Extensions.Options.Options.Create(syncOptions),
            NullLogger<FinancialConnectionTransactionSyncOrchestrator>.Instance,
            new NoOpGraphCacheInvalidator());
        var job = new FinancialConnectionRecurringSyncJob(
            db, orchestrator, tenantContext, Microsoft.Extensions.Options.Options.Create(new ScheduledJobOptions()), Microsoft.Extensions.Options.Options.Create(syncOptions),
            NullLogger<FinancialConnectionRecurringSyncJob>.Instance, new FakeReader(DisabledTenant));
        var context = JobContext();

        // Act
        await job.Execute(context);

        // Assert
        context.Result.Should().BeOfType<string>().Which.Should()
            .Contain("No connections due for sync in tenants with Personal Finance enabled")
            .And.Contain("Skipped 1 tenant(s)");
    }

    private static async Task<Guid> SeedConnectionAsync(
        PersonalFinanceDbContext db,
        MutableTenantContext tenantContext,
        Guid tenantId,
        Guid userId,
        DateTime nextScheduledSyncAt,
        bool withLinkedAccount)
    {
        // EnforceTenantOnWrites: the ambient tenant must match the rows being saved.
        tenantContext.TenantId = tenantId;

        var connection = new FinancialConnection
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Provider = Provider,
            ProviderConnectionReference = $"item-{Guid.NewGuid():N}",
            InstitutionName = "Test Bank",
            AutoSyncEnabled = true,
            SyncIntervalMinutes = 60,
            NextScheduledSyncAt = nextScheduledSyncAt,
            Status = "Connected",
            ConsentStatus = "Granted",
            SecretReference = "vault://test",
        };
        db.FinancialConnections.Add(connection);

        if (withLinkedAccount)
        {
            var account = new PersonalAccount
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = userId,
                Name = "Current account",
                AccountType = "Checking",
                Currency = "GBP",
                Status = "Active",
            };
            db.PersonalAccounts.Add(account);
            db.PersonalLinkedAccounts.Add(new PersonalLinkedAccount
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = userId,
                FinancialConnectionId = connection.Id,
                PersonalAccountId = account.Id,
                ProviderAccountReference = $"acct-{Guid.NewGuid():N}",
                Name = "Current account",
                AccountType = "Checking",
                Currency = "GBP",
                Status = "Connected",
            });
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        tenantContext.TenantId = null;
        return connection.Id;
    }

    private static IJobExecutionContext JobContext()
    {
        var mock = new Mock<IJobExecutionContext>();
        mock.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        mock.SetupProperty(c => c.Result);
        return mock.Object;
    }

    private sealed class NoOpGraphCacheInvalidator : IFinancialLifeGraphCacheInvalidator
    {
        public void InvalidateCurrentUserGraph()
        {
        }

        public Task InvalidateCurrentUserGraphAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task InvalidateUserGraphAsync(Guid userId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task InvalidateUserGraphsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task InvalidateAllGraphCachesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class MutableTenantContext : ITenantContext
    {
        public Guid? TenantId { get; set; }
        public string? ResolutionSource { get; set; }
        public bool IsResolved => TenantId.HasValue;
    }

    private sealed class ContextTenantProvider(ITenantContext context) : ITenantProvider
    {
        public Guid GetCurrentTenantId() => context.TenantId ?? Guid.Empty;

        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = context.TenantId ?? Guid.Empty;
            return context.TenantId.HasValue;
        }
    }

    /// <summary>Every module on for every tenant except Personal Finance for the given tenants.</summary>
    private sealed class FakeReader(params Guid[] personalFinanceOffFor) : IModuleEnablementReader
    {
        public Task<ModuleEnablementSet> GetAsync(Guid tenantId, CancellationToken ct = default)
        {
            var enabled = ModuleCatalog.All.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
            if (personalFinanceOffFor.Contains(tenantId))
                enabled.Remove(ModuleIds.PersonalFinance);
            return Task.FromResult(new ModuleEnablementSet(tenantId, enabled));
        }

        public Task<IReadOnlyList<Guid>> FilterEnabledTenantsAsync(
            IEnumerable<Guid> tenantIds, string moduleId, CancellationToken ct = default)
        {
            IReadOnlyList<Guid> result = moduleId == ModuleIds.PersonalFinance
                ? tenantIds.Distinct().Where(id => !personalFinanceOffFor.Contains(id)).ToList()
                : tenantIds.Distinct().ToList();
            return Task.FromResult(result);
        }
    }
}
