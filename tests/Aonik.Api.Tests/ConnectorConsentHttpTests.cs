using System.Net;
using System.Net.Http.Json;

using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Persistence;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;

namespace Aonik.Api.Tests;

public class ConnectorConsentHttpTests
{
    [Fact]
    public async Task Callback_Should_RequireTheProtectedOwnerCookie_AndStageUntilAuthenticatedAccountConfirmation()
    {
        await using var factory = new ConsentFactory();
        Guid tenantId = Guid.NewGuid();
        using HttpClient owner = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenantId).WithRoles("PersonalUser")
            .WithPermissions("Integration.Read", "Integration.Use", "Integration.Request"));
        owner.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            db.Settings.AddRange(new Setting { Key = IntegrationSettingNames.GmailEnabled, Value = "true", Scope = SettingScope.Tenant, TenantId = tenantId },
                new Setting { Key = IntegrationSettingNames.GmailAllowSelfService, Value = "true", Scope = SettingScope.Tenant, TenantId = tenantId });
            await db.SaveChangesAsync();
        }
        var request = new CreateConnectionRequest("gmail", "User", [new(GmailActionKeys.Search, 1)], "Invoice evidence", "consent-http");
        var created = (await (await owner.PostAsJsonAsync("/v1/integrations/connection-requests", request)).Content.ReadFromJsonAsync<ConnectionRequestDetail>())!;
        created.Status.Should().Be("AwaitingConsent");
        var begin = await owner.PostAsJsonAsync($"/v1/integrations/connection-requests/{created.Id}/consent", new { created.ExpectedVersion });
        begin.StatusCode.Should().Be(HttpStatusCode.OK, await begin.Content.ReadAsStringAsync());
        var challenge = await begin.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        challenge.TryGetProperty("cookie", out _).Should().BeFalse();
        string state = QueryHelpers.ParseQuery(new Uri(challenge.GetProperty("authorizationUrl").GetString()!).Query)["state"].ToString();
        string cookie = begin.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        begin.Headers.GetValues("Set-Cookie").Single().Should().Contain("path=/api/integrations/gmail/oauth/callback");
        using HttpClient callback = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        string callbackUrl = "/integrations/gmail/oauth/callback?state=" + Uri.EscapeDataString(state) + "&code=synthetic-code";
        (await callback.GetAsync(callbackUrl)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        callback.DefaultRequestHeaders.Add("Cookie", cookie);
        // An anonymous caller's arbitrary tenant header cannot override the protected proof.
        callback.DefaultRequestHeaders.Add("X-Tenant-Id", Guid.NewGuid().ToString());
        var returned = await callback.GetAsync(callbackUrl);
        returned.StatusCode.Should().Be(HttpStatusCode.Found, await returned.Content.ReadAsStringAsync());
        returned.Headers.Location!.AbsoluteUri.Should().Be($"https://app.example.test/settings/my-connections?requestId={created.Id:D}");
        (await callback.GetAsync(callbackUrl)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var staged = (await owner.GetFromJsonAsync<ConnectionRequestDetail>($"/v1/integrations/connection-requests/{created.Id}"))!;
        staged.Status.Should().Be("AwaitingConsent");
        staged.CanConfirm.Should().BeTrue();
        staged.ConsentedAccountLabel.Should().Be("mailbox@example.test");
        var confirmation = await owner.PostAsJsonAsync($"/v1/integrations/connection-requests/{created.Id}/confirm-account", new { staged.ExpectedVersion });
        confirmation.StatusCode.Should().Be(HttpStatusCode.OK, await confirmation.Content.ReadAsStringAsync());
        var fulfilled = (await confirmation.Content.ReadFromJsonAsync<ConnectionRequestDetail>())!;
        fulfilled.Status.Should().Be("Fulfilled");
        fulfilled.GrantId.Should().NotBeNull();
        (await confirmation.Content.ReadAsStringAsync()).Should().NotContain("synthetic-access").And.NotContain("synthetic-refresh");
    }

    private sealed class ConsentFactory : CustomWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
                { ["Integrations:Gmail:OAuth:ReviewReturnUri"] = "https://app.example.test/settings/my-connections",
                    ["Integrations:Gmail:OAuth:RedirectUri"] = "https://api.example.test/api/integrations/gmail/oauth/callback" }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IConnectorOAuthProvider>(); services.AddSingleton<IConnectorOAuthProvider, SyntheticOAuth>();
            });
        }
    }
    private sealed class SyntheticOAuth : IConnectorOAuthProvider
    {
        public string ConnectorKey => "gmail";
        public bool IsConfigured => true;
        public ConnectorAuthorizationChallenge CreateAuthorization(IReadOnlyList<string> scopes, string state)
            => new("https://accounts.google.com/o/oauth2/auth?state=" + Uri.EscapeDataString(state), "synthetic-verifier");
        public Task<ConnectorOAuthTokens> ExchangeAsync(string code, string verifier, CancellationToken cancellationToken = default)
        {
            code.Should().Be("synthetic-code"); verifier.Should().Be("synthetic-verifier");
            return Task.FromResult(new ConnectorOAuthTokens("synthetic-access", "synthetic-refresh", DateTime.UtcNow.AddHours(1), [GmailActionKeys.ReadScope, "openid", "email"]));
        }
        public Task<ConnectorAccountIdentity> GetIdentityAsync(string accessToken, CancellationToken cancellationToken = default)
        { accessToken.Should().Be("synthetic-access"); return Task.FromResult(new ConnectorAccountIdentity("account-1", "mailbox@example.test")); }
        public Task<ConnectorOAuthTokens> RefreshAsync(string refreshToken, IReadOnlyList<string> scopes, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RevokeAsync(string token, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
