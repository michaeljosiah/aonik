using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions.Identity;
using Aonik.SharedKernel.Abstractions.Messaging;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;

using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Aonik.Api.Tests;

// These requests run through the real JwtBearer validation/events and production endpoint metadata.
// Only discovery/signing keys and email delivery are local test doubles.
public class AccountAccessEndpointTests(AccountAccessWebApplicationFactory factory)
    : IClassFixture<AccountAccessWebApplicationFactory>
{
    private const string Root = "/identity/account-access";

    [Fact]
    public async Task EmailChange_Should_KeepTheCurrentEmailUntilNewMailboxConfirmation_ThenRevokeOldBearer()
    {
        var seeded = await SeedAsync();
        var subject = "auth0|" + Guid.NewGuid().ToString("N");
        using var client = Client(seeded.TenantId, Token(seeded.TenantId, subject));
        (await client.PostAsJsonAsync(Root + "/complete", new { seeded.Token })).StatusCode.Should().Be(HttpStatusCode.OK);

        using var requested = await client.PutAsJsonAsync("/profiles/customers/me/email", new { newEmail = "new@example.test" });
        requested.StatusCode.Should().Be(HttpStatusCode.Accepted);
        AssertPrivate(requested);
        string confirmationToken;
        await using (var scope = Scope(seeded.TenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
            (await db.Users.SingleAsync()).Email.Should().Be("paid@example.test");
            var action = await db.AccountAccessActions.SingleAsync(x => x.Purpose == AccountAccessPurposes.EmailChange);
            action.Status.Should().Be("Pending");
            factory.Accounts.ConfirmedSubjects.Should().NotContain(subject);
            await scope.ServiceProvider.GetRequiredService<IAccountAccessService>().DeliverAsync(action.Id, action.Generation);
            confirmationToken = TokenFromEmail(factory.Emails.Messages.Last());
        }
        using var confirm = await client.PostAsJsonAsync("/identity/email-change/complete", new { token = confirmationToken });
        confirm.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.Accounts.ConfirmedSubjects.Should().Contain(subject);
        using var oldSession = await client.GetAsync("/profiles/customers/me");
        oldSession.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await using var verification = Scope(seeded.TenantId);
        var verify = verification.ServiceProvider.GetRequiredService<AonikDbContext>();
        var user = await verify.Users.SingleAsync();
        user.Email.Should().Be("new@example.test");
        user.IdentityRevision.Should().Be(1);
        (await verify.PartyContacts.SingleAsync(x => x.Type == "Email" && x.IsPrimary)).Value.Should().Be("new@example.test");
        (await verify.AccountAccessActions.SingleAsync(x => x.Purpose == AccountAccessPurposes.EmailChange))
            .Status.Should().Be("Consumed");
    }

    [Fact]
    public async Task PasswordReset_Should_ReturnTheSameResponseWithoutDispatch_WhenBodyTenantDiffers()
    {
        var seeded = await SeedAsync();
        using var client = Client(seeded.TenantId);
        using var valid = await client.PostAsJsonAsync("/identity/password/forgot",
            new { email = "reset@example.test", seeded.TenantId });
        using var mismatch = await client.PostAsJsonAsync("/identity/password/forgot",
            new { email = "reset@example.test", tenantId = Guid.NewGuid() });
        valid.StatusCode.Should().Be(HttpStatusCode.OK);
        mismatch.StatusCode.Should().Be(HttpStatusCode.OK);
        (await mismatch.Content.ReadAsStringAsync()).Should().Be(await valid.Content.ReadAsStringAsync());
        factory.Resets.Tenants.Count(x => x == seeded.TenantId).Should().Be(1);
        AssertPrivate(mismatch);
    }

    [Fact]
    public async Task IpBudget_Should_KeepResendAndPasswordResetResponsesNeutral_WhenExhausted()
    {
        var seeded = await SeedAsync();
        await using var limited = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Identity:SecurityRequestsPerMinute"] = "1" })));
        using var client = limited.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", seeded.TenantId.ToString());
        using var first = await client.PostAsJsonAsync(Root + "/resolve", new { token = "invalid" });
        first.StatusCode.Should().Be(HttpStatusCode.Gone);
        using var throttled = await client.PostAsJsonAsync(Root + "/resolve", new { seeded.Token });
        throttled.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        AssertPrivate(throttled);
        using var resend = await client.PostAsJsonAsync(Root + "/resend", new { seeded.Token });
        resend.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await resend.Content.ReadAsStringAsync()).Should().BeEmpty();
        AssertPrivate(resend);
        using var reset = await client.PostAsJsonAsync("/identity/password/forgot",
            new { email = "paid@example.test", seeded.TenantId });
        reset.StatusCode.Should().Be(HttpStatusCode.OK);
        (await reset.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString().Should().Be("ok");
        AssertPrivate(reset);
    }

    [Fact]
    public async Task Complete_Should_RequireExplicitVerifiedAccess_AndConsumeThePaidLinkOnlyOnce()
    {
        var seeded = await SeedAsync();
        using var anonymous = Client(seeded.TenantId);
        using var resolved = await anonymous.PostAsJsonAsync(Root + "/resolve", new { seeded.Token });
        resolved.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertPrivate(resolved);
        using var noBearer = await anonymous.PostAsJsonAsync(Root + "/complete", new { seeded.Token });
        noBearer.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertNoUserAsync(seeded.TenantId);

        using var signedIn = Client(seeded.TenantId, Token(seeded.TenantId, "new-subject"));
        using var before = await signedIn.GetAsync("/profiles/customers/me");
        before.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "an unknown external subject must not gain ordinary tenant membership");
        using var completed = await signedIn.PostAsJsonAsync(Root + "/complete", new { seeded.Token });
        completed.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertPrivate(completed);
        using var replay = await signedIn.PostAsJsonAsync(Root + "/complete", new { seeded.Token });
        replay.StatusCode.Should().Be(HttpStatusCode.Gone);
        using var consumed = await anonymous.PostAsJsonAsync(Root + "/resolve", new { seeded.Token });
        consumed.StatusCode.Should().Be(HttpStatusCode.Gone);
        AssertPrivate(consumed);

        await using var scope = Scope(seeded.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        var user = await db.Users.SingleAsync();
        user.ExternalSubject.Should().Be("new-subject");
        user.Email.Should().Be("paid@example.test");
        (await db.UserParties.CountAsync()).Should().Be(1);
        (await db.AccountAccessActions.SingleAsync()).ConsumedByUserId.Should().Be(user.Id);
        var claim = await db.Set<OutboxMessage>().SingleAsync(x => x.TenantId == seeded.TenantId
            && x.EventType == typeof(AccountAccessVerifiedEvent).FullName);
        claim.Payload.Should().NotContain("paid@example.test").And.NotContain(seeded.Token);
    }

    [Theory]
    [InlineData("missing-verification")]
    [InlineData("unverified")]
    [InlineData("wrong-email")]
    [InlineData("wrong-issuer")]
    [InlineData("wrong-audience")]
    [InlineData("bad-signature")]
    [InlineData("expired")]
    public async Task Complete_Should_RejectUntrustedProof_WithoutCreatingOrDisclosingAnAccount(string defect)
    {
        var seeded = await SeedAsync();
        using var client = Client(seeded.TenantId, Token(seeded.TenantId, "untrusted-subject", defect));
        using var result = await client.PostAsJsonAsync(Root + "/complete", new
        {
            seeded.Token, email = "paid@example.test", email_verified = true, subject = "claimed-subject"
        });
        result.IsSuccessStatusCode.Should().BeFalse();
        result.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
        (await result.Content.ReadAsStringAsync()).Should().NotContain("paid@example.test")
            .And.NotContain("untrusted-subject").And.NotContain("claimed-subject");
        await AssertNoUserAsync(seeded.TenantId);
        using var anonymous = Client(seeded.TenantId);
        (await anonymous.PostAsJsonAsync(Root + "/resolve", new { seeded.Token })).StatusCode
            .Should().Be(HttpStatusCode.OK, "failed proof must not consume the link");
    }

    [Theory]
    [InlineData("Suspended")]
    [InlineData("Deactivated")]
    [InlineData("revoked")]
    public async Task Complete_Should_RejectAnExistingInactiveOrRevokedIdentity(string state)
    {
        var seeded = await SeedAsync();
        await using (var scope = Scope(seeded.TenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
            var user = new User { TenantId = seeded.TenantId, ExternalIssuer = AccountAccessWebApplicationFactory.Issuer,
                ExternalSubject = "existing", Email = "paid@example.test", Status = state == "revoked" ? "Active" : state };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            if (state == "revoked")
                await scope.ServiceProvider.GetRequiredService<IUserSessionBlocklist>()
                    .RevokeAsync(seeded.TenantId, user.Id, user.Id, "test");
        }
        using var client = Client(seeded.TenantId, Token(seeded.TenantId, "existing"));
        using var response = await client.PostAsJsonAsync(Root + "/complete", new { seeded.Token });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await using var verification = Scope(seeded.TenantId);
        (await verification.ServiceProvider.GetRequiredService<AonikDbContext>().AccountAccessActions.SingleAsync())
            .ConsumedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAndResend_Should_BeNeutralAcrossInvalidExpiredConsumedAndForeignTokens()
    {
        var seeded = await SeedAsync();
        var foreign = await SeedAsync();
        await using (var scope = Scope(seeded.TenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
            (await db.AccountAccessActions.SingleAsync()).ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        using var client = Client(seeded.TenantId);
        foreach (var token in new[] { "invalid", foreign.Token, seeded.Token })
        {
            using var resolve = await client.PostAsJsonAsync(Root + "/resolve", new { token });
            resolve.StatusCode.Should().Be(HttpStatusCode.Gone);
            AssertPrivate(resolve);
            (await resolve.Content.ReadAsStringAsync()).Should().NotContain("paid@example.test");
            using var resend = await client.PostAsJsonAsync(Root + "/resend", new { token });
            resend.StatusCode.Should().Be(HttpStatusCode.Accepted);
            (await resend.Content.ReadAsStringAsync()).Should().BeEmpty();
            AssertPrivate(resend);
        }
        using var scan = await client.GetAsync(Root + "/complete?token=" + Uri.EscapeDataString(seeded.Token));
        scan.IsSuccessStatusCode.Should().BeFalse();
        await AssertNoUserAsync(seeded.TenantId);
    }

    private async Task<(Guid TenantId, string Token)> SeedAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var scope = Scope(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Account access tests", Environment = "Testing",
            DefaultCurrency = "GBP", Status = TenantStatus.Active });
        db.Settings.Add(new Setting { Key = AccountAccessSettingNames.Configuration, TenantId = tenantId,
            Scope = SettingScope.Tenant,
            Value = JsonSerializer.Serialize(new AccountAccessConfiguration(true, "https://store.example.test",
                "/account/access", "/account/email-change"), new JsonSerializerOptions(JsonSerializerDefaults.Web)) });
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IPaidAccountAccessService>().IssueAsync(new(
            tenantId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "paid@example.test"));
        var action = await db.AccountAccessActions.SingleAsync();
        await scope.ServiceProvider.GetRequiredService<IAccountAccessService>().DeliverAsync(action.Id, action.Generation);
        return (tenantId, TokenFromEmail(factory.Emails.Messages.Last()));
    }

    private static string TokenFromEmail(TemplatedEmailMessage message)
    {
        var uri = new Uri((string)message.Model["action_url"]!);
        return QueryHelpers.ParseQuery(string.IsNullOrEmpty(uri.Fragment) ? uri.Query : uri.Fragment[1..])["token"].ToString();
    }

    private async Task AssertNoUserAsync(Guid tenantId)
    {
        await using var scope = Scope(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        (await db.Users.CountAsync()).Should().Be(0);
        (await db.UserParties.CountAsync()).Should().Be(0);
    }

    private AsyncServiceScope Scope(Guid tenantId)
    {
        var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
        return scope;
    }

    private HttpClient Client(Guid tenantId, string? bearer = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        if (bearer is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return client;
    }

    private static void AssertPrivate(HttpResponseMessage response)
    {
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Headers.GetValues("Referrer-Policy").Should().ContainSingle().Which.Should().Be("no-referrer");
    }

    private static string Token(Guid tenantId, string subject, string? defect = null)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new("sub", subject), new("aonik_tenant_id", tenantId.ToString()),
            new("email", defect == "wrong-email" ? "different@example.test" : "paid@example.test"),
            new("iat", new DateTimeOffset(now.AddMinutes(-2)).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("auth_time", new DateTimeOffset(now.AddMinutes(-2)).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("roles", "PersonalUser")
        };
        if (defect != "missing-verification")
            claims.Add(new Claim("email_verified", defect == "unverified" ? "false" : "true", ClaimValueTypes.Boolean));
        var key = defect == "bad-signature"
            ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes("different-signing-key-with-at-least-32-characters"))
            : AccountAccessWebApplicationFactory.Key;
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            defect == "wrong-issuer" ? "https://untrusted.example.test/" : AccountAccessWebApplicationFactory.Issuer,
            defect == "wrong-audience" ? "another-api" : "account-access-tests", claims,
            now.AddHours(-1), defect == "expired" ? now.AddMinutes(-10) : now.AddMinutes(10),
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256)));
    }
}

public sealed class AccountAccessWebApplicationFactory : CustomWebApplicationFactory
{
    public const string Issuer = "https://identity.example.test/";
    public static readonly SymmetricSecurityKey Key = new(Encoding.UTF8.GetBytes("account-access-tests-signing-key-32-bytes-minimum"));
    public CapturingEmailSender Emails { get; } = new();
    public ConfirmedAccounts Accounts { get; } = new();
    public CapturingPasswordReset Resets { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:Provider"] = "Auth0", ["Auth:Auth0:Authority"] = Issuer,
            ["Auth:Auth0:Audience"] = "account-access-tests", ["Auth:Auth0:ValidateIssuer"] = "true",
            ["Auth:Auth0:ClockSkewSeconds"] = "0"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ITemplatedEmailSender>();
            services.AddSingleton<ITemplatedEmailSender>(Emails);
            services.RemoveAll<IIdpAccountServiceFactory>();
            services.AddSingleton<IIdpAccountServiceFactory>(Accounts);
            services.RemoveAll<IIdpPasswordResetServiceFactory>();
            services.AddSingleton<IIdpPasswordResetServiceFactory>(Resets);
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = "Auth0";
                options.DefaultAuthenticateScheme = "Auth0";
                options.DefaultChallengeScheme = "Auth0";
            });
            services.PostConfigure<AuthorizationOptions>(options => options.DefaultPolicy =
                new AuthorizationPolicyBuilder("Auth0").RequireAuthenticatedUser().Build());
            services.PostConfigure<JwtBearerOptions>("Auth0", options =>
            {
                var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                configuration.SigningKeys.Add(Key);
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                options.TokenValidationParameters.ValidIssuer = Issuer;
                options.TokenValidationParameters.ValidAudience = "account-access-tests";
                options.TokenValidationParameters.IssuerSigningKey = Key;
            });
        });
    }

    public sealed class CapturingEmailSender : ITemplatedEmailSender
    {
        public ConcurrentQueue<TemplatedEmailMessage> Messages { get; } = new();
        public Task SendAsync(TemplatedEmailMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Enqueue(message);
            return Task.CompletedTask;
        }
    }

    public sealed class CapturingPasswordReset : IIdpPasswordResetServiceFactory, IIdpPasswordResetService
    {
        public ConcurrentQueue<Guid> Tenants { get; } = new();
        public IIdpPasswordResetService GetService(string provider) => this;
        public Task TriggerResetAsync(string email, Guid tenantId, CancellationToken cancellationToken = default)
        { Tenants.Enqueue(tenantId); return Task.CompletedTask; }
    }

    public sealed class ConfirmedAccounts : IIdpAccountServiceFactory, IIdpAccountService
    {
        public ConcurrentQueue<string> ConfirmedSubjects { get; } = new();
        public IIdpAccountService GetService(string provider) => this;
        public Task ConfirmVerifiedEmailAsync(User user, string expectedCurrentEmail, string newEmail,
            CancellationToken cancellationToken = default)
        { ConfirmedSubjects.Enqueue(user.ExternalSubject); return Task.CompletedTask; }
        public Task UpdateEmailAsync(User user, string newEmail, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The immediate email-change path must never run.");
        public Task ValidatePasswordAsync(User user, string password, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Mailbox confirmation must use fresh provider proof.");
        public Task UpdatePasswordAsync(User user, string newPassword, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
