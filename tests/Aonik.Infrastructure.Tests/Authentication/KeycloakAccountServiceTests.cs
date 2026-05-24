using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Aonik.Infrastructure.Authentication.Account;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Services.Settings;
using FluentAssertions;

namespace Aonik.Infrastructure.Tests.Authentication;

/// <summary>
/// Spec 029 — covers <see cref="KeycloakAccountService"/>:
/// <list type="bullet">
///   <item><c>ValidatePasswordAsync</c> — direct-grant password POST to the realm
///         token endpoint; 401 → InvalidOperationException.</item>
///   <item><c>UpdateEmailAsync</c> — admin-token then PUT user representation to
///         <c>{root}/admin/realms/{realm}/users/{sub}</c>.</item>
///   <item><c>UpdatePasswordAsync</c> — admin-token then PUT credential to
///         the <c>/reset-password</c> sub-resource.</item>
/// </list>
/// </summary>
public class KeycloakAccountServiceTests
{
    private const string Authority = "http://keycloak.local/realms/aonik";
    private const string Realm = "aonik";
    private const string ClientId = "aonik-spa";
    private const string ClientSecret = "spa-secret";
    private const string AdminClientId = "aonik-admin";
    private const string AdminClientSecret = "admin-secret";
    private const string AdminTokenBody = """{"access_token":"admin-token","expires_in":3600,"token_type":"Bearer"}""";

    [Fact]
    public async Task ValidatePasswordAsync_Should_PostPasswordGrant_When_ValidCredentials()
    {
        var handler = new ScriptedHandler
        {
            TokenResponseStatusCode = HttpStatusCode.OK,
            TokenResponseBody = """{"access_token":"user-token","expires_in":300,"token_type":"Bearer"}""",
        };
        var service = CreateService(handler);

        var user = new User { Email = "alice@example.com", ExternalSubject = "sub-123" };

        var act = async () => await service.ValidatePasswordAsync(user, "secret123", CancellationToken.None);

        await act.Should().NotThrowAsync();

        handler.TokenRequest.Should().NotBeNull();
        handler.TokenRequest!.RequestUri!.AbsoluteUri.Should()
            .Be("http://keycloak.local/realms/aonik/protocol/openid-connect/token");
        handler.TokenRequest!.Method.Should().Be(HttpMethod.Post);

        // FormUrlEncodedContent body — parse k/v pairs.
        var form = ParseFormUrlEncoded(handler.TokenRequestBody);
        form.Should().ContainKey("grant_type").WhoseValue.Should().Be("password");
        form.Should().ContainKey("client_id").WhoseValue.Should().Be(ClientId);
        form.Should().ContainKey("username").WhoseValue.Should().Be("alice@example.com");
        form.Should().ContainKey("password").WhoseValue.Should().Be("secret123");
        form.Should().ContainKey("scope").WhoseValue.Should().Be("openid");
    }

