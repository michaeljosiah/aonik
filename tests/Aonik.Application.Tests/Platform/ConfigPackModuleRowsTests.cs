using Aonik.Platform.Contracts.Services.ReferenceData;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Modules;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Modules;
using Aonik.Platform.Services.Packs;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.SharedKernel.Abstractions.Packs;
using Aonik.SharedKernel.Events;
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
/// Spec 097 §13: <see cref="ConfigPackApplier.ApplyModulesAsync"/> turns a pack's <c>modules</c> list
/// into one pack-sourced <see cref="TenantModule"/> row per catalogue module — declared + hard
/// dependencies + core on, the rest off — on the tenant's initial provisioning, and is additive only
/// on every later run (never a disabling row, never a touched explicit row).
/// </summary>
public class ConfigPackModuleRowsTests
{
    private static readonly string[] FoodCommerceExpectedOn =
    [
        ModuleIds.Commerce,
        ModuleIds.Finance,
        ModuleIds.Ordering,
        ModuleIds.Ai,
        ModuleIds.Agents,
        ModuleIds.Platform,
    ];

    // The revised simi pack (spec §13): personal-finance, groups, documents, voice + core. Nothing in
    // that list hard-depends on finance, commerce, subscriptions or workspaces, so those resolve off.
    private static readonly string[] SimiExpectedOn =
    [
        ModuleIds.PersonalFinance,
        ModuleIds.Groups,
        ModuleIds.Documents,
        ModuleIds.Voice,
        ModuleIds.Ai,
        ModuleIds.Agents,
        ModuleIds.Platform,
        ModuleIds.Ordering,
    ];

    private static readonly string[] SimiExpectedOff =
    [
        ModuleIds.Finance,
        ModuleIds.Commerce,
        ModuleIds.Subscriptions,
        ModuleIds.Workspaces,
    ];

    private readonly Guid _tenantId = Guid.NewGuid();

    private readonly DbContextOptions<PlatformDbContext> _options = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
        .Options;

    private PlatformDbContext NewDb()
        => new(_options, new TestTenantProvider(_tenantId), new TestCurrentUserProvider());

    private static ConfigPackApplier NewApplier(IConfigPackSource source, PlatformDbContext db, ITenantContext? tenantContext = null, TenantModuleService? moduleService = null)
        => new(
            source,
            db,
            new Mock<IAgentConfigurationService>().Object,
            new Mock<IReferenceDataService>().Object,
            tenantContext ?? new FakeTenantContext(),
            moduleService);

    private async Task SeedTenantAsync(string businessType, int? appliedPackVersion = null)
    {
        await using var db = NewDb();
        db.Tenants.Add(new Tenant
        {
            Id = _tenantId,
            Name = $"Tenant-{_tenantId:N}",
            Environment = "Test",
            DefaultCurrency = "GBP",
            BusinessType = businessType,
            AppliedPackVersion = appliedPackVersion,
            SupportedCountriesJson = "[]",
            AllowedOriginCountriesJson = "[]",
            AllowedDestinationCountriesJson = "[]",
        });
        await db.SaveChangesAsync();
    }

    private TenantModuleService NewReader(PlatformDbContext db)
        => new(
            db,
            new FusionCache(new FusionCacheOptions()),
            NullLogger<TenantModuleService>.Instance,
            Mock.Of<IClock>(),
            new TestCurrentUserProvider(),
            Mock.Of<ICorrelationContext>(),
            Mock.Of<IAuditLogWriter>(),
            Mock.Of<IEventBus>(),
            Mock.Of<ITenantContext>(),
            Mock.Of<IPermissionService>());

    private async Task<List<TenantModule>> RowsAsync()
    {
        await using var db = NewDb();
        return await db.TenantModules.AsNoTracking().Where(row => row.TenantId == _tenantId).ToListAsync();
    }

