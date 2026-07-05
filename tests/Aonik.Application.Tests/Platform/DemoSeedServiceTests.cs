using Aonik.Agents.Persistence;
using Aonik.Agents.Services;
using Aonik.Agents.Services.Seeding;
using LedgerEntity = Aonik.Finance.Entities.Ledger.Ledger;
using Aonik.Finance.Entities.Ledger;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Seeding;
using Aonik.PersonalFinance.Persistence;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Seeding;
using Aonik.Infrastructure.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Observability;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.Platform;

public class DemoSeedServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("1d53eab0-f4fa-4ac6-8400-6fe21308e8fd");
    private static readonly Guid UserId = Guid.Parse("8fd75699-884d-434a-906e-c7c4bc66c352");

    // The fixture uses `(localdb)\MSSQLLocalDB` — LocalDB is Windows-only,
    // so these tests can't run on the Linux CI runners. They're parked
    // until either (a) converted to InMemory (lossy on relational
    // semantics this seed exercises) or (b) moved into a separate
    // integration test project that the CI pipeline targets via Docker
    // SQL Server. Run manually on Windows by removing the Skip.
    private const string SkipReason = "Requires LocalDB (Windows-only). Convert to InMemory or move to an Integration test suite.";

    [Fact(Skip = SkipReason)]
    public async Task SeedThenReverseAsync_Should_RemoveBillCollectionDemoData()
    {
        await using var fixture = await CreateFixtureAsync();

        var seedResult = await fixture.Service.SeedAsync(TenantId, cancellationToken: default);
        var reverseResult = await fixture.Service.ReverseAsync(TenantId, default);

        seedResult.SeedType.Should().Be("BillCollection");
        reverseResult.SeedType.Should().Be("BillCollection");
        reverseResult.Operations.Should().NotBeEmpty();

        fixture.FinanceDb.Orders.Should().BeEmpty();
        fixture.FinanceDb.CatalogBillers.Should().BeEmpty();
        fixture.FinanceDb.CatalogBillerServices.Should().BeEmpty();
        fixture.FinanceDb.FxQuotes.Should().BeEmpty();
        fixture.FinanceDb.FeePolicies.Should().BeEmpty();
        fixture.FinanceDb.LimitsPolicies.Should().BeEmpty();
        fixture.PlatformDb.Notifications.Should().BeEmpty();
        fixture.PlatformDb.Parties.Where(x => x.TenantId == TenantId).Should().BeEmpty();
        fixture.PlatformDb.Settings.Where(x => x.TenantId == TenantId && x.Key.StartsWith("DemoSeed.")).Should().BeEmpty();
        fixture.AgentsDb.Agents.Where(x => x.TenantId == TenantId).Should().BeEmpty();
        fixture.AgentsDb.Workflows.Where(x => x.TenantId == TenantId).Should().BeEmpty();
    }

    [Fact(Skip = SkipReason)]
    public async Task SeedThenReverseAsync_Should_RemoveCrossBorderDemoData_AndRestoreTenantSnapshot()
    {
        await using var fixture = await CreateFixtureAsync();

        var tenant = await fixture.PlatformDb.Tenants.FirstAsync(x => x.Id == TenantId);
        tenant.Country = "NG";
        tenant.DefaultCurrency = "NGN";
        tenant.City = "Lagos";
        tenant.StateProvince = "Lagos";
        tenant.AddressLine1 = "Original HQ";
        tenant.SupportedCountriesJson = "[\"NG\"]";
        tenant.AllowedOriginCountriesJson = "[\"NG\"]";
        tenant.AllowedDestinationCountriesJson = "[\"NG\"]";
        await fixture.PlatformDb.SaveChangesAsync();

        var seedResult = await fixture.Service.SeedAsync(TenantId, "CrossBorderPayments", default);
        var reverseResult = await fixture.Service.ReverseAsync(TenantId, default);

        seedResult.SeedType.Should().Be("CrossBorderPayments");
        reverseResult.SeedType.Should().Be("CrossBorderPayments");

        var restoredTenant = await fixture.PlatformDb.Tenants.FirstAsync(x => x.Id == TenantId);
        restoredTenant.Country.Should().Be("NG");
        restoredTenant.DefaultCurrency.Should().Be("NGN");
        restoredTenant.City.Should().Be("Lagos");
        restoredTenant.StateProvince.Should().Be("Lagos");
        restoredTenant.AddressLine1.Should().Be("Original HQ");
        restoredTenant.SupportedCountriesJson.Should().Be("[\"NG\"]");
        restoredTenant.AllowedOriginCountriesJson.Should().Be("[\"NG\"]");
        restoredTenant.AllowedDestinationCountriesJson.Should().Be("[\"NG\"]");

        fixture.PlatformDb.TenantCountries.Where(x => x.TenantId == TenantId).Should().BeEmpty();
        fixture.PlatformDb.TenantCurrencies.Where(x => x.TenantId == TenantId).Should().BeEmpty();
        fixture.FinanceDb.Partners.Where(x => x.TenantId == TenantId).Should().BeEmpty();
        fixture.FinanceDb.PartnerBranches.Where(x => x.TenantId == TenantId).Should().BeEmpty();
        fixture.FinanceDb.Connectors.Where(x => x.TenantId == TenantId).Should().BeEmpty();
        fixture.FinanceDb.RoutingRules.Where(x => x.TenantId == TenantId).Should().BeEmpty();
        fixture.PersonalFinanceDb.Households.Where(x => x.TenantId == TenantId).Should().BeEmpty();
        fixture.PersonalFinanceDb.HouseholdMembers.Where(x => x.TenantId == TenantId).Should().BeEmpty();
        fixture.PlatformDb.Settings.Where(x => x.TenantId == TenantId && x.Key.StartsWith("DemoSeed.")).Should().BeEmpty();
    }

    private static async Task<TestFixture> CreateFixtureAsync()
    {
        var databaseName = $"DemoSeedServiceTests_{Guid.NewGuid():N}";
        var tenantProvider = new TestTenantProvider(TenantId);
        var connectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;";

        var platformOptions = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var financeOptions = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var personalFinanceOptions = new DbContextOptionsBuilder<PersonalFinanceDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var agentsOptions = new DbContextOptionsBuilder<AgentsDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var aonikOptions = new DbContextOptionsBuilder<AonikDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var aonikDb = new AonikDbContext(aonikOptions);
        var platformDb = new PlatformDbContext(platformOptions, tenantProvider);
        var financeDb = new FinanceDbContext(financeOptions, tenantProvider);
        var personalFinanceDb = new PersonalFinanceDbContext(personalFinanceOptions, tenantProvider);
        var agentsDb = new AgentsDbContext(agentsOptions, tenantProvider);

        await aonikDb.Database.EnsureDeletedAsync();
        await aonikDb.Database.MigrateAsync();

        await SeedTenantAsync(platformDb);
        await SeedUserAsync(platformDb);
        await SeedLedgerAsync(financeDb);

        var contributors = new IDemoSeedContributor[]
        {
            new FinanceDemoSeedContributor(financeDb, personalFinanceDb, NullLogger<FinanceDemoSeedContributor>.Instance),
            new AgentsDemoSeedContributor(agentsDb, NullLogger<AgentsDemoSeedContributor>.Instance),
            new PlatformDemoSeedContributor(platformDb, NullLogger<PlatformDemoSeedContributor>.Instance)
        };

        var service = new DemoSeedService(
            platformDb,
            contributors,
            new FixedClock(new DateTime(2026, 05, 04, 12, 0, 0, DateTimeKind.Utc)),
            NullLoggerFactory.Instance,
            new TestAuditLogWriter(),
            new TestCurrentUserProvider(UserId),
            new TestCorrelationContext(),
            new AllowAllPermissionService(),
            new TestTenantContext(),
            financeDb,
            personalFinanceDb,
            new AgentDemoCleanup(agentsDb));

        return new TestFixture(aonikDb, platformDb, financeDb, personalFinanceDb, agentsDb, service);
    }

    private static async Task SeedTenantAsync(PlatformDbContext dbContext)
    {
        dbContext.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Name = "Demo Seed Test Tenant",
            Environment = "Development",
            DefaultCurrency = "USD",
            SupportedCountriesJson = "[]",
            AllowedOriginCountriesJson = "[]",
            AllowedDestinationCountriesJson = "[]",
            Status = TenantStatus.Active
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedLedgerAsync(FinanceDbContext dbContext)
    {
        var ledgerId = Guid.NewGuid();
        dbContext.Ledgers.Add(new LedgerEntity
        {
            Id = ledgerId,
            TenantId = TenantId,
            BaseCurrency = "USD"
        });

        dbContext.LedgerAccounts.Add(new LedgerAccount
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            LedgerId = ledgerId,
            AccountType = "Asset",
            Name = "Cash",
            Code = "1000",
            DimensionsJson = "{}"
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedUserAsync(PlatformDbContext dbContext)
    {
        dbContext.Users.Add(new User
        {
            Id = UserId,
            TenantId = TenantId,
            ExternalIssuer = "tests",
            ExternalSubject = $"demo-seed-user-{UserId:N}",
            Email = "demo-seed-tests@aonik.local",
            Status = "Active",
            PreferencesJson = "{}"
        });

        await dbContext.SaveChangesAsync();
    }

    private sealed record TestFixture(
        AonikDbContext AonikDb,
        PlatformDbContext PlatformDb,
        FinanceDbContext FinanceDb,
        PersonalFinanceDbContext PersonalFinanceDb,
        AgentsDbContext AgentsDb,
        DemoSeedService Service) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await PlatformDb.DisposeAsync();
            await FinanceDb.DisposeAsync();
            await PersonalFinanceDb.DisposeAsync();
            await AgentsDb.DisposeAsync();
            await AonikDb.Database.EnsureDeletedAsync();
            await AonikDb.DisposeAsync();
        }
    }

    private sealed class TestTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid GetCurrentTenantId() => tenantId;

        public bool TryGetCurrentTenantId(out Guid currentTenantId)
        {
            currentTenantId = tenantId;
            return true;
        }
    }

    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime UtcNow => now;
    }

    private sealed class AllowAllPermissionService : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permissionKey, CancellationToken ct = default) => Task.FromResult(true);

        public Task<List<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(new List<string>());
    }

    private sealed class TestCurrentUserProvider(Guid userId) : ICurrentUserProvider
    {
        public Guid? GetCurrentUserId() => userId;

        public bool TryGetCurrentUserId(out Guid currentUserId)
        {
            currentUserId = userId;
            return true;
        }
    }

    private sealed class TestCorrelationContext : ICorrelationContext
    {
        public string? CorrelationId => "corr-demo-seed-tests";
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid? TenantId { get; set; }
        public string? ResolutionSource { get; set; }
        public bool IsResolved => TenantId.HasValue;
    }

    private sealed class TestAuditLogWriter : IAuditLogWriter
    {
        public Task LogAsync(
            string action,
            string resourceType,
            Guid resourceId,
            Guid tenantId,
            Guid? actorId,
            string? correlationId,
            string? detailsJson = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
