using FluentAssertions;
using Microsoft.EntityFrameworkCore;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.SharedKernel.Abstractions;
using Aonik.Finance.Contracts.Services.Pricing;
using Aonik.Finance.Services.Pricing;
using Aonik.Platform.Services.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Entities.ReferenceData;
using Aonik.Infrastructure.Multitenancy;
using Aonik.Platform.Persistence;

namespace Aonik.Application.Tests.Identity;

public class TenantServiceTests
{
    private sealed class TestTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;

        public TestTenantProvider(Guid tenantId) => _tenantId = tenantId;

        public Guid GetCurrentTenantId() => _tenantId;

        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = _tenantId;
            return true;
        }
    }

    private sealed class AllowAllPermissionService : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permissionKey, CancellationToken ct = default) =>
            Task.FromResult(true);

        public Task<List<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(new List<string>());
    }

    private sealed class TestCurrentUserProvider : ICurrentUserProvider
    {
        private readonly Guid _userId;

        public TestCurrentUserProvider(Guid userId) => _userId = userId;

        public Guid? GetCurrentUserId() => _userId;

        public bool TryGetCurrentUserId(out Guid userId)
        {
            userId = _userId;
            return true;
        }
    }

    private sealed class TestCorrelationContext : ICorrelationContext
    {
        public string? CorrelationId => "corr-tenant-tests";
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

    private sealed class TestTenantProvisioner : ITenantProvisioner
    {
        private readonly PlatformDbContext _dbContext;
        private readonly IClock _clock;
        private readonly Guid _userId;

        public TestTenantProvisioner(PlatformDbContext dbContext, IClock clock, Guid userId)
        {
            _dbContext = dbContext;
            _clock = clock;
            _userId = userId;
        }

        public async Task<ProvisionTenantResult> ProvisionTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            // Plant a TenantAdmin role for the new tenant so
            // TenantService.CreateTenantAsync can assign it to the
            // initial owner (the real TenantProvisioner does this
            // alongside permission seeding; the in-memory test
            // doesn't need permissions, just the role row).
            _dbContext.Roles.Add(new Aonik.Platform.Entities.Identity.Role
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "TenantAdmin",
                CreatedAt = _clock.UtcNow,
                CreatedBy = _userId,
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new ProvisionTenantResult(true, 0, 1, 0, new List<string>());
        }

        public Task<TenantHealthResult> CheckTenantHealthAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TenantHealthResult(true, true, true, true, new List<string>()));
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTime now) => UtcNow = now;
        public DateTime UtcNow { get; }
    }

    [Fact]
    public async Task CreateTenantAsync_ShouldAcceptNGN_WhenCurrencyConfiguredAndKnown()
    {
        // Arrange
        var tenantContext = new TestTenantContext();
        var userId = Guid.NewGuid();
        var clock = new FixedClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(databaseName: $"TenantServiceTestDb_{Guid.NewGuid()}")
            .Options;

        using var dbContext = new PlatformDbContext(
            options,
            new HttpContextTenantProvider(tenantContext),
            new TestCurrentUserProvider(userId),
            clock);
        dbContext.Countries.Add(new Country
        {
            Id = Guid.NewGuid(),
            TenantId = null,
            IsoAlpha2 = "NG",
            IsoAlpha3 = "NGA",
            IsoNumeric = 566,
            Name = "Nigeria",
            SortOrder = 1,
            IsActive = true
        });
        dbContext.Currencies.Add(new Currency
        {
            Id = Guid.NewGuid(),
            TenantId = null,
            Code = "NGN",
            Name = "Naira",
            NumericCode = "566",
            MinorUnit = 2,
            WithdrawalDate = null,
            SortOrder = 1,
            IsActive = true
        });
        await dbContext.SaveChangesAsync();

        var auditLogWriter = new TestAuditLogWriter();
        var correlationContext = new TestCorrelationContext();
        var currentUserProvider = new TestCurrentUserProvider(userId);
        var service = new TenantService(
            dbContext,
            new TestTenantProvisioner(dbContext, clock, userId),
            auditLogWriter,
            clock,
            currentUserProvider,
            correlationContext,
            tenantContext,
            new AllowAllPermissionService(),
            new CurrencyMetadataProvider(),
            new PendingTenantUserProvisioner(dbContext, clock, currentUserProvider, auditLogWriter, correlationContext));

        // Act
        var response = await service.CreateTenantAsync(
            new Aonik.Platform.Contracts.Models.Identity.CreateTenantRequest(
                Name: "Tenant NG",
                Environment: "Dev",
                DefaultCurrency: "ngn",
                SupportedCountries: ["ng"],
                OwnerEmail: "owner@tenant-ng.test"),
            CancellationToken.None);

        // Assert
        response.DefaultCurrency.Should().Be("NGN");
        response.SupportedCountries.Should().BeEquivalentTo(new[] { "NG" });
        response.AllowedOriginCountries.Should().BeEquivalentTo(new[] { "NG" });
        response.AllowedDestinationCountries.Should().BeEquivalentTo(new[] { "NG" });
        response.SupportedCurrencies.Should().BeEquivalentTo(new[] { "NGN" });
    }

    [Fact]
    public async Task UpdateTenantAsync_ShouldRejectAllowedOriginCountriesOutsideSupportedCountries()
    {
        // Arrange
        var tenantContext = new TestTenantContext();
        var userId = Guid.NewGuid();
        var clock = new FixedClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(databaseName: $"TenantServiceTestDb_{Guid.NewGuid()}")
            .Options;

        using var dbContext = new PlatformDbContext(
            options,
            new HttpContextTenantProvider(tenantContext),
            new TestCurrentUserProvider(userId),
            clock);
        dbContext.Countries.AddRange(
            new Country
            {
                Id = Guid.NewGuid(),
                TenantId = null,
                IsoAlpha2 = "US",
                IsoAlpha3 = "USA",
                IsoNumeric = 840,
                Name = "United States",
                SortOrder = 1,
                IsActive = true
            },
            new Country
            {
                Id = Guid.NewGuid(),
                TenantId = null,
                IsoAlpha2 = "NG",
                IsoAlpha3 = "NGA",
                IsoNumeric = 566,
                Name = "Nigeria",
                SortOrder = 2,
                IsActive = true
            });
        dbContext.Currencies.Add(new Currency
        {
            Id = Guid.NewGuid(),
            TenantId = null,
            Code = "USD",
            Name = "US Dollar",
            NumericCode = "840",
            MinorUnit = 2,
            WithdrawalDate = null,
            SortOrder = 1,
            IsActive = true
        });
        await dbContext.SaveChangesAsync();

        var auditLogWriter = new TestAuditLogWriter();
        var correlationContext = new TestCorrelationContext();
        var currentUserProvider = new TestCurrentUserProvider(userId);
        var service = new TenantService(
            dbContext,
            new TestTenantProvisioner(dbContext, clock, userId),
            auditLogWriter,
            clock,
            currentUserProvider,
            correlationContext,
            tenantContext,
            new AllowAllPermissionService(),
            new CurrencyMetadataProvider(),
            new PendingTenantUserProvisioner(dbContext, clock, currentUserProvider, auditLogWriter, correlationContext));

        var tenant = await service.CreateTenantAsync(
            new CreateTenantRequest(
                Name: "Tenant Update Validation",
                Environment: "Dev",
                DefaultCurrency: "USD",
                SupportedCountries: ["US"],
                OwnerEmail: "owner@tenant-update.test"),
            CancellationToken.None);

        // Act
        var act = async () =>
            await service.UpdateTenantAsync(
                tenant.TenantId,
                new UpdateTenantRequest(AllowedOriginCountries: ["NG"]),
                CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<ArgumentException>();
        ex.Which.Message.Should().Contain("subset of supported countries");
    }

    [Fact]
    public async Task UpdateTenantAsync_ShouldTrimAllowedCountryLists_WhenSupportedCountriesShrink()
    {
        // Arrange
        var tenantContext = new TestTenantContext();
        var userId = Guid.NewGuid();
        var clock = new FixedClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(databaseName: $"TenantServiceTestDb_{Guid.NewGuid()}")
            .Options;

        using var dbContext = new PlatformDbContext(
            options,
            new HttpContextTenantProvider(tenantContext),
            new TestCurrentUserProvider(userId),
            clock);
        dbContext.Countries.AddRange(
            new Country
            {
                Id = Guid.NewGuid(),
                TenantId = null,
                IsoAlpha2 = "US",
                IsoAlpha3 = "USA",
                IsoNumeric = 840,
                Name = "United States",
                SortOrder = 1,
                IsActive = true
            },
            new Country
            {
                Id = Guid.NewGuid(),
                TenantId = null,
                IsoAlpha2 = "NG",
                IsoAlpha3 = "NGA",
                IsoNumeric = 566,
                Name = "Nigeria",
                SortOrder = 2,
                IsActive = true
            });
        dbContext.Currencies.Add(new Currency
        {
            Id = Guid.NewGuid(),
            TenantId = null,
            Code = "USD",
            Name = "US Dollar",
            NumericCode = "840",
            MinorUnit = 2,
            WithdrawalDate = null,
            SortOrder = 1,
            IsActive = true
        });
        await dbContext.SaveChangesAsync();

        var auditLogWriter = new TestAuditLogWriter();
        var correlationContext = new TestCorrelationContext();
        var currentUserProvider = new TestCurrentUserProvider(userId);
        var service = new TenantService(
            dbContext,
            new TestTenantProvisioner(dbContext, clock, userId),
            auditLogWriter,
            clock,
            currentUserProvider,
            correlationContext,
            tenantContext,
            new AllowAllPermissionService(),
            new CurrencyMetadataProvider(),
            new PendingTenantUserProvisioner(dbContext, clock, currentUserProvider, auditLogWriter, correlationContext));

        var tenant = await service.CreateTenantAsync(
            new CreateTenantRequest(
                Name: "Tenant Coverage Trim",
                Environment: "Dev",
                DefaultCurrency: "USD",
                SupportedCountries: ["US", "NG"],
                OwnerEmail: "owner@tenant-trim.test",
                AllowedOriginCountries: ["US", "NG"],
                AllowedDestinationCountries: ["NG"]),
            CancellationToken.None);

        // Act
        var updated = await service.UpdateTenantAsync(
            tenant.TenantId,
            new UpdateTenantRequest(SupportedCountries: ["US"]),
            CancellationToken.None);

        // Assert
        updated.SupportedCountries.Should().BeEquivalentTo(new[] { "US" });
        updated.AllowedOriginCountries.Should().BeEquivalentTo(new[] { "US" });
        updated.AllowedDestinationCountries.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateTenantAsync_ShouldRejectUnknownCountry_WhenNotInReferenceData()
    {
        // Arrange
        var tenantContext = new TestTenantContext();
        var userId = Guid.NewGuid();
        var clock = new FixedClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(databaseName: $"TenantServiceTestDb_{Guid.NewGuid()}")
            .Options;

        using var dbContext = new PlatformDbContext(
            options,
            new HttpContextTenantProvider(tenantContext),
            new TestCurrentUserProvider(userId),
            clock);
        dbContext.Currencies.Add(new Currency
        {
            Id = Guid.NewGuid(),
            TenantId = null,
            Code = "USD",
            Name = "US Dollar",
            NumericCode = "840",
            MinorUnit = 2,
            WithdrawalDate = null,
            SortOrder = 1,
            IsActive = true
        });
        await dbContext.SaveChangesAsync();

        var auditLogWriter = new TestAuditLogWriter();
        var correlationContext = new TestCorrelationContext();
        var currentUserProvider = new TestCurrentUserProvider(userId);
        var service = new TenantService(
            dbContext,
            new TestTenantProvisioner(dbContext, clock, userId),
            auditLogWriter,
            clock,
            currentUserProvider,
            correlationContext,
            tenantContext,
            new AllowAllPermissionService(),
            new CurrencyMetadataProvider(),
            new PendingTenantUserProvisioner(dbContext, clock, currentUserProvider, auditLogWriter, correlationContext));

        // Act
        var act = async () =>
            await service.CreateTenantAsync(
                new Aonik.Platform.Contracts.Models.Identity.CreateTenantRequest(
                    Name: "Tenant Bad Country",
                    Environment: "Dev",
                    DefaultCurrency: "USD",
                    SupportedCountries: ["ZZ"],
                    OwnerEmail: "owner@bad-country.test"),
                CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<ArgumentException>();
        ex.Which.Message.Should().Contain("Unsupported country codes");
    }

    [Fact]
    public async Task CreateTenantAsync_ShouldRejectCurrency_WhenNotInReferenceData()
    {
        // Arrange
        var tenantContext = new TestTenantContext();
        var userId = Guid.NewGuid();
        var clock = new FixedClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(databaseName: $"TenantServiceTestDb_{Guid.NewGuid()}")
            .Options;

        using var dbContext = new PlatformDbContext(
            options,
            new HttpContextTenantProvider(tenantContext),
            new TestCurrentUserProvider(userId),
            clock);
        dbContext.Countries.Add(new Country
        {
            Id = Guid.NewGuid(),
            TenantId = null,
            IsoAlpha2 = "US",
            IsoAlpha3 = "USA",
            IsoNumeric = 840,
            Name = "United States",
            SortOrder = 1,
            IsActive = true
        });
        await dbContext.SaveChangesAsync();

        var auditLogWriter = new TestAuditLogWriter();
        var correlationContext = new TestCorrelationContext();
        var currentUserProvider = new TestCurrentUserProvider(userId);
        var service = new TenantService(
            dbContext,
            new TestTenantProvisioner(dbContext, clock, userId),
            auditLogWriter,
            clock,
            currentUserProvider,
            correlationContext,
            tenantContext,
            new AllowAllPermissionService(),
            new CurrencyMetadataProvider(),
            new PendingTenantUserProvisioner(dbContext, clock, currentUserProvider, auditLogWriter, correlationContext));

        // Act
        var act = async () =>
            await service.CreateTenantAsync(
                new Aonik.Platform.Contracts.Models.Identity.CreateTenantRequest(
                    Name: "Tenant No Currency Ref",
                    Environment: "Dev",
                    DefaultCurrency: "USD",
                    SupportedCountries: ["US"],
                    OwnerEmail: "owner@no-currency.test"),
                CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<ArgumentException>();
        ex.Which.Message.Should().Contain("Currency is not configured in currencies");
    }

    [Fact]
    public async Task CreateTenantAsync_ShouldRejectMissingOwnerEmail()
    {
        // Acceptance criterion: creating a tenant requires an owner
        // email — empty string must be rejected before any DB write.
        var tenantContext = new TestTenantContext();
        var userId = Guid.NewGuid();
        var clock = new FixedClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(databaseName: $"TenantServiceTestDb_{Guid.NewGuid()}")
            .Options;

        using var dbContext = new PlatformDbContext(
            options,
            new HttpContextTenantProvider(tenantContext),
            new TestCurrentUserProvider(userId),
            clock);
        SeedReferenceData(dbContext);
        await dbContext.SaveChangesAsync();

        var auditLogWriter = new TestAuditLogWriter();
        var correlationContext = new TestCorrelationContext();
        var currentUserProvider = new TestCurrentUserProvider(userId);
        var service = new TenantService(
            dbContext,
            new TestTenantProvisioner(dbContext, clock, userId),
            auditLogWriter,
            clock,
            currentUserProvider,
            correlationContext,
            tenantContext,
            new AllowAllPermissionService(),
            new CurrencyMetadataProvider(),
            new PendingTenantUserProvisioner(dbContext, clock, currentUserProvider, auditLogWriter, correlationContext));

        var act = async () => await service.CreateTenantAsync(
            new CreateTenantRequest(
                Name: "Owner Required",
                Environment: "Dev",
                DefaultCurrency: "USD",
                SupportedCountries: ["US"],
                OwnerEmail: ""),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ArgumentException>();
        ex.Which.Message.Should().Contain("Owner email is required");
        // DB stays clean: no half-provisioned tenant or roles linger.
        dbContext.Tenants.Should().BeEmpty();
        dbContext.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateTenantAsync_ShouldProvisionPendingOwnerAndAssignTenantAdmin()
    {
        // Pins acceptance criteria 2 + 4: tenant creation creates one
        // pending owner user in the new tenant AND that owner has
        // TenantAdmin (and NOT PlatformAdmin — that's reserved for
        // host bootstrap).
        var tenantContext = new TestTenantContext();
        var userId = Guid.NewGuid();
        var clock = new FixedClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(databaseName: $"TenantServiceTestDb_{Guid.NewGuid()}")
            .Options;

        using var dbContext = new PlatformDbContext(
            options,
            new HttpContextTenantProvider(tenantContext),
            new TestCurrentUserProvider(userId),
            clock);
        SeedReferenceData(dbContext);
        await dbContext.SaveChangesAsync();

        var auditLogWriter = new TestAuditLogWriter();
        var correlationContext = new TestCorrelationContext();
        var currentUserProvider = new TestCurrentUserProvider(userId);
        var service = new TenantService(
            dbContext,
            new TestTenantProvisioner(dbContext, clock, userId),
            auditLogWriter,
            clock,
            currentUserProvider,
            correlationContext,
            tenantContext,
            new AllowAllPermissionService(),
            new CurrencyMetadataProvider(),
            new PendingTenantUserProvisioner(dbContext, clock, currentUserProvider, auditLogWriter, correlationContext));

        var response = await service.CreateTenantAsync(
            new CreateTenantRequest(
                Name: "Owner Provisioned Tenant",
                Environment: "Dev",
                DefaultCurrency: "USD",
                SupportedCountries: ["US"],
                OwnerEmail: "Owner@Provisioned.Test",
                OwnerDisplayName: "Customer Owner"),
            CancellationToken.None);

        // Pending owner row exists with the bootstrap issuer marker.
        var ownerUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.TenantId == response.TenantId);
        ownerUser.Should().NotBeNull();
        ownerUser!.Email.Should().Be("Owner@Provisioned.Test");
        ownerUser.ExternalIssuer.Should()
            .Be(BootstrapIdentityConstants.PendingOwnerIssuer);
        ownerUser.ExternalSubject.Should()
            .StartWith("owner:");

        // Party + UserParty + PersonProfile chain exists with the
        // owner's display name.
        var party = await dbContext.Parties
            .FirstOrDefaultAsync(p => p.TenantId == response.TenantId);
        party.Should().NotBeNull();
        party!.DisplayName.Should().Be("Customer Owner");

        var userParty = await dbContext.UserParties
            .FirstOrDefaultAsync(up => up.UserId == ownerUser.Id);
        userParty.Should().NotBeNull();

        // TenantAdmin assigned to the placeholder; PlatformAdmin not.
        var assignedRoleNames = await (
            from ur in dbContext.UserRoles
            join r in dbContext.Roles on ur.RoleId equals r.Id
            where ur.UserId == ownerUser.Id
            select r.Name).ToListAsync();
        assignedRoleNames.Should().Contain("TenantAdmin");
        assignedRoleNames.Should().NotContain("PlatformAdmin");
    }

    private static void SeedReferenceData(PlatformDbContext dbContext)
    {
        dbContext.Countries.Add(new Country
        {
            Id = Guid.NewGuid(),
            TenantId = null,
            IsoAlpha2 = "US",
            IsoAlpha3 = "USA",
            IsoNumeric = 840,
            Name = "United States",
            SortOrder = 1,
            IsActive = true
        });
        dbContext.Currencies.Add(new Currency
        {
            Id = Guid.NewGuid(),
            TenantId = null,
            Code = "USD",
            Name = "US Dollar",
            NumericCode = "840",
            MinorUnit = 2,
            WithdrawalDate = null,
            SortOrder = 1,
            IsActive = true
        });
    }
}
