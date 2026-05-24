using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Aonik.Infrastructure.Authentication.PasswordReset;
using Aonik.Platform.Services.Settings;
using FluentAssertions;

namespace Aonik.Infrastructure.Tests.Authentication;

/// <summary>
/// Spec 029 — covers the Keycloak password-reset flow: token acquisition,
/// user lookup by email, and the execute-actions-email PUT carrying
/// <c>["UPDATE_PASSWORD"]</c>. Critically, also asserts the silent no-op
/// when the user is unknown (preserves the no-enumeration contract).
/// </summary>
public class KeycloakPasswordResetServiceTests
{
    private const string Authority = "http://keycloak.local/realms/aonik";
    private const string Realm = "aonik";
    private const string AdminClientId = "aonik-admin";
    private const string AdminClientSecret = "test-secret";

    [Fact]
    public async Task TriggerResetAsync_Should_PostExecuteActionsEmail_When_UserFound()
    {
        var foundUserId = "abc123-de45-6789-1011-121314151617";
        var handler = new ScriptedHandler
        {
            TokenResponseBody = """{"access_token":"admin-token","expires_in":3600,"token_type":"Bearer"}""",
            LookupStatusCode = HttpStatusCode.OK,
            LookupResponseBody = "[{\"id\":\"" + foundUserId + "\",\"email\":\"alice@example.com\"}]",
            ExecuteActionsStatusCode = HttpStatusCode.NoContent,
        };
        var service = CreateService(handler);

        await service.TriggerResetAsync("alice@example.com", Guid.NewGuid(), CancellationToken.None);

        // Lookup URL — must include exact=true and url-encoded email.
        handler.LookupRequest.Should().NotBeNull();
        handler.LookupRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LookupRequest!.RequestUri!.AbsolutePath.Should()
            .Be($"/admin/realms/{Realm}/users");
        handler.LookupRequest!.RequestUri!.Query.Should()
            .Contain("email=alice%40example.com")
            .And.Contain("exact=true");
        handler.LookupRequest!.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.LookupRequest!.Headers.Authorization!.Parameter.Should().Be("admin-token");

        // PUT URL targets the discovered user id.
        handler.ExecuteActionsRequest.Should().NotBeNull();
        handler.ExecuteActionsRequest!.Method.Should().Be(HttpMethod.Put);
        handler.ExecuteActionsRequest!.RequestUri!.AbsoluteUri.Should()
            .Be($"http://keycloak.local/admin/realms/{Realm}/users/{foundUserId}/execute-actions-email");
        handler.ExecuteActionsRequest!.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.ExecuteActionsRequest!.Headers.Authorization!.Parameter.Should().Be("admin-token");

        // Body must be the JSON array ["UPDATE_PASSWORD"].
        using var doc = JsonDocument.Parse(handler.ExecuteActionsRequestBody);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().Should().Be(1);
        doc.RootElement[0].GetString().Should().Be("UPDATE_PASSWORD");
    }

    [Fact]
    public async Task TriggerResetAsync_Should_ReturnSilently_When_UserNotFound()
    {
        var handler = new ScriptedHandler
        {
            TokenResponseBody = """{"access_token":"admin-token","expires_in":3600,"token_type":"Bearer"}""",
            LookupStatusCode = HttpStatusCode.OK,
            LookupResponseBody = "[]",
        };
        var service = CreateService(handler);

        // Silent success — preserves the no-enumeration contract.
        await service.TriggerResetAsync("ghost@example.com", Guid.NewGuid(), CancellationToken.None);

        handler.PutCount.Should().Be(0);
        handler.ExecuteActionsRequest.Should().BeNull();
    }

    [Fact]
    public async Task TriggerResetAsync_Should_Throw_When_UserLookupFails()
    {
        var handler = new ScriptedHandler
        {
            TokenResponseBody = """{"access_token":"admin-token","expires_in":3600,"token_type":"Bearer"}""",
            LookupStatusCode = HttpStatusCode.InternalServerError,
            LookupResponseBody = "boom",
        };
        var service = CreateService(handler);

        var act = async () => await service.TriggerResetAsync(
            "alice@example.com", Guid.NewGuid(), CancellationToken.None);

        // HttpStatusCode.ToString() yields the symbolic name "InternalServerError"
        // (the 500-class label), and the response body is appended verbatim — so
        // the exception message carries the status and the upstream payload.
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.And.Message.Should().Contain("InternalServerError");
        ex.And.Message.Should().Contain("boom");
    }

