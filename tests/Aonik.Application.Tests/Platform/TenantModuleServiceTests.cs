using Aonik.Platform.Entities.Modules;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Modules;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Modules;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using ZiggyCreatures.Caching.Fusion;

namespace Aonik.Application.Tests.Platform;

/// <summary>
/// Spec 097 §7 / §16: the read side of per-tenant module enablement — defaults, row overlay, caching
/// and invalidation, and the one-query tenant filter. The resolver's graph rules are proven in
/// SharedKernel.Tests; these tests prove the service wires rows, cache and resolver together.
/// </summary>
public class TenantModuleServiceTests
{
    private static IReadOnlySet<string> AllIds
        => ModuleCatalog.All.Select(descriptor => descriptor.Id).ToHashSet(StringComparer.Ordinal);

    private readonly DbContextOptions<PlatformDbContext> _options = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
        .Options;

    private readonly IFusionCache _cache = new FusionCache(new FusionCacheOptions());

    /// <summary>
    /// The service's own context is deliberately scoped to an UNRELATED ambient tenant: host admins
    /// and Worker jobs read other tenants' state, so the reader must not depend on the tenant filter.
    /// The write-side collaborators are inert mocks: nothing here writes.
    /// </summary>
    private TenantModuleService CreateService()
        => new(
            new PlatformDbContext(_options, new TestTenantProvider(Guid.NewGuid()), new TestCurrentUserProvider()),
            _cache,
            NullLogger<TenantModuleService>.Instance,
            Mock.Of<IClock>(),
            new TestCurrentUserProvider(),
            Mock.Of<ICorrelationContext>(),
            Mock.Of<IAuditLogWriter>(),
            Mock.Of<IEventBus>(),
            Mock.Of<ITenantContext>(),
            Mock.Of<IPermissionService>());

