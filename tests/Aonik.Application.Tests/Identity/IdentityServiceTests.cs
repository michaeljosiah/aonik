using FluentAssertions;
using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Contracts.Models.Authentication;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Models.Settings;
using Aonik.Platform.Services.Identity;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Services.Settings;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Party;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Persistence;
using Aonik.Infrastructure.Time;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Tests.Identity;

public class IdentityServiceTests
{

    private sealed class AllowAllPermissionService : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permissionKey, CancellationToken ct = default) =>
            Task.FromResult(true);

        public Task<List<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(new List<string>());
    }
    private sealed class TestSettingProvider : ISettingProvider
    {
        private readonly Dictionary<string, string?> _settings;

        public TestSettingProvider(string provider)
        {
            _settings = new Dictionary<string, string?>
            {
                [AuthSettingNames.Provider] = provider
            };
        }

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            _settings.TryGetValue(key, out var value);
            return Task.FromResult(value);
        }

        public Task<string> GetRequiredAsync(string key, CancellationToken cancellationToken = default)
        {
            if (_settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return Task.FromResult(value!);
            }

            throw new InvalidOperationException($"Setting '{key}' is required.");
        }

        public Task<string?> GetForScopeAsync(
            string key,
            SettingScope scope,
            Guid? tenantId = null,
            Guid? userId = null,
            CancellationToken cancellationToken = default)
        {
            return GetAsync(key, cancellationToken);
        }

        public Task<SettingResolution> GetResolvedAsync(
            string key,
            Guid? tenantId = null,
            Guid? userId = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SettingResolution(key, null, "Test"));
        }
    }

    private sealed class TestCurrentUserContext : ICurrentUserContext
    {
        public Guid? UserId { get; set; }
        public Guid? TenantId { get; set; }
        public string? ExternalIssuer { get; set; }
        public string? ExternalSubject { get; set; }
        public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();
        public bool IsAuthenticated { get; set; }
    }

    private sealed class TestAuditLogWriter : IAuditLogWriter
    {
        public AuditLogEntry? LastEntry { get; private set; }

        public Task LogAsync(
            string action,
            string resourceType,
            Guid resourceId,
            Guid tenantId,
            Guid? actorId,
            string? correlationId,
            string? detailsJson = null,
            CancellationToken cancellationToken = default)
        {
            LastEntry = new AuditLogEntry(action, resourceType, resourceId, tenantId, actorId, correlationId, detailsJson);
            return Task.CompletedTask;
        }
    }

    private sealed record AuditLogEntry(
        string Action,
        string ResourceType,
        Guid ResourceId,
        Guid TenantId,
        Guid? ActorId,
        string? CorrelationId,
        string? DetailsJson);

    private sealed class TestCorrelationContext : ICorrelationContext
    {
        public string? CorrelationId { get; } = "corr-test";
    }

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

    private sealed class StubAuthTokenServiceFactory : IAuthTokenServiceFactory
    {
        private readonly IAuthTokenService _service = new StubAuthTokenService();

        public IAuthTokenService GetService(string provider) => _service;
    }

    private sealed class StubPasswordResetServiceFactory : IIdpPasswordResetServiceFactory
    {
        public string? LastProvider { get; private set; }
        public string? LastEmail { get; private set; }
        public Guid? LastTenantId { get; private set; }

        public IIdpPasswordResetService GetService(string provider)
        {
            LastProvider = provider;
            return new StubPasswordResetService(this);
        }

        private sealed class StubPasswordResetService : IIdpPasswordResetService
        {
            private readonly StubPasswordResetServiceFactory _parent;

            public StubPasswordResetService(StubPasswordResetServiceFactory parent)
            {
                _parent = parent;
            }

            public Task TriggerResetAsync(string email, Guid tenantId, CancellationToken cancellationToken = default)
            {
                _parent.LastEmail = email;
                _parent.LastTenantId = tenantId;
                return Task.CompletedTask;
            }
        }
    }

    private sealed class StubAuthTokenService : IAuthTokenService
    {
        public Task<TokenResponse> ExchangeAsync(TokenRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TokenResponse("access", "refresh", 3600, "Bearer", "id"));
        }
    }

    [Fact]
    public async Task TokenAsync_ReturnsProviderTokens()
    {
        var tenantId = Guid.NewGuid();
        var dbContext = CreateDbContext(tenantId);
        var settingProvider = new TestSettingProvider("Auth0");
        var authTokenFactory = new StubAuthTokenServiceFactory();
        var resetFactory = new StubPasswordResetServiceFactory();
        var currentUserContext = new TestCurrentUserContext();

        var service = new IdentityService(
            settingProvider,
            authTokenFactory,
            resetFactory,
            currentUserContext,
            new TestAuditLogWriter(),
            new TestCorrelationContext(),
            dbContext,
            new UserProvisioningService(
                dbContext,
                new UserIdentityService(
                    dbContext,
                    new Microsoft.Extensions.Logging.Abstractions.NullLogger<UserIdentityService>(),
                    new TestAuditLogWriter(),
                    new TestCorrelationContext()),
                new TestAuditLogWriter(),
                new SystemClock(),
                new TestCurrentUserProvider(currentUserContext),
                new TestCorrelationContext()),
            new AllowAllPermissionService());

        var response = await service.TokenAsync(new TokenRequest("password", "client", "user", "pass", null, null, null, null));

        response.AccessToken.Should().Be("access");
    }

    [Fact]
    public async Task SendPasswordResetAsync_TriggersProviderService()
    {
        var tenantId = Guid.NewGuid();
        var dbContext = CreateDbContext(tenantId);
        var settingProvider = new TestSettingProvider("AzureAd");
        var authTokenFactory = new StubAuthTokenServiceFactory();
        var resetFactory = new StubPasswordResetServiceFactory();
        var currentUserContext = new TestCurrentUserContext();

        var service = new IdentityService(
            settingProvider,
            authTokenFactory,
            resetFactory,
            currentUserContext,
            new TestAuditLogWriter(),
            new TestCorrelationContext(),
            dbContext,
            new UserProvisioningService(
                dbContext,
                new UserIdentityService(
                    dbContext,
                    new Microsoft.Extensions.Logging.Abstractions.NullLogger<UserIdentityService>(),
                    new TestAuditLogWriter(),
                    new TestCorrelationContext()),
                new TestAuditLogWriter(),
                new SystemClock(),
                new TestCurrentUserProvider(currentUserContext),
                new TestCorrelationContext()),
            new AllowAllPermissionService());

        var response = await service.SendPasswordResetAsync(
            new ForgotPasswordRequest("user@example.com", tenantId));

        response.Status.Should().Be("ok");
        resetFactory.LastProvider.Should().Be("AzureAd");
        resetFactory.LastEmail.Should().Be("user@example.com");
        resetFactory.LastTenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task GetUserInfoAsync_MapsUserAndParty()
    {
        var tenantId = Guid.NewGuid();
        var dbContext = CreateDbContext(tenantId);
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();

        dbContext.Users.Add(new User
        {
            Id = userId,
            TenantId = tenantId,
            ExternalIssuer = "issuer",
            ExternalSubject = "subject",
            Email = "user@example.com",
            Status = "Active"
        });

        dbContext.Parties.Add(new Party
        {
            Id = partyId,
            TenantId = tenantId,
            PartyType = "Individual",
            DisplayName = "Amina Diallo",
            Status = "Active"
        });

        dbContext.UserParties.Add(new UserParty
        {
            TenantId = tenantId,
            UserId = userId,
            PartyId = partyId,
            LinkType = "Individual"
        });

        await dbContext.SaveChangesAsync();

        var settingProvider = new TestSettingProvider("Auth0");
        var authTokenFactory = new StubAuthTokenServiceFactory();
        var resetFactory = new StubPasswordResetServiceFactory();
        var currentUserContext = new TestCurrentUserContext
        {
            UserId = userId,
            TenantId = tenantId,
            ExternalIssuer = "issuer",
            ExternalSubject = "subject",
            Roles = new[] { "Customer" }
        };

        var service = new IdentityService(
            settingProvider,
            authTokenFactory,
            resetFactory,
            currentUserContext,
            new TestAuditLogWriter(),
            new TestCorrelationContext(),
            dbContext,
            new UserProvisioningService(
                dbContext,
                new UserIdentityService(
                    dbContext,
                    new Microsoft.Extensions.Logging.Abstractions.NullLogger<UserIdentityService>(),
                    new TestAuditLogWriter(),
                    new TestCorrelationContext()),
                new TestAuditLogWriter(),
                new SystemClock(),
                new TestCurrentUserProvider(currentUserContext),
                new TestCorrelationContext()),
            new AllowAllPermissionService());

        var response = await service.GetUserInfoAsync();

        response.UserId.Should().Be(userId);
        response.PartyId.Should().Be(partyId);
        response.FirstName.Should().Be("Amina");
        response.LastName.Should().Be("Diallo");
        response.Roles.Should().ContainSingle("Customer");
    }

    private static PlatformDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(databaseName: $"IdentityTestDb_{Guid.NewGuid()}")
            .Options;

        var tenantProvider = new TestTenantProvider(tenantId);
        var dbContext = new PlatformDbContext(options, tenantProvider);

        dbContext.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Test Tenant",
            Environment = "Testing",
            DefaultCurrency = "USD",
            SupportedCountriesJson = "[]",
            Status = TenantStatus.Active
        });

        dbContext.SaveChanges();
        return dbContext;
    }

    private sealed class TestCurrentUserProvider : ICurrentUserProvider
    {
        private readonly ICurrentUserContext _context;

        public TestCurrentUserProvider(ICurrentUserContext context)
        {
            _context = context;
        }

        public Guid? GetCurrentUserId() => _context.UserId;

        public bool TryGetCurrentUserId(out Guid userId)
        {
            userId = _context.UserId ?? Guid.Empty;
            return _context.UserId.HasValue;
        }
    }
}