    [Fact]
    public async Task ValidatePasswordAsync_Should_Throw_When_InvalidCredentials()
    {
        var handler = new ScriptedHandler
        {
            TokenResponseStatusCode = HttpStatusCode.Unauthorized,
            TokenResponseBody = """{"error":"invalid_grant"}""",
        };
        var service = CreateService(handler);

        var user = new User { Email = "alice@example.com", ExternalSubject = "sub-123" };

        var act = async () => await service.ValidatePasswordAsync(user, "wrong-password", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Current password is invalid.");
    }

    [Fact]
    public async Task UpdateEmailAsync_Should_PutUserRepresentation_With_NewEmail()
    {
        var handler = new ScriptedHandler
        {
            TokenResponseStatusCode = HttpStatusCode.OK,
            TokenResponseBody = AdminTokenBody,
            AdminPutStatusCode = HttpStatusCode.NoContent,
        };
        var service = CreateService(handler);

        var user = new User { Email = "alice@example.com", ExternalSubject = "user-sub-7" };

        await service.UpdateEmailAsync(user, "bob@example.com", CancellationToken.None);

        handler.UserPutRequest.Should().NotBeNull();
        handler.UserPutRequest!.Method.Should().Be(HttpMethod.Put);
        handler.UserPutRequest!.RequestUri!.AbsoluteUri.Should()
            .Be("http://keycloak.local/admin/realms/aonik/users/user-sub-7");

        handler.UserPutRequest!.Headers.Authorization.Should().NotBeNull();
        handler.UserPutRequest!.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.UserPutRequest!.Headers.Authorization!.Parameter.Should().Be("admin-token");

        using var doc = JsonDocument.Parse(handler.UserPutRequestBody);
        doc.RootElement.GetProperty("email").GetString().Should().Be("bob@example.com");
        doc.RootElement.GetProperty("emailVerified").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("username").GetString().Should().Be("bob@example.com");
    }

    [Fact]
    public async Task UpdateEmailAsync_Should_Throw_When_KeycloakReturnsError()
    {
        var handler = new ScriptedHandler
        {
            TokenResponseStatusCode = HttpStatusCode.OK,
            TokenResponseBody = AdminTokenBody,
            AdminPutStatusCode = HttpStatusCode.InternalServerError,
            AdminPutResponseBody = """{"error":"internal"}""",
        };
        var service = CreateService(handler);

        var user = new User { Email = "alice@example.com", ExternalSubject = "user-sub-7" };

        var act = async () => await service.UpdateEmailAsync(user, "bob@example.com", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*InternalServerError*");
    }

    [Fact]
    public async Task UpdatePasswordAsync_Should_PutResetPassword_Endpoint()
    {
        var handler = new ScriptedHandler
        {
            TokenResponseStatusCode = HttpStatusCode.OK,
            TokenResponseBody = AdminTokenBody,
            AdminPutStatusCode = HttpStatusCode.NoContent,
        };
        var service = CreateService(handler);

        var user = new User { Email = "alice@example.com", ExternalSubject = "user-sub-7" };

        await service.UpdatePasswordAsync(user, "NewPass!2026", CancellationToken.None);

        handler.UserPutRequest.Should().NotBeNull();
        handler.UserPutRequest!.Method.Should().Be(HttpMethod.Put);
        handler.UserPutRequest!.RequestUri!.AbsoluteUri.Should()
            .EndWith("/admin/realms/aonik/users/user-sub-7/reset-password");

        handler.UserPutRequest!.Headers.Authorization.Should().NotBeNull();
        handler.UserPutRequest!.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.UserPutRequest!.Headers.Authorization!.Parameter.Should().Be("admin-token");

        using var doc = JsonDocument.Parse(handler.UserPutRequestBody);
        doc.RootElement.GetProperty("type").GetString().Should().Be("password");
        doc.RootElement.GetProperty("value").GetString().Should().Be("NewPass!2026");
        doc.RootElement.GetProperty("temporary").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task UpdatePasswordAsync_Should_Throw_When_KeycloakReturnsError()
    {
        var handler = new ScriptedHandler
        {
            TokenResponseStatusCode = HttpStatusCode.OK,
            TokenResponseBody = AdminTokenBody,
            AdminPutStatusCode = HttpStatusCode.BadRequest,
            AdminPutResponseBody = """{"error":"weak_password"}""",
        };
        var service = CreateService(handler);

        var user = new User { Email = "alice@example.com", ExternalSubject = "user-sub-7" };

        var act = async () => await service.UpdatePasswordAsync(user, "weak", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*BadRequest*");
    }

    private static KeycloakAccountService CreateService(ScriptedHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var settings = new InMemorySettingProvider();
        settings.Set(AuthSettingNames.KeycloakAuthority, Authority);
        settings.Set(AuthSettingNames.KeycloakRealm, Realm);
        settings.Set(AuthSettingNames.KeycloakClientId, ClientId);
        settings.Set(AuthSettingNames.KeycloakClientSecret, ClientSecret);
        settings.Set(AuthSettingNames.KeycloakAdminClientId, AdminClientId);
        settings.Set(AuthSettingNames.KeycloakAdminClientSecret, AdminClientSecret);

        return new KeycloakAccountService(httpClient, settings);
    }

    private static Dictionary<string, string> ParseFormUrlEncoded(string body)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(body))
        {
            return result;
        }
        foreach (var pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq < 0)
            {
                result[Uri.UnescapeDataString(pair)] = string.Empty;
            }
            else
            {
                var key = Uri.UnescapeDataString(pair[..eq]);
                var value = Uri.UnescapeDataString(pair[(eq + 1)..]);
                result[key] = value;
            }
        }
        return result;
    }

    /// <summary>
    /// Branches on URL + method to fan out to one of three endpoints:
    /// the token endpoint (POST), an admin user PUT, or the reset-password
    /// PUT. ValidatePasswordAsync uses only the token endpoint; the Update
    /// methods hit the token endpoint then a PUT — last captured PUT wins.
    /// </summary>
    private sealed class ScriptedHandler : HttpMessageHandler
    {
        public HttpRequestMessage? TokenRequest { get; private set; }
        public string TokenRequestBody { get; private set; } = string.Empty;

        public HttpRequestMessage? UserPutRequest { get; private set; }
        public string UserPutRequestBody { get; private set; } = string.Empty;

        public HttpStatusCode TokenResponseStatusCode { get; set; } = HttpStatusCode.OK;
        public string TokenResponseBody { get; set; } = string.Empty;

        public HttpStatusCode AdminPutStatusCode { get; set; } = HttpStatusCode.NoContent;
        public string AdminPutResponseBody { get; set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;

            if (path.EndsWith("/protocol/openid-connect/token") && request.Method == HttpMethod.Post)
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

            if (request.Method == HttpMethod.Put && path.Contains("/admin/realms/"))
            {
                UserPutRequest = request;
                UserPutRequestBody = request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken);

                return new HttpResponseMessage(AdminPutStatusCode)
                {
                    Content = new StringContent(AdminPutResponseBody, Encoding.UTF8, "application/json"),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent($"Unexpected {request.Method} {request.RequestUri}", Encoding.UTF8, "text/plain"),
            };
        }
    }
}
