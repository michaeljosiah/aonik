using System.Net;
using System.Security.Cryptography;
using System.Text;

using FluentAssertions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;

using Aonik.Infrastructure.Integrations.Gmail;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Infrastructure.Tests.Integrations;

public class GmailOAuthProviderTests
{
    [Fact]
    public void Authorization_Should_UseTheGoogleLibraryPkceChallenge_OfflineConsent_AndTheExactState()
    {
        var host = new Host();
        const string state = "request-id.random-state";

        ConnectorAuthorizationChallenge challenge = host.Provider.CreateAuthorization(["openid", "email", GmailActionKeys.ReadScope], state);

        var uri = new Uri(challenge.AuthorizationUrl);
        uri.Host.Should().Be("accounts.google.com");
        uri.Scheme.Should().Be("https");
        var query = QueryHelpers.ParseQuery(uri.Query);
        query["state"].ToString().Should().Be(state);
        query["response_type"].ToString().Should().Be("code");
        query["access_type"].ToString().Should().Be("offline");
        query["include_granted_scopes"].ToString().Should().Be("true");
        query["code_challenge_method"].ToString().Should().Be("S256");
        query["code_challenge"].ToString().Should().Be(WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(challenge.Verifier))));
        challenge.AuthorizationUrl.Should().NotContain(host.Options.ClientSecret).And.NotContain(challenge.Verifier);
    }

    [Fact]
    public async Task Exchange_Should_SendTheExactVerifierAndRedirect_AndReturnOnlyActualProviderScopes()
    {
        var host = new Host();

        ConnectorOAuthTokens tokens = await host.Provider.ExchangeAsync("synthetic-code", "synthetic-verifier");

        host.Http.Calls.Should().Be(1);
        host.Http.LastUri!.Host.Should().Be("oauth2.googleapis.com");
        host.Http.LastForm!["code_verifier"].ToString().Should().Be("synthetic-verifier");
        host.Http.LastForm["redirect_uri"].ToString().Should().Be(host.Options.RedirectUri);
        host.Http.LastForm["client_id"].ToString().Should().Be(host.Options.ClientId);
        tokens.GrantedScopes.Should().Equal(GmailActionKeys.ReadScope, "openid", "email");
        tokens.ExpiresAt.Should().Be(host.Clock.UtcNow.AddHours(1));
        tokens.ToString().Should().NotContain("synthetic-access").And.NotContain("synthetic-refresh");
    }

    [Fact]
    public async Task Exchange_Should_NotBlindlyRetryAnUncertainCodeExchange_OrExposeProviderDiagnostics()
    {
        var host = new Host();
        host.Http.Response = _ => new(HttpStatusCode.InternalServerError)
        { Content = new StringContent("{\"error\":\"server_error\",\"error_description\":\"private-provider-detail\"}") };

        Func<Task> exchange = async () => { await host.Provider.ExchangeAsync("synthetic-code", "synthetic-verifier"); };

        ConnectorProviderException exception = (await exchange.Should().ThrowAsync<ConnectorProviderException>()).Which;
        host.Http.Calls.Should().Be(1);
        exception.ToString().Should().NotContain("private-provider-detail");
    }

    [Fact]
    public async Task Identity_Should_UseTheFixedGoogleUserinfoEndpoint_AndRequireVerifiedEmail()
    {
        var host = new Host();
        host.Http.Response = _ => new(HttpStatusCode.OK) { Content = new StringContent("{\"sub\":\"stable-account-1\",\"email\":\"mailbox@example.test\",\"email_verified\":true}") };

        ConnectorAccountIdentity identity = await host.Provider.GetIdentityAsync("synthetic-access");

        host.Http.LastUri!.AbsoluteUri.Should().Be("https://openidconnect.googleapis.com/v1/userinfo");
        identity.Id.Should().Be("stable-account-1");
        host.Http.Response = _ => new(HttpStatusCode.OK) { Content = new StringContent("{\"sub\":\"stable-account-1\",\"email\":\"mailbox@example.test\",\"email_verified\":false}") };
        Func<Task> unverified = async () => { await host.Provider.GetIdentityAsync("synthetic-access"); };
        (await unverified.Should().ThrowAsync<ConnectorProviderException>()).Which.Code.Should().Be("ProviderIdentityInvalid");
    }

    [Fact]
    public void Configuration_Should_RejectUnencryptedNonLoopbackRedirects()
    {
        var host = new Host();
        host.Options.RedirectUri = "http://external.example.test/oauth/callback";
        host.Provider.IsConfigured.Should().BeFalse();
        host.Options.RedirectUri = "https://api.example.test/integrations/oauth/gmail/callback#unsafe-fragment";
        host.Provider.IsConfigured.Should().BeFalse();
    }

    private sealed class Host
    {
        public GmailConnectorOptions Options { get; } = new()
        {
            ClientId = "synthetic-client", ClientSecret = "synthetic-client-secret", RedirectUri = "https://api.example.test/integrations/oauth/gmail/callback",
            ReviewReturnUri = "https://app.example.test/settings/my-connections",
        };
        public TestClock Clock { get; } = new();
        public FakeHttpFactory Http { get; } = new();
        public GmailOAuthProvider Provider { get; }
        public Host()
        {
            var environment = new Mock<IHostEnvironment>();
            environment.SetupGet(value => value.EnvironmentName).Returns("Testing");
            Provider = new(Http, Http, new FixedOptions(Options), environment.Object, Clock);
        }
    }
    private sealed class FixedOptions(GmailConnectorOptions value) : IOptionsMonitor<GmailConnectorOptions>
    {
        public GmailConnectorOptions CurrentValue => value;
        public GmailConnectorOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<GmailConnectorOptions, string?> listener) => null;
    }
    private sealed class TestClock : IClock { public DateTime UtcNow => new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc); }
    private sealed class FakeHttpFactory : System.Net.Http.IHttpClientFactory, IHttpMessageHandlerFactory
    {
        public int Calls { get; private set; }
        public Uri? LastUri { get; private set; }
        public Dictionary<string, Microsoft.Extensions.Primitives.StringValues>? LastForm { get; private set; }
        public Func<HttpRequestMessage, HttpResponseMessage> Response { get; set; } = _ => new(HttpStatusCode.OK)
        { Content = new StringContent("{\"access_token\":\"synthetic-access\",\"refresh_token\":\"synthetic-refresh\",\"expires_in\":3600,\"token_type\":\"Bearer\",\"scope\":\"https://www.googleapis.com/auth/gmail.readonly openid email\"}") };
        public HttpClient CreateClient(string name) => new(CreateHandler(name));
        public HttpMessageHandler CreateHandler(string name) => new Handler(this);
        private sealed class Handler(FakeHttpFactory factory) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                factory.Calls++; factory.LastUri = request.RequestUri;
                if (request.Content is not null) factory.LastForm = QueryHelpers.ParseQuery(await request.Content.ReadAsStringAsync(cancellationToken));
                return factory.Response(request);
            }
        }
    }
}
