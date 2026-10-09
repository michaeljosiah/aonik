using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Aonik.Application.Abstractions.Persistence;
using Aonik.Infrastructure.Authentication;
using Aonik.Infrastructure.Identity;
using Aonik.Infrastructure.Multitenancy;
using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Services.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;

using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace Aonik.Infrastructure.Tests.Authentication;

/// <summary>Exercises the post-validation application boundary; signed bearer validation is covered by API tests.</summary>
public sealed class AccountAccessAuthenticationTests
{
    [Fact]
    public async Task UnknownCompletion_Should_ExposeTrustedProofWithoutProvisioningOrLocalAuthority()
    {
        using var h = new Harness();
        var result = await h.ValidateAsync();

        result.Result?.Failure.Should().BeNull();
        h.Proof.Should().NotBeNull();
        h.Proof!.VerifiedEmail.Should().Be("buyer@example.com");
        h.Proof.TenantId.Should().Be(h.TenantId);
        h.Proof.ExistingUserId.Should().BeNull();
        h.Current.UserId.Should().BeNull();
        h.Current.IsAuthenticated.Should().BeFalse();
        h.Db.Users.Should().BeEmpty();
        h.Users.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("/identity/account-access/complete", "POST", false)]
    [InlineData("/identity/account-access/resolve", "POST", true)]
    [InlineData("/identity/account-access/complete", "GET", true)]
    public async Task UnknownSubject_Should_NotBypassOrdinaryProvisioning(string path, string method, bool marker)
    {
        using var h = new Harness();
        var result = await h.ValidateAsync(path: path, method: method, marker: marker);

        result.Result!.Failure.Should().NotBeNull();
        h.Proof.Should().BeNull();
        h.Current.UserId.Should().BeNull();
    }