    [Fact]
    public async Task ApplyModulesAsync_Should_WriteOneRowPerCatalogueModule_When_PackDeclaresModules()
    {
        await SeedTenantAsync("food-commerce");
        await using var db = NewDb();
        var applier = NewApplier(new ConfigPackSource(), db);

        var actions = await applier.ApplyModulesAsync(_tenantId, "food-commerce", initialProvisioning: true);

        var rows = await RowsAsync();
        rows.Should().HaveCount(ModuleCatalog.All.Count, "one row per catalogue module");
        rows.Should().OnlyContain(row => row.Source == TenantModuleSource.Pack);
        rows.Should().OnlyContain(row => row.Reason == "pack:food-commerce@v1");
        rows.Where(row => row.IsEnabled).Select(row => row.ModuleId).Should().BeEquivalentTo(FoodCommerceExpectedOn,
            "commerce pulls finance and ordering through the hard-dependency closure; core is always on");
        rows.Where(row => !row.IsEnabled).Select(row => row.ModuleId).Should().BeEquivalentTo(
            ModuleCatalog.All.Select(descriptor => descriptor.Id).Except(FoodCommerceExpectedOn));
        actions.Should().ContainSingle().Which.Should().Contain("food-commerce").And.Contain(ModuleIds.Commerce);
    }

    [Fact]
    public async Task ApplyModulesAsync_Should_WriteNoRows_When_PackDeclaresNoModules()
    {
        await SeedTenantAsync("base");
        await using var db = NewDb();
        var applier = NewApplier(new ConfigPackSource(), db);

        var actions = await applier.ApplyModulesAsync(_tenantId, "base", initialProvisioning: true);

        (await RowsAsync()).Should().BeEmpty("the base pack leaves the catalogue defaults");
        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyModulesAsync_Should_WriteNoRows_When_NoPackIsInstalled()
    {
        await SeedTenantAsync("nonexistent-type");
        await using var db = NewDb();
        var applier = NewApplier(new ConfigPackSource(), db);

        var actions = await applier.ApplyModulesAsync(_tenantId, "nonexistent-type", initialProvisioning: true);

        (await RowsAsync()).Should().BeEmpty();
        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyModulesAsync_Should_LeaveExplicitRowUntouched_When_Reapplied()
    {
        await SeedTenantAsync("food-commerce");
        await using (var seed = NewDb())
        {
            // A host admin switched commerce off explicitly before the pack ran again.
            seed.TenantModules.Add(new TenantModule
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                ModuleId = ModuleIds.Commerce,
                IsEnabled = false,
                Source = TenantModuleSource.Explicit,
                Reason = "admin: paused",
            });
            await seed.SaveChangesAsync();
        }

        await using var db = NewDb();
        var applier = NewApplier(new ConfigPackSource(), db);

        // The tenant already has a row, so the provisioner treats this as a re-apply (additive path).
        await applier.ApplyModulesAsync(_tenantId, "food-commerce", initialProvisioning: false);

        var rows = await RowsAsync();
        var commerce = rows.Single(row => row.ModuleId == ModuleIds.Commerce);
        commerce.IsEnabled.Should().BeFalse("an explicit row is never overwritten by a pack");
        commerce.Source.Should().Be(TenantModuleSource.Explicit);
        commerce.Reason.Should().Be("admin: paused");
        rows.Where(row => row.ModuleId != ModuleIds.Commerce).Select(row => row.ModuleId)
            .Should().BeEquivalentTo(FoodCommerceExpectedOn.Except([ModuleIds.Commerce]),
                "the additive path creates rows only for the declared + closure + core set");
        rows.Where(row => row.ModuleId != ModuleIds.Commerce).Should().OnlyContain(row => row.IsEnabled,
            "the additive path never writes a disabling row");
    }

    [Fact]
    public async Task ApplyModulesAsync_Should_LeaveExplicitRowUntouched_When_InitialProvisioningFindsOne()
    {
        await SeedTenantAsync("food-commerce");
        await using (var seed = NewDb())
        {
            seed.TenantModules.Add(new TenantModule
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                ModuleId = ModuleIds.Commerce,
                IsEnabled = false,
                Source = TenantModuleSource.Explicit,
                Reason = "admin: paused",
            });
            await seed.SaveChangesAsync();
        }

        await using var db = NewDb();
        var applier = NewApplier(new ConfigPackSource(), db);

        await applier.ApplyModulesAsync(_tenantId, "food-commerce", initialProvisioning: true);

        var rows = await RowsAsync();
        rows.Should().HaveCount(ModuleCatalog.All.Count, "the other rows are still written on the authoritative path");
        var commerce = rows.Single(row => row.ModuleId == ModuleIds.Commerce);
        commerce.IsEnabled.Should().BeFalse("an explicit row is never overwritten by a pack");
        commerce.Source.Should().Be(TenantModuleSource.Explicit);
        commerce.Reason.Should().Be("admin: paused");
    }

