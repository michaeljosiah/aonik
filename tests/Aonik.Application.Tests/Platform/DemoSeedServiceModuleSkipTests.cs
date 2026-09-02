using Aonik.Finance.Persistence;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Seeding;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.SharedKernel.Abstractions.PersonalFinance;
using Aonik.SharedKernel.Modules;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

namespace Aonik.Application.Tests.Platform;

/// <summary>
/// Spec 097 §12.4: the demo seed skips every <see cref="IDemoSeedContributor"/> whose module resolved off
/// for the tenant, in every phase, so a demo toggle never seeds sample rows for a module the tenant does
/// not have. Core modules and names the catalogue does not know always run.
/// </summary>
public class DemoSeedServiceModuleSkipTests
{
    private static readonly Guid TenantId = Guid.Parse("2d53eab0-f4fa-4ac6-8400-6fe21308e897");
    private static readonly Guid UserId = Guid.Parse("9fd75699-884d-434a-906e-c7c4bc66c397");

    [Fact]
    public async Task SeedAsync_Should_SkipContributor_When_ItsModuleIsDisabled()
    {
        var commerce = new RecordingContributor(ModuleIds.Commerce);
        var finance = new RecordingContributor(ModuleIds.Finance);
        var agents = new RecordingContributor(ModuleIds.Agents);
        var legacy = new RecordingContributor("LegacyName");
        var reader = new FakeModuleEnablementReader(ModuleCatalog.All.Select(d => d.Id).Except([ModuleIds.Commerce]));
        var service = await CreateServiceAsync([commerce, finance, agents, legacy], reader);

        var result = await service.SeedAsync(TenantId);

        commerce.Phases.Should().BeEmpty("commerce resolved off for this tenant, so no phase reaches it");
        finance.Phases.Should().NotBeEmpty("finance resolved on");
        agents.Phases.Should().NotBeEmpty("agents is core and can never be off");
        legacy.Phases.Should().NotBeEmpty("a name the catalogue does not know runs as before Spec 097");
        finance.Phases.Should().Contain(DemoSeedPhase.Activity);
        result.Operations.Should().Contain("Skipped commerce demo seed: module disabled for tenant");
        result.Operations.Should().NotContain(operation => operation.StartsWith("Skipped finance", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SeedAsync_Should_RunEveryContributor_When_NoModuleReaderIsAvailable()
    {
        var commerce = new RecordingContributor(ModuleIds.Commerce);
        var service = await CreateServiceAsync([commerce], moduleReader: null);

        var result = await service.SeedAsync(TenantId);

        commerce.Phases.Should().NotBeEmpty("without a reader the legacy behaviour (every contributor runs) applies");
        result.Operations.Should().NotContain(operation => operation.StartsWith("Skipped", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(ModuleIds.Commerce, false, true)]
    [InlineData(ModuleIds.Commerce, true, false)]
    [InlineData(ModuleIds.Agents, false, false)]
    [InlineData("LegacyName", false, false)]
    public void IsModuleDisabled_Should_SkipOnlyKnownNonCoreDisabledModules(string moduleName, bool enabledInSet, bool expectedSkip)
    {
        var enabled = ModuleCatalog.All.Select(d => d.Id).Where(id => enabledInSet || id != moduleName);
        var set = new ModuleEnablementSet(TenantId, enabled.ToHashSet(StringComparer.Ordinal));

        DemoSeedService.IsModuleDisabled(moduleName, set).Should().Be(expectedSkip);
    }

    // ── fixture ─────────────────────────────────────────────────────────

    private static async Task<DemoSeedService> CreateServiceAsync(IDemoSeedContributor[] contributors, IModuleEnablementReader? moduleReader)
    {
        var databaseName = $"TestDb_{Guid.NewGuid()}";
        var tenantProvider = new TestTenantProvider(TenantId);
        var currentUser = new TestCurrentUserProvider(UserId);

        var platformDb = new PlatformDbContext(
            new DbContextOptionsBuilder<PlatformDbContext>().UseInMemoryDatabase(databaseName).Options,
            tenantProvider,
            currentUser);
        var financeDb = new FinanceDbContext(
            new DbContextOptionsBuilder<FinanceDbContext>().UseInMemoryDatabase(databaseName).Options,
            tenantProvider);

        platformDb.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Name = "Module Skip Tenant",
            Environment = "Development",
            DefaultCurrency = "GBP",
            SupportedCountriesJson = "[]",
            AllowedOriginCountriesJson = "[]",
            AllowedDestinationCountriesJson = "[]",
            Status = TenantStatus.Active,
        });
        platformDb.Users.Add(new User
        {
            Id = UserId,
            TenantId = TenantId,
            ExternalIssuer = "tests",
            ExternalSubject = $"module-skip-user-{UserId:N}",
            Email = "module-skip-tests@aonik.local",
            Status = "Active",
            PreferencesJson = "{}",
        });
        await platformDb.SaveChangesAsync();

        var permissionService = new Mock<IPermissionService>();
        permissionService
            .Setup(x => x.HasPermissionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var correlation = new Mock<ICorrelationContext>();
        correlation.SetupGet(x => x.CorrelationId).Returns("corr-097-demo");

        var clock = new Mock<IClock>();
        clock.SetupGet(x => x.UtcNow).Returns(new DateTime(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc));

        return new DemoSeedService(
            platformDb,
            contributors,
            clock.Object,
            NullLoggerFactory.Instance,
            new Mock<IAuditLogWriter>().Object,
            currentUser,
            correlation.Object,
            permissionService.Object,
            new FakeTenantContext(),
            financeDb,
            new Mock<IPersonalFinanceDemoDataReverser>().Object,
            new Mock<IAgentDemoCleanup>().Object,
            moduleReader);
    }

    private sealed class RecordingContributor(string moduleName) : IDemoSeedContributor
    {
        public string ModuleName => moduleName;
        public List<DemoSeedPhase> Phases { get; } = [];

        public Task<IReadOnlyList<string>> SeedAsync(DemoSeedPhase phase, DemoSeedContext context, CancellationToken cancellationToken = default)
        {
            Phases.Add(phase);
            return Task.FromResult<IReadOnlyList<string>>([$"{moduleName}:{phase}"]);
        }

        public void ClearTracking()
        {
        }

        public IReadOnlyDictionary<string, object> GetResults() => new Dictionary<string, object>();
    }

    private sealed class FakeModuleEnablementReader(IEnumerable<string> enabled) : IModuleEnablementReader
    {
        private readonly HashSet<string> _enabled = enabled.ToHashSet(StringComparer.Ordinal);

        public Task<ModuleEnablementSet> GetAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(new ModuleEnablementSet(tenantId, _enabled));

        public Task<IReadOnlyList<Guid>> FilterEnabledTenantsAsync(IEnumerable<Guid> tenantIds, string moduleId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Guid>>(_enabled.Contains(moduleId) ? tenantIds.Distinct().ToList() : []);
    }

    private sealed class FakeTenantContext : ITenantContext
    {
        public Guid? TenantId { get; set; }
        public string? ResolutionSource { get; set; }
        public bool IsResolved => TenantId.HasValue;
    }
}