    [Theory]
    [InlineData("missing-email")]
    [InlineData("unverified")]
    [InlineData("duplicate-verified")]
    [InlineData("missing-iat")]
    [InlineData("future-auth")]
    [InlineData("other-issuer")]
    public async Task Completion_Should_RejectAbsentOrAmbiguousTrustedEvidence(string scenario)
    {
        using var h = new Harness();
        var result = await h.ValidateAsync(scenario: scenario);

        result.Result!.Failure.Should().NotBeNull();
        h.Proof.Should().BeNull();
        h.Current.UserId.Should().BeNull();
        h.Db.Users.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Suspended", false)]
    [InlineData("Active", true)]
    public async Task KnownCompletion_Should_RejectInactiveOrRevokedIdentity(string status, bool revoked)
    {
        using var h = new Harness(status, revoked);

        var result = await h.ValidateAsync();

        result.Result!.Failure.Should().NotBeNull();
        h.Proof.Should().BeNull();
        h.Current.UserId.Should().BeNull();
    }

    [Fact]
    public async Task KnownOrdinaryRoute_Should_ExposeCheckedIdentityRevisionAndFreshAuthEvidence()
    {
        using var h = new Harness("Active");

        var result = await h.ValidateAsync(path: "/profiles/customers/me/email", method: "PUT", marker: false);

        result.Result?.Failure.Should().BeNull();
        h.Proof!.ExistingUserId.Should().Be(h.User!.Id);
        h.Proof.IdentityRevision.Should().Be(7);
        h.Proof.AuthenticationTimeUtc.Should().Be(h.Now.AddMinutes(-1));
        h.Current.UserId.Should().Be(h.User.Id);
    }

    private sealed class Harness : IDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public DateTime Now { get; } = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
        public AonikDbContext Db { get; }
        public User? User { get; }
        public Mock<IUserIdentityService> Users { get; } = new(MockBehavior.Strict);
        private readonly ServiceProvider _services;
        private readonly DefaultHttpContext _http = new();
        public ICurrentUserContext Current => _services.GetRequiredService<ICurrentUserContext>();
        public AccountAccessIdentityProof? Proof => _services.GetRequiredService<IAccountAccessIdentityProofAccessor>().GetCurrent();

        public Harness(string? status = null, bool revoked = false)
        {
            var tenants = new Mock<ITenantProvider>();
            tenants.Setup(t => t.GetCurrentTenantId()).Returns(TenantId);
            var id = TenantId;
            tenants.Setup(t => t.TryGetCurrentTenantId(out id)).Returns(true);
            Db = new(new DbContextOptionsBuilder<AonikDbContext>().UseInMemoryDatabase($"AccountAuth_{Guid.NewGuid()}").Options, tenants.Object);
            Db.Tenants.Add(new Tenant { Id = TenantId, Name = "Account auth", Status = TenantStatus.Active });
            if (status is not null)
            {
                User = new User { TenantId = TenantId, ExternalIssuer = "https://auth.example/", ExternalSubject = "auth0|buyer",
                    Email = "buyer@example.com", Status = status, IdentityRevision = 7 };
                Db.Users.Add(User);
                Users.Setup(u => u.ResolveOrCreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                    It.IsAny<string?>(), TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(User);
                Users.Setup(u => u.GetRoleNamesAsync(User.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<string>());
            }
            Db.SaveChanges();
            Db.ChangeTracker.Clear();
            var resolver = new Mock<ITenantResolver>();
            resolver.Setup(t => t.ResolveTenantIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(TenantId);
            var settings = new InMemorySettingProvider();
            settings.Set(AuthSettingNames.Provider, "Auth0");
            var clock = new Mock<IClock>();
            clock.SetupGet(c => c.UtcNow).Returns(Now);
            var sessions = new Mock<IUserSessionBlocklist>();
            sessions.Setup(s => s.IsRevokedAsync(TenantId, It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(revoked);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAonikAuthentication(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Provider"] = "Auth0", ["Auth:Auth0:Authority"] = "https://auth.example/", ["Auth:Auth0:Audience"] = "api",
                ["Auth:AccountAccess:EmailClaimType"] = "https://app.example/email",
                ["Auth:AccountAccess:EmailVerifiedClaimType"] = "https://app.example/email_verified"
            }).Build());
            services.AddSingleton<IAonikDbContext>(Db);
            services.AddSingleton(resolver.Object);
            services.AddSingleton<ISettingProvider>(settings);
            services.AddSingleton(Users.Object);
            services.AddSingleton(clock.Object);
            services.AddSingleton(sessions.Object);
            services.AddSingleton<ITenantContext, TenantContext>();
            services.AddSingleton<ICurrentUserContext, HttpContextCurrentUserContext>();
            _services = services.BuildServiceProvider();
            _http.RequestServices = _services;
            _services.GetRequiredService<IHttpContextAccessor>().HttpContext = _http;
        }

        public async Task<TokenValidatedContext> ValidateAsync(string path = AccountAccessCompletionEndpointMetadata.Route,
            string method = "POST", bool marker = true, string? scenario = null)
        {
            _http.Request.Path = path;
            _http.Request.Method = method;
            _http.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
                new EndpointMetadataCollection(marker ? new object[] { new AccountAccessCompletionEndpointMetadata() } : []), "test"));
            var claims = new List<Claim>
            {
                new("sub", "auth0|buyer"), new("email", "untrusted-fallback@example.com"),
                new("https://app.example/email", "buyer@example.com"), new("https://app.example/email_verified", "true"),
                new("iat", new DateTimeOffset(Now.AddSeconds(-30)).ToUnixTimeSeconds().ToString()),
                new("auth_time", new DateTimeOffset(Now.AddMinutes(-1)).ToUnixTimeSeconds().ToString())
            };
            if (scenario == "missing-email") claims.RemoveAll(c => c.Type == "https://app.example/email");
            if (scenario == "unverified") claims.RemoveAll(c => c.Type == "https://app.example/email_verified");
            if (scenario == "duplicate-verified") claims.Add(new("https://app.example/email_verified", "false"));
            if (scenario == "missing-iat") claims.RemoveAll(c => c.Type == "iat");
            if (scenario == "future-auth")
            {
                claims.RemoveAll(c => c.Type == "auth_time");
                claims.Add(new("auth_time", new DateTimeOffset(Now.AddMinutes(1)).ToUnixTimeSeconds().ToString()));
            }
            var options = _services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("Auth0");
            var context = new TokenValidatedContext(_http, new AuthenticationScheme("Auth0", "Auth0", typeof(JwtBearerHandler)), options)
            {
                Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")),
                SecurityToken = new JwtSecurityToken(scenario == "other-issuer" ? "https://auth.example/other" : "https://auth.example/", "api", claims)
            };
            _http.User = context.Principal;
            await options.Events.TokenValidated(context);
            return context;
        }

        public void Dispose() { _services.Dispose(); Db.Dispose(); }
    }
}