    private async Task SeedRowAsync(Guid tenantId, string moduleId, bool isEnabled, bool deleted = false)
    {
        await using var context = new PlatformDbContext(_options, new TestTenantProvider(tenantId), new TestCurrentUserProvider());
        context.TenantModules.Add(new TenantModule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ModuleId = moduleId,
            IsEnabled = isEnabled,
            Source = TenantModuleSource.Explicit,
            IsDeleted = deleted,
        });
        await context.SaveChangesAsync();
    }

    private async Task SetRowAsync(Guid tenantId, string moduleId, bool isEnabled)
    {
        await using var context = new PlatformDbContext(_options, new TestTenantProvider(tenantId), new TestCurrentUserProvider());
        var row = await context.TenantModules.SingleAsync(x => x.TenantId == tenantId && x.ModuleId == moduleId);
        row.IsEnabled = isEnabled;
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetAsync_Should_ReturnEveryModule_When_TenantHasNoRows()
    {
        var tenantId = Guid.NewGuid();
        var service = CreateService();

        var set = await service.GetAsync(tenantId);

        set.TenantId.Should().Be(tenantId);
        set.Enabled.Should().BeEquivalentTo(AllIds, "an absent row means the catalogue default, which is on");
        set.IsEnabled(ModuleIds.Commerce).Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_Should_ReportCommerceOff_When_RowDisablesIt()
    {
        var tenantId = Guid.NewGuid();
        await SeedRowAsync(tenantId, ModuleIds.Commerce, isEnabled: false);
        var service = CreateService();

        var set = await service.GetAsync(tenantId);

        set.IsEnabled(ModuleIds.Commerce).Should().BeFalse();
        set.Enabled.Should().BeEquivalentTo(AllIds.Except([ModuleIds.Commerce]));
    }

    [Fact]
    public async Task GetAsync_Should_CloseOverDependencies_When_RowDisablesFinance()
    {
        var tenantId = Guid.NewGuid();
        await SeedRowAsync(tenantId, ModuleIds.Finance, isEnabled: false);
        var service = CreateService();

        var set = await service.GetAsync(tenantId);

        set.IsEnabled(ModuleIds.Finance).Should().BeFalse();
        set.IsEnabled(ModuleIds.Commerce).Should().BeFalse("commerce hard-depends on finance");
        set.IsEnabled(ModuleIds.Subscriptions).Should().BeFalse();
        set.IsEnabled(ModuleIds.Workspaces).Should().BeFalse();
        set.IsEnabled(ModuleIds.PersonalFinance).Should().BeTrue("only a soft dependency");
    }

    [Fact]
    public async Task GetAsync_Should_IgnoreSoftDeletedRows()
    {
        var tenantId = Guid.NewGuid();
        await SeedRowAsync(tenantId, ModuleIds.Commerce, isEnabled: false, deleted: true);
        var service = CreateService();

        var set = await service.GetAsync(tenantId);

        set.IsEnabled(ModuleIds.Commerce).Should().BeTrue("AcrossTenants drops the soft-delete filter, so the query excludes deleted rows itself");
    }

    [Fact]
    public async Task GetAsync_Should_KeepCoreModulesOn_When_RowsSayOtherwise()
    {
        var tenantId = Guid.NewGuid();
        await SeedRowAsync(tenantId, ModuleIds.Platform, isEnabled: false);
        await SeedRowAsync(tenantId, ModuleIds.Agents, isEnabled: false);
        var service = CreateService();

        var set = await service.GetAsync(tenantId);

        set.Enabled.Should().Contain(ModuleCatalog.CoreIds);
    }

    [Fact]
    public async Task GetAsync_Should_NotLeakRowsBetweenTenants()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await SeedRowAsync(tenantA, ModuleIds.Commerce, isEnabled: false);
        var service = CreateService();

        var setA = await service.GetAsync(tenantA);
        var setB = await service.GetAsync(tenantB);

        setA.IsEnabled(ModuleIds.Commerce).Should().BeFalse();
        setB.IsEnabled(ModuleIds.Commerce).Should().BeTrue("tenant B has no rows; A's row is A's alone");
    }

    [Fact]
    public async Task GetAsync_Should_ServeFromCache_UntilInvalidated()
    {
        var tenantId = Guid.NewGuid();
        await SeedRowAsync(tenantId, ModuleIds.Commerce, isEnabled: false);

        (await CreateService().GetAsync(tenantId)).IsEnabled(ModuleIds.Commerce).Should().BeFalse();

        // Flip the row underneath the cache. A fresh service instance (empty per-scope memo) still
        // sees the cached value — that is the FusionCache layer, not the memo, holding the entry.
        await SetRowAsync(tenantId, ModuleIds.Commerce, isEnabled: true);
        var second = CreateService();
        (await second.GetAsync(tenantId)).IsEnabled(ModuleIds.Commerce).Should().BeFalse(
            "the cached set is served until a write invalidates it");

        await second.InvalidateAsync(tenantId);

        (await CreateService().GetAsync(tenantId)).IsEnabled(ModuleIds.Commerce).Should().BeTrue(
            "after invalidation the next read resolves from the store");
    }

    [Fact]
    public async Task GetAsync_Should_MemoiseWithinAScope_AfterInvalidationIsCleared()
    {
        var tenantId = Guid.NewGuid();
        await SeedRowAsync(tenantId, ModuleIds.Commerce, isEnabled: false);
        var service = CreateService();

        var first = await service.GetAsync(tenantId);
        var again = await service.GetAsync(tenantId);
        again.Should().BeSameAs(first, "one lookup per scope: the gate, manifest and agent resolver share it");

        await SetRowAsync(tenantId, ModuleIds.Commerce, isEnabled: true);
        await service.InvalidateAsync(tenantId);
        var fresh = await service.GetAsync(tenantId);

        fresh.Should().NotBeSameAs(first);
        fresh.IsEnabled(ModuleIds.Commerce).Should().BeTrue();
    }

    [Fact]
    public async Task ChangedEventHandler_Should_InvalidateTheTenantCache()
    {
        var tenantId = Guid.NewGuid();
        await SeedRowAsync(tenantId, ModuleIds.Commerce, isEnabled: false);
        (await CreateService().GetAsync(tenantId)).IsEnabled(ModuleIds.Commerce).Should().BeFalse();
        await SetRowAsync(tenantId, ModuleIds.Commerce, isEnabled: true);

        var handler = new TenantModulesChangedCacheInvalidator(CreateService());
        await handler.HandleAsync(new TenantModulesChangedEvent(tenantId, [ModuleIds.Commerce], [], null));

        (await CreateService().GetAsync(tenantId)).IsEnabled(ModuleIds.Commerce).Should().BeTrue();
    }

    [Fact]
    public async Task FilterEnabledTenantsAsync_Should_ReturnOnlyTenantsWithTheModuleOn()
    {
        var noRows = Guid.NewGuid();
        var commerceOff = Guid.NewGuid();
        var financeOff = Guid.NewGuid();
        await SeedRowAsync(commerceOff, ModuleIds.Commerce, isEnabled: false);
        await SeedRowAsync(financeOff, ModuleIds.Finance, isEnabled: false);
        var service = CreateService();

        var commerceTenants = await service.FilterEnabledTenantsAsync(
            [noRows, commerceOff, financeOff], ModuleIds.Commerce);
        var financeTenants = await service.FilterEnabledTenantsAsync(
            [noRows, commerceOff, financeOff], ModuleIds.Finance);

        commerceTenants.Should().Equal(noRows);
        financeTenants.Should().Equal(noRows, commerceOff);
    }

    [Fact]
    public async Task FilterEnabledTenantsAsync_Should_ApplyDependencyClosure()
    {
        var financeOff = Guid.NewGuid();
        await SeedRowAsync(financeOff, ModuleIds.Finance, isEnabled: false);
        // The tenant's own row says workspaces is on, but its hard chain (subscriptions -> finance) is off.
        await SeedRowAsync(financeOff, ModuleIds.Workspaces, isEnabled: true);
        var service = CreateService();

        var tenants = await service.FilterEnabledTenantsAsync([financeOff], ModuleIds.Workspaces);

        tenants.Should().BeEmpty("the filter applies the same dependency-closed resolution as GetAsync");
    }

    [Fact]
    public async Task FilterEnabledTenantsAsync_Should_ReturnEveryTenant_When_ModuleIsCore()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        await SeedRowAsync(a, ModuleIds.Platform, isEnabled: false);
        await SeedRowAsync(b, ModuleIds.Agents, isEnabled: false);
        var service = CreateService();

        (await service.FilterEnabledTenantsAsync([a, b], ModuleIds.Platform)).Should().Equal(a, b);
        (await service.FilterEnabledTenantsAsync([a, b], ModuleIds.Agents)).Should().Equal(a, b);
    }

    [Fact]
    public async Task FilterEnabledTenantsAsync_Should_PreserveOrderAndCollapseDuplicates()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var service = CreateService();

        var tenants = await service.FilterEnabledTenantsAsync([second, first, second], ModuleIds.Commerce);

        tenants.Should().Equal(second, first);
    }

    [Fact]
    public async Task FilterEnabledTenantsAsync_Should_ReturnEmpty_When_NoTenantsRequested()
    {
        var service = CreateService();

        var tenants = await service.FilterEnabledTenantsAsync([], ModuleIds.Commerce);

        tenants.Should().BeEmpty();
    }

    [Fact]
    public async Task FilterEnabledTenantsAsync_Should_Throw_When_ModuleIdIsUnknown()
    {
        var service = CreateService();

        var act = () => service.FilterEnabledTenantsAsync([Guid.NewGuid()], "not-a-module");

        await act.Should().ThrowAsync<ArgumentException>("a job with a mistyped module id must fail loudly, not silently skip every tenant");
    }
}
