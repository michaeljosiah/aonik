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
        public Task<ProvisionTenantResult> ProvisionTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProvisionTenantResult(false, 0, 0, 0, new List<string>()));

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

        var service = new TenantService(
            dbContext,
            new TestTenantProvisioner(),
            new TestAuditLogWriter(),
            clock,
            new TestCurrentUserProvider(userId),
            new TestCorrelationContext(),
            tenantContext,
            new AllowAllPermissionService(),
            new CurrencyMetadataProvider());

        // Act
        var response = await service.CreateTenantAsync(
            new Aonik.Platform.Contracts.Models.Identity.CreateTenantRequest(
                Name: "Tenant NG",
                Environment: "Dev",
                DefaultCurrency: "ngn",
                SupportedCountries: ["ng"]),
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

        var service = new TenantService(
            dbContext,
            new TestTenantProvisioner(),
            new TestAuditLogWriter(),
            clock,
            new TestCurrentUserProvider(userId),
            new TestCorrelationContext(),
            tenantContext,
            new AllowAllPermissionService(),
            new CurrencyMetadataProvider());

        var tenant = await service.CreateTenantAsync(
            new CreateTenantRequest(
                Name: "Tenant Update Validation",
                Environment: "Dev",
                DefaultCurrency: "USD",
                SupportedCountries: ["US"]),
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

        var service = new TenantService(
            dbContext,
            new TestTenantProvisioner(),
            new TestAuditLogWriter(),
            clock,
            new TestCurrentUserProvider(userId),
            new TestCorrelationContext(),
            tenantContext,
            new AllowAllPermissionService(),
            new CurrencyMetadataProvider());

        var tenant = await service.CreateTenantAsync(
            new CreateTenantRequest(
                Name: "Tenant Coverage Trim",
                Environment: "Dev",
                DefaultCurrency: "USD",
                SupportedCountries: ["US", "NG"],
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

        var service = new TenantService(
            dbContext,
            new TestTenantProvisioner(),
            new TestAuditLogWriter(),
            clock,
            new TestCurrentUserProvider(userId),
            new TestCorrelationContext(),
            tenantContext,
            new AllowAllPermissionService(),
            new CurrencyMetadataProvider());

        // Act
        var act = async () =>
            await service.CreateTenantAsync(
                new Aonik.Platform.Contracts.Models.Identity.CreateTenantRequest(
                    Name: "Tenant Bad Country",
                    Environment: "Dev",
                    DefaultCurrency: "USD",
                    SupportedCountries: ["ZZ"]),
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

        var service = new TenantService(
            dbContext,
            new TestTenantProvisioner(),
            new TestAuditLogWriter(),
            clock,
            new TestCurrentUserProvider(userId),
            new TestCorrelationContext(),
            tenantContext,
            new AllowAllPermissionService(),
            new CurrencyMetadataProvider());

        // Act
        var act = async () =>
            await service.CreateTenantAsync(
                new Aonik.Platform.Contracts.Models.Identity.CreateTenantRequest(
                    Name: "Tenant No Currency Ref",
                    Environment: "Dev",
                    DefaultCurrency: "USD",
                    SupportedCountries: ["US"]),
                CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<ArgumentException>();
        ex.Which.Message.Should().Contain("Currency is not configured in currencies");
    }
}
