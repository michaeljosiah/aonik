using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Aonik.Infrastructure.Authentication.Provisioning;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Services.Settings;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Infrastructure.Tests.Authentication;

/// <summary>
/// Spec 029 — verifies KeycloakManagementClient.DeleteUserAsync produces the
/// correct HTTP shape and honours the documented success / failure contract
/// (204 / 404 → success, all other statuses → structured failure).
/// </summary>
public class KeycloakManagementClientTests
{
    private const string Authority = "http://keycloak.local/realms/aonik";
    private const string Realm = "aonik";
    private const string AdminClientId = "aonik-admin";
    private const string AdminClientSecret = "test-secret";
    private const string UserId = "11111111-2222-3333-4444-555555555555";

    [Fact]
    public async Task DeleteUserAsync_Should_ReturnSuccess_When_AdminApiReturns204()
    {
        var handler = new ScriptedHandler
        {
            TokenResponseBody = """{"access_token":"fake-admin-token","expires_in":3600,"token_type":"Bearer"}""",
            DeleteStatusCode = HttpStatusCode.NoContent,
        };
        var client = CreateClient(handler);

        var result = await client.DeleteUserAsync(UserId, null, CancellationToken.None);

        result.Deleted.Should().BeTrue();
        result.FailureReason.Should().BeNull();

        handler.DeleteRequest.Should().NotBeNull();
        handler.DeleteRequest!.RequestUri!.AbsoluteUri.Should().Be(
            $"http://keycloak.local/admin/realms/{Realm}/users/{UserId}");
        handler.DeleteRequest!.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.DeleteRequest!.Headers.Authorization!.Parameter.Should().Be("fake-admin-token");

        handler.TokenRequest.Should().NotBeNull();
        handler.TokenRequest!.RequestUri!.AbsoluteUri.Should().Be(
            "http://keycloak.local/realms/aonik/protocol/openid-connect/token");
    }

    [Fact]
    public async Task DeleteUserAsync_Should_ReturnSuccess_When_UserAlreadyMissing_404()
    {
        var handler = new ScriptedHandler
        {
            TokenResponseBody = """{"access_token":"t","expires_in":3600,"token_type":"Bearer"}""",
            DeleteStatusCode = HttpStatusCode.NotFound,
        };
        var client = CreateClient(handler);

        var result = await client.DeleteUserAsync(UserId, null, CancellationToken.None);

        // 404 is success — mirrors the Auth0 client semantics; the user
        // record is gone, which is the desired end state.
        result.Deleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteUserAsync_Should_ReturnFailure_When_AdminApiReturns500()
    {
        var handler = new ScriptedHandler
        {
            TokenResponseBody = """{"access_token":"t","expires_in":3600,"token_type":"Bearer"}""",
            DeleteStatusCode = HttpStatusCode.InternalServerError,
            DeleteResponseBody = "boom",
        };
        var client = CreateClient(handler);

        var result = await client.DeleteUserAsync(UserId, null, CancellationToken.None);

        result.Deleted.Should().BeFalse();
        result.FailureReason.Should().NotBeNull();
        result.FailureReason.Should().Contain("HTTP 500");
        result.FailureReason.Should().Contain("boom");
    }

    [Fact]
    public async Task DeleteUserAsync_Should_ReturnFailure_When_ExternalSubjectMissing()
    {
        var client = CreateClient(new ScriptedHandler());

        var result = await client.DeleteUserAsync("", null, CancellationToken.None);

        result.Deleted.Should().BeFalse();
        result.FailureReason.Should().Be("external subject is missing");
    }

    [Fact]
    public async Task DeleteUserAsync_Should_ReturnFailure_When_TokenAcquisitionFails()
    {
        var handler = new ScriptedHandler
        {
            TokenResponseStatusCode = HttpStatusCode.Unauthorized,
            TokenResponseBody = "invalid_client",
        };
        var client = CreateClient(handler);

        var result = await client.DeleteUserAsync(UserId, null, CancellationToken.None);

        result.Deleted.Should().BeFalse();
        result.FailureReason.Should().Contain("token acquisition failed");
    }

    private static KeycloakManagementClient CreateClient(ScriptedHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var settings = new InMemorySettingProvider();
        settings.Set(AuthSettingNames.KeycloakAuthority, Authority);
        settings.Set(AuthSettingNames.KeycloakRealm, Realm);
        settings.Set(AuthSettingNames.KeycloakAdminClientId, AdminClientId);
        settings.Set(AuthSettingNames.KeycloakAdminClientSecret, AdminClientSecret);

        return new KeycloakManagementClient(
            httpClient,
            settings,
            NullLogger<KeycloakManagementClient>.Instance);
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        public HttpRequestMessage? TokenRequest { get; private set; }
        public HttpRequestMessage? DeleteRequest { get; private set; }
        public HttpStatusCode TokenResponseStatusCode { get; set; } = HttpStatusCode.OK;
        public string TokenResponseBody { get; set; } = """{"access_token":"x","expires_in":3600,"token_type":"Bearer"}""";
        public HttpStatusCode DeleteStatusCode { get; set; } = HttpStatusCode.NoContent;
        public string DeleteResponseBody { get; set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post)
            {
                TokenRequest = request;
                return Task.FromResult(new HttpResponseMessage(TokenResponseStatusCode)
                {
                    Content = new StringContent(TokenResponseBody, Encoding.UTF8, "application/json"),
                });
            }

            if (request.Method == HttpMethod.Delete)
            {
                DeleteRequest = request;
                return Task.FromResult(new HttpResponseMessage(DeleteStatusCode)
                {
                    Content = new StringContent(DeleteResponseBody, Encoding.UTF8, "text/plain"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotImplemented));
        }
    }
}
