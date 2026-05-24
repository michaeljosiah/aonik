using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

using Aonik.Infrastructure.Authentication.TokenExchange;
using Aonik.Platform.Contracts.Models.Authentication;
using Aonik.Platform.Services.Settings;
using FluentAssertions;

namespace Aonik.Infrastructure.Tests.Authentication;

/// <summary>
/// Spec 029 — covers the Keycloak <c>/protocol/openid-connect/token</c>
/// form-encoded POST shape, optional-field omission, and JSON response
/// mapping across the password / authorization_code / refresh_token grants.
/// </summary>
public class KeycloakAuthTokenServiceTests
{
    private const string Authority = "http://keycloak.local/realms/aonik";
    private const string ExpectedTokenUrl = "http://keycloak.local/realms/aonik/protocol/openid-connect/token";

    [Fact]
    public async Task ExchangeAsync_Should_PostPasswordGrant_And_MapResponse()
    {
        var handler = new ScriptedHandler
        {
            TokenResponseBody = """{"access_token":"abc.def.ghi","refresh_token":"r123","expires_in":3600,"token_type":"Bearer","id_token":"id.token.xyz"}""",
        };
        var service = CreateService(handler);

        var request = new TokenRequest(
            GrantType: "password",
            ClientId: "aonik-spa",
            Username: "alice@example.com",
            Password: "secret",
            Scope: "openid profile",
            RedirectUri: null,
            CodeVerifier: null,
            AuthorizationCode: null,
            RefreshToken: null);

        var result = await service.ExchangeAsync(request, CancellationToken.None);

        handler.TokenRequest.Should().NotBeNull();
        handler.TokenRequest!.RequestUri!.AbsoluteUri.Should().Be(ExpectedTokenUrl);
        handler.TokenRequest.Method.Should().Be(HttpMethod.Post);

        var form = ParseForm(handler.TokenRequestBody);
        form["grant_type"].Should().Be("password");
        form["client_id"].Should().Be("aonik-spa");
        form["username"].Should().Be("alice@example.com");
        form["password"].Should().Be("secret");
        form["scope"].Should().Be("openid profile");

        result.AccessToken.Should().Be("abc.def.ghi");
        result.RefreshToken.Should().Be("r123");
        result.ExpiresIn.Should().Be(3600);
        result.TokenType.Should().Be("Bearer");
        result.IdToken.Should().Be("id.token.xyz");
    }

    [Fact]
    public async Task ExchangeAsync_Should_UseConfiguredClientId_When_RequestClientIdMissing()
    {
        var handler = new ScriptedHandler
        {
            TokenResponseBody = """{"access_token":"abc","expires_in":60,"token_type":"Bearer"}""",
        };
        var service = CreateService(handler);

        var request = new TokenRequest(
            GrantType: "password",
            ClientId: "   ",
            Username: "alice@example.com",
            Password: "secret",
            Scope: null,
            RedirectUri: null,
            CodeVerifier: null,
            AuthorizationCode: null,
            RefreshToken: null);

        await service.ExchangeAsync(request, CancellationToken.None);

        var form = ParseForm(handler.TokenRequestBody);
        form["client_id"].Should().Be("aonik-spa");
    }

    [Fact]
    public async Task ExchangeAsync_Should_OmitNullAndWhitespaceFields_FromFormBody()
    {
        var handler = new ScriptedHandler
        {
            TokenResponseBody = """{"access_token":"abc","expires_in":60,"token_type":"Bearer"}""",
        };
        var service = CreateService(handler);

        var request = new TokenRequest(
            GrantType: "refresh_token",
            ClientId: "aonik-spa",
            Username: null,
            Password: null,
            Scope: null,
            RedirectUri: null,
            CodeVerifier: null,
            AuthorizationCode: null,
            RefreshToken: "r0");

        await service.ExchangeAsync(request, CancellationToken.None);

        var form = ParseForm(handler.TokenRequestBody);
        form.AllKeys.Should().BeEquivalentTo(new[] { "grant_type", "client_id", "refresh_token" });
        form["grant_type"].Should().Be("refresh_token");
        form["client_id"].Should().Be("aonik-spa");
        form["refresh_token"].Should().Be("r0");
    }

    [Fact]
    public async Task ExchangeAsync_Should_IncludeClientSecret_When_Configured()
    {
        var handler = new ScriptedHandler
        {
            TokenResponseBody = """{"access_token":"abc","expires_in":60,"token_type":"Bearer"}""",
        };
        var service = CreateService(handler, clientSecret: "shh");

        var request = new TokenRequest(
            GrantType: "refresh_token",
            ClientId: "aonik-spa",
            Username: null,
            Password: null,
            Scope: null,
            RedirectUri: null,
            CodeVerifier: null,
            AuthorizationCode: null,
            RefreshToken: "r0");

        await service.ExchangeAsync(request, CancellationToken.None);

        var form = ParseForm(handler.TokenRequestBody);
        form["client_secret"].Should().Be("shh");
    }

