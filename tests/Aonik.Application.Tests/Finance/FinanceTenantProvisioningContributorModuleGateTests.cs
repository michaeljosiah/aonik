using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Provisioning;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ledgers;
using Aonik.SharedKernel.Modules;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Finance;

/// <summary>
/// Spec 097 §12.4 for the chart of accounts: an <see cref="ILedgerAccountContributor"/> whose module
/// is off for the tenant contributes no accounts, so a tenant never receives ledger rows for a module
/// it does not have. Core and unknown module names, and a host without the reader, contribute as before.
/// </summary>
public class FinanceTenantProvisioningContributorModuleGateTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc);

    private const string SubscriptionsCode = "2400-SUBSCRIPTION-DEFERRED";
    private const string LegacyCode = "9900-LEGACY";

    [Fact]
    public async Task ContributeProvisioningAsync_Should_SkipSubscriptionsAccounts_When_SubscriptionsIsOffForTenant()
    {
        // Arrange
        await using var db = CreateDb();
        var contributor = new FinanceTenantProvisioningContributor(
            db,
            [new FakeAccountContributor(ModuleIds.Subscriptions, SubscriptionsCode), new FakeAccountContributor("LegacyName", LegacyCode)],
            new FakeReader(disabled: ModuleIds.Subscriptions));

        // Act
        var contribution = await contributor.ContributeProvisioningAsync(Context());

        // Assert
        var codes = await db.LedgerAccounts.AsNoTracking().Where(a => a.TenantId == TenantId).Select(a => a.Code).ToListAsync();
        codes.Should().NotContain(SubscriptionsCode, "the Subscriptions module is off for this tenant");
        codes.Should().Contain(LegacyCode, "a name the catalogue does not know is never gated");
        contribution.ActionsPerformed.Should().Contain($"Skipped ledger accounts contributed by {ModuleIds.Subscriptions}: module disabled for tenant");
    }

    [Fact]
    public async Task ContributeProvisioningAsync_Should_CreateSubscriptionsAccounts_When_SubscriptionsIsOnForTenant()
    {
        // Arrange
        await using var db = CreateDb();
        var contributor = new FinanceTenantProvisioningContributor(
            db,
            [new FakeAccountContributor(ModuleIds.Subscriptions, SubscriptionsCode)],
            new FakeReader(disabled: ModuleIds.Workspaces));

        // Act
        await contributor.ContributeProvisioningAsync(Context());

        // Assert
        (await db.LedgerAccounts.AsNoTracking().AnyAsync(a => a.TenantId == TenantId && a.Code == SubscriptionsCode)).Should().BeTrue();
    }

    [Fact]
    public async Task ContributeProvisioningAsync_Should_CreateEveryContributedAccount_When_NoReaderIsRegistered()
    {
        // Arrange
        await using var db = CreateDb();
        var contributor = new FinanceTenantProvisioningContributor(
            db,
            [new FakeAccountContributor(ModuleIds.Subscriptions, SubscriptionsCode)]);

        // Act
        await contributor.ContributeProvisioningAsync(Context());

        // Assert
        (await db.LedgerAccounts.AsNoTracking().AnyAsync(a => a.TenantId == TenantId && a.Code == SubscriptionsCode)).Should().BeTrue(
            "a host without the module graph provisions every contributor, as before Spec 097");
    }

    [Theory]
    [InlineData(ModuleIds.Subscriptions, false, true)]
    [InlineData(ModuleIds.Subscriptions, true, false)]
    [InlineData(ModuleIds.Ai, false, false)]
    [InlineData("LegacyName", false, false)]
    public void IsModuleDisabled_Should_SkipOnlyKnownNonCoreDisabledModules(string moduleName, bool enabledInSet, bool expectedSkip)
    {
        var enabled = ModuleCatalog.All.Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
        if (!enabledInSet)
            enabled.Remove(moduleName);

        FinanceTenantProvisioningContributor.IsModuleDisabled(moduleName, new ModuleEnablementSet(TenantId, enabled)).Should().Be(expectedSkip);
        FinanceTenantProvisioningContributor.IsModuleDisabled(moduleName, null).Should().BeFalse();
    }

    private static TenantProvisioningContext Context() => new(TenantId, "GBP", null, Now);

    private static FinanceDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;
        return new FinanceDbContext(options, new TestTenantProvider(TenantId));
    }

    private sealed class FakeAccountContributor(string moduleName, string code) : ILedgerAccountContributor
    {
        public string ModuleName => moduleName;

        public IReadOnlyCollection<LedgerAccountDefinition> GetAccounts()
            => [new LedgerAccountDefinition(code, $"Account {code}", "Liability")];
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