    [Fact]
    public async Task ApplyModulesAsync_Should_NotDisableAnything_When_TenantWasProvisionedBeforeModuleRowsExisted()
    {
        // A pre-Spec-097 tenant: the pack was applied once (version stamped) but no module rows exist,
        // so it resolves to "everything on". Re-running provisioning must keep it that way.
        await SeedTenantAsync("simi", appliedPackVersion: 1);
        await using var db = NewDb();
        var reader = NewReader(db);
        var applier = NewApplier(new ConfigPackSource(), db, moduleService: reader);

        var actions = await applier.ApplyModulesAsync(_tenantId, "simi", initialProvisioning: false);

        var rows = await RowsAsync();
        rows.Should().NotBeEmpty();
        rows.Should().OnlyContain(row => row.IsEnabled, "a re-apply never writes a disabling row");
        rows.Select(row => row.ModuleId).Should().BeEquivalentTo(SimiExpectedOn,
            "only the declared + closure + core set gets a (enabling) row");
        rows.Should().OnlyContain(row => row.Source == TenantModuleSource.Pack && row.Reason == "pack:simi@v1");

        var resolved = await reader.GetAsync(_tenantId);
        resolved.IsEnabled(ModuleIds.Finance).Should().BeTrue("finance had no row before and still has none, so the catalogue default holds");
        resolved.IsEnabled(ModuleIds.Commerce).Should().BeTrue();
        resolved.Enabled.Should().BeEquivalentTo(ModuleCatalog.All.Select(descriptor => descriptor.Id),
            "an existing tenant is unaffected by a re-apply");
        actions.Should().ContainSingle().Which.Should().Contain("additively").And.Contain("simi");
    }

    [Fact]
    public async Task ApplyModulesAsync_Should_EnableDeclaredModulesPlusCoreAndNothingElse_When_SimiPackIsApplied()
    {
        // Acceptance 13: a freshly provisioned tenant on the revised simi pack must not 403 on
        // personal-finance, groups, documents or voice (nor on the core ai / agents).
        await SeedTenantAsync("simi");
        await using var db = NewDb();
        var reader = NewReader(db);
        var applier = NewApplier(new ConfigPackSource(), db, moduleService: reader);

        await applier.ApplyModulesAsync(_tenantId, "simi", initialProvisioning: true);

        var rows = await RowsAsync();
        rows.Should().HaveCount(ModuleCatalog.All.Count);
        rows.Where(row => row.IsEnabled).Select(row => row.ModuleId).Should().BeEquivalentTo(SimiExpectedOn);
        rows.Where(row => !row.IsEnabled).Select(row => row.ModuleId).Should().BeEquivalentTo(SimiExpectedOff);
        rows.Should().OnlyContain(row => row.Reason == "pack:simi@v1" && row.Source == TenantModuleSource.Pack);

        var resolved = await reader.GetAsync(_tenantId);
        resolved.Enabled.Should().BeEquivalentTo(SimiExpectedOn);
        foreach (var id in new[] { ModuleIds.PersonalFinance, ModuleIds.Groups, ModuleIds.Documents, ModuleIds.Voice, ModuleIds.Ai, ModuleIds.Agents })
        {
            resolved.IsEnabled(id).Should().BeTrue($"a simi tenant's request to a {id} endpoint must not be gated");
        }
        resolved.IsEnabled(ModuleIds.Finance).Should().BeFalse();
    }

    [Fact]
    public async Task ApplyModulesAsync_Should_FlipPackRowOn_AndNeverOff_When_NewerPackVersionIsApplied()
    {
        await SeedTenantAsync("test-pack");
        var source = new FakeConfigPackSource();
        source.Set(new ConfigPackManifest { BusinessType = "test-pack", Version = 1, Modules = [ModuleIds.Commerce] });

        await using (var first = NewDb())
        {
            await NewApplier(source, first).ApplyModulesAsync(_tenantId, "test-pack", initialProvisioning: true);
        }

        var afterV1 = await RowsAsync();
        afterV1.Single(row => row.ModuleId == ModuleIds.Documents).IsEnabled.Should().BeFalse();
        afterV1.Single(row => row.ModuleId == ModuleIds.Commerce).IsEnabled.Should().BeTrue();

        // v2 declares documents and DROPS commerce: documents flips on, commerce must stay on.
        source.Set(new ConfigPackManifest { BusinessType = "test-pack", Version = 2, Modules = [ModuleIds.Documents] });
        await using (var second = NewDb())
        {
            await NewApplier(source, second).ApplyModulesAsync(_tenantId, "test-pack", initialProvisioning: false);
        }

        var afterV2 = await RowsAsync();
        afterV2.Should().HaveCount(ModuleCatalog.All.Count, "re-apply never duplicates rows");
        var documents = afterV2.Single(row => row.ModuleId == ModuleIds.Documents);
        documents.IsEnabled.Should().BeTrue("a newly declared module is enabled on re-apply");
        documents.Reason.Should().Be("pack:test-pack@v2");
        documents.Source.Should().Be(TenantModuleSource.Pack);
        afterV2.Single(row => row.ModuleId == ModuleIds.Commerce).IsEnabled.Should().BeTrue("a pack re-apply never disables anything");
        afterV2.Single(row => row.ModuleId == ModuleIds.Finance).IsEnabled.Should().BeTrue();
        afterV2.Single(row => row.ModuleId == ModuleIds.Groups).IsEnabled.Should().BeFalse("still undeclared");
    }