    [Fact]
    public async Task ExchangeAsync_Should_OmitClientSecret_When_NotConfigured()
    {
        var handler = new ScriptedHandler
        {
            TokenResponseBody = """{"access_token":"abc","expires_in":60,"token_type":"Bearer"}""",
        };
        var service = CreateService(handler);

        var request = new TokenRequest(
            GrantType: "refresh_token",
            ClientId: "aonik-spa",
            Username: null,
            Password: null,
            Scope: null,
            RedirectUri: null,
            CodeVerifier: null,
            AuthorizationCode: null,
            RefreshToken: "r0");

        await service.ExchangeAsync(request, CancellationToken.None);

        var form = ParseForm(handler.TokenRequestBody);
        form.AllKeys.Should().NotContain("client_secret");
    }

    [Fact]
    public async Task ExchangeAsync_Should_Throw_When_KeycloakReturnsError()
    {
        var handler = new ScriptedHandler
        {
            TokenResponseStatusCode = HttpStatusCode.BadRequest,
            TokenResponseBody = """{"error":"invalid_grant"}""",
        };
        var service = CreateService(handler);

        var request = new TokenRequest(
            GrantType: "password",
            ClientId: "aonik-spa",
            Username: "x",
            Password: "y",
            Scope: null,
            RedirectUri: null,
            CodeVerifier: null,
            AuthorizationCode: null,
            RefreshToken: null);

        var act = async () => await service.ExchangeAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*invalid_grant*");
    }

    [Fact]
    public async Task ExchangeAsync_Should_Throw_When_AccessTokenMissing()
    {
        var handler = new ScriptedHandler
        {
            TokenResponseBody = """{"token_type":"Bearer","expires_in":3600}""",
        };
        var service = CreateService(handler);

        var request = new TokenRequest(
            GrantType: "password",
            ClientId: "aonik-spa",
            Username: "x",
            Password: "y",
            Scope: null,
            RedirectUri: null,
            CodeVerifier: null,
            AuthorizationCode: null,
            RefreshToken: null);

        var act = async () => await service.ExchangeAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ExchangeAsync_Should_PassAuthorizationCodeGrant_With_CodeVerifier()
    {
        var handler = new ScriptedHandler
        {
            TokenResponseBody = """{"access_token":"abc","expires_in":60,"token_type":"Bearer"}""",
        };
        var service = CreateService(handler);

        var request = new TokenRequest(
            GrantType: "authorization_code",
            ClientId: "aonik-spa",
            Username: null,
            Password: null,
            Scope: "openid",
            RedirectUri: "http://localhost:5173/callback",
            CodeVerifier: "verifier-abc",
            AuthorizationCode: "auth-code-xyz",
            RefreshToken: null);

        await service.ExchangeAsync(request, CancellationToken.None);

        var form = ParseForm(handler.TokenRequestBody);
        form["grant_type"].Should().Be("authorization_code");
        form["code"].Should().Be("auth-code-xyz");
        form["code_verifier"].Should().Be("verifier-abc");
        form["redirect_uri"].Should().Be("http://localhost:5173/callback");
        form["scope"].Should().Be("openid");
        form.AllKeys.Should().NotContain("username");
        form.AllKeys.Should().NotContain("password");
        form.AllKeys.Should().NotContain("refresh_token");
    }

    private static KeycloakAuthTokenService CreateService(ScriptedHandler handler, string? clientSecret = null)
    {
        var httpClient = new HttpClient(handler);
        var settings = new InMemorySettingProvider();
        settings.Set(AuthSettingNames.KeycloakAuthority, Authority);
        settings.Set(AuthSettingNames.KeycloakClientId, "aonik-spa");
        if (!string.IsNullOrEmpty(clientSecret))
        {
            settings.Set(AuthSettingNames.KeycloakClientSecret, clientSecret);
        }

        return new KeycloakAuthTokenService(httpClient, settings);
    }

    private static System.Collections.Specialized.NameValueCollection ParseForm(string body)
        => HttpUtility.ParseQueryString(body);

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        public HttpRequestMessage? TokenRequest { get; private set; }
        public string TokenRequestBody { get; private set; } = string.Empty;

        public HttpStatusCode TokenResponseStatusCode { get; set; } = HttpStatusCode.OK;
        public string TokenResponseBody { get; set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            TokenRequest = request;
            TokenRequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(TokenResponseStatusCode)
            {
                Content = new StringContent(TokenResponseBody, Encoding.UTF8, "application/json"),
            };
        }
    }
}