    [Fact]
    public async Task TriggerResetAsync_Should_Throw_When_ExecuteActionsEmailFails()
    {
        var handler = new ScriptedHandler
        {
            TokenResponseBody = """{"access_token":"admin-token","expires_in":3600,"token_type":"Bearer"}""",
            LookupStatusCode = HttpStatusCode.OK,
            LookupResponseBody = """[{"id":"u-123","email":"alice@example.com"}]""",
            ExecuteActionsStatusCode = HttpStatusCode.InternalServerError,
            ExecuteActionsResponseBody = "smtp_down",
        };
        var service = CreateService(handler);

        var act = async () => await service.TriggerResetAsync(
            "alice@example.com", Guid.NewGuid(), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.And.Message.Should().Contain("InternalServerError");
        ex.And.Message.Should().Contain("smtp_down");
    }

    [Fact]
    public async Task TriggerResetAsync_Should_PassTenantIdThrough_WithoutModification()
    {
        // The tenantId argument is part of the IIdpPasswordResetService contract
        // but the Keycloak implementation does not propagate it into URLs or
        // headers — confirm the call succeeds regardless of the value supplied.
        var handler = new ScriptedHandler
        {
            TokenResponseBody = """{"access_token":"admin-token","expires_in":3600,"token_type":"Bearer"}""",
            LookupStatusCode = HttpStatusCode.OK,
            LookupResponseBody = """[{"id":"u-tenant","email":"bob@example.com"}]""",
            ExecuteActionsStatusCode = HttpStatusCode.NoContent,
        };
        var service = CreateService(handler);

        var arbitraryTenantId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        await service.TriggerResetAsync("bob@example.com", arbitraryTenantId, CancellationToken.None);

        // Verify nothing about the tenant id leaked into the wire shape.
        handler.LookupRequest!.RequestUri!.AbsolutePath.Should()
            .Be($"/admin/realms/{Realm}/users");
        handler.LookupRequest!.RequestUri!.Query.Should()
            .NotContain(arbitraryTenantId.ToString());
        handler.ExecuteActionsRequest!.RequestUri!.AbsoluteUri.Should()
            .Be($"http://keycloak.local/admin/realms/{Realm}/users/u-tenant/execute-actions-email");
        handler.ExecuteActionsRequest!.RequestUri!.AbsoluteUri.Should()
            .NotContain(arbitraryTenantId.ToString());
    }

    private static KeycloakPasswordResetService CreateService(ScriptedHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var settings = new InMemorySettingProvider();
        settings.Set(AuthSettingNames.KeycloakAuthority, Authority);
        settings.Set(AuthSettingNames.KeycloakRealm, Realm);
        settings.Set(AuthSettingNames.KeycloakAdminClientId, AdminClientId);
        settings.Set(AuthSettingNames.KeycloakAdminClientSecret, AdminClientSecret);

        return new KeycloakPasswordResetService(httpClient, settings);
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        public HttpRequestMessage? TokenRequest { get; private set; }
        public HttpRequestMessage? LookupRequest { get; private set; }
        public HttpRequestMessage? ExecuteActionsRequest { get; private set; }
        public string ExecuteActionsRequestBody { get; private set; } = string.Empty;
        public int PutCount { get; private set; }

        public HttpStatusCode TokenResponseStatusCode { get; set; } = HttpStatusCode.OK;
        public string TokenResponseBody { get; set; } = string.Empty;
        public HttpStatusCode LookupStatusCode { get; set; } = HttpStatusCode.OK;
        public string LookupResponseBody { get; set; } = "[]";
        public HttpStatusCode ExecuteActionsStatusCode { get; set; } = HttpStatusCode.NoContent;
        public string ExecuteActionsResponseBody { get; set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;

            // Token endpoint — POST {realm}/protocol/openid-connect/token
            if (path.EndsWith("/protocol/openid-connect/token"))
            {
                TokenRequest = request;
                return new HttpResponseMessage(TokenResponseStatusCode)
                {
                    Content = new StringContent(TokenResponseBody, Encoding.UTF8, "application/json"),
                };
            }

            // User lookup — GET /admin/realms/{realm}/users?email=...&exact=true
            if (request.Method == HttpMethod.Get && path.EndsWith($"/admin/realms/{Realm}/users"))
            {
                LookupRequest = request;
                return new HttpResponseMessage(LookupStatusCode)
                {
                    Content = new StringContent(LookupResponseBody, Encoding.UTF8, "application/json"),
                };
            }

            // Execute actions email — PUT /admin/realms/{realm}/users/{id}/execute-actions-email
            if (request.Method == HttpMethod.Put && path.EndsWith("/execute-actions-email"))
            {
                PutCount++;
                ExecuteActionsRequest = request;
                ExecuteActionsRequestBody = request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken);

                return new HttpResponseMessage(ExecuteActionsStatusCode)
                {
                    Content = new StringContent(ExecuteActionsResponseBody, Encoding.UTF8, "application/json"),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotImplemented);
        }
    }
}