    [Fact]
    public async Task ApplyModulesAsync_Should_Throw_When_ManifestDeclaresUnknownModule()
    {
        await SeedTenantAsync("bad-pack");
        var source = new FakeConfigPackSource();
        source.Set(new ConfigPackManifest { BusinessType = "bad-pack", Version = 1, Modules = ["Commerce"] });
        await using var db = NewDb();
        var applier = NewApplier(source, db);

        var act = () => applier.ApplyModulesAsync(_tenantId, "bad-pack", initialProvisioning: true);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*bad-pack*Commerce*");
        (await RowsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyModulesAsync_Should_InvalidateReaderCache_When_RowsAreWritten()
    {
        await SeedTenantAsync("food-commerce");
        var cache = new FusionCache(new FusionCacheOptions());
        await using var readerDb = NewDb();
        var moduleService = new TenantModuleService(
            readerDb,
            cache,
            NullLogger<TenantModuleService>.Instance,
            Mock.Of<IClock>(),
            new TestCurrentUserProvider(),
            Mock.Of<ICorrelationContext>(),
            Mock.Of<IAuditLogWriter>(),
            Mock.Of<IEventBus>(),
            Mock.Of<ITenantContext>(),
            Mock.Of<IPermissionService>());

        var before = await moduleService.GetAsync(_tenantId);
        before.IsEnabled(ModuleIds.Documents).Should().BeTrue("no rows yet, so the catalogue default applies");

        await using var db = NewDb();
        await NewApplier(new ConfigPackSource(), db, moduleService: moduleService).ApplyModulesAsync(_tenantId, "food-commerce", initialProvisioning: true);

        var after = await moduleService.GetAsync(_tenantId);
        after.IsEnabled(ModuleIds.Documents).Should().BeFalse("the applier invalidated the cache and memo, so the pack's rows are visible");
        after.IsEnabled(ModuleIds.Commerce).Should().BeTrue();
        after.Enabled.Should().BeEquivalentTo(FoodCommerceExpectedOn);
    }

    [Fact]
    public async Task ApplyModulesAsync_Should_RestoreAmbientTenantContext_When_Done()
    {
        await SeedTenantAsync("food-commerce");
        var ambient = Guid.NewGuid();
        var tenantContext = new FakeTenantContext { TenantId = ambient, ResolutionSource = "Test" };
        await using var db = NewDb();
        var applier = NewApplier(new ConfigPackSource(), db, tenantContext);

        await applier.ApplyModulesAsync(_tenantId, "food-commerce", initialProvisioning: true);

        tenantContext.TenantId.Should().Be(ambient);
        tenantContext.ResolutionSource.Should().Be("Test");
    }

    // ── helpers ─────────────────────────────────────────────────────────

    private sealed class FakeConfigPackSource : IConfigPackSource
    {
        private readonly Dictionary<string, ConfigPackManifest> _packs = new(StringComparer.OrdinalIgnoreCase);

        public void Set(ConfigPackManifest manifest) => _packs[manifest.BusinessType] = manifest;

        public ConfigPackManifest? Get(string businessType)
            => _packs.TryGetValue(businessType, out var manifest) ? manifest : null;

        public IReadOnlyList<string> ListBusinessTypes() => _packs.Keys.ToList();
    }

    private sealed class FakeTenantContext : ITenantContext
    {
        public Guid? TenantId { get; set; }
        public string? ResolutionSource { get; set; }
        public bool IsResolved => TenantId.HasValue;
    }
}
