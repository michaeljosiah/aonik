using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Aonik.Infrastructure.Authentication.Provisioning;
using Aonik.Platform.Contracts.Models.Authentication;
using Aonik.Platform.Contracts.Services.Registration;
using Aonik.Platform.Services.Settings;
using FluentAssertions;

namespace Aonik.Infrastructure.Tests.Authentication;

/// <summary>
/// Spec 029 — covers the create-user POST shape and the Location-header
/// extraction that Keycloak uses to return the new user id (the response
/// body is empty, so the canonical id lives in the Location header).
/// </summary>
public class KeycloakUserProvisionerTests
{
    private const string Authority = "http://keycloak.local/realms/aonik";

    [Fact]
    public async Task CreateUserAsync_Should_PostUserRepresentation_AndExtractIdFromLocationHeader()
    {
        var newUserId = "abc123-de45-6789-1011-121314151617";
        var handler = new ScriptedHandler
        {
            TokenResponseBody = """{"access_token":"admin-token","expires_in":3600,"token_type":"Bearer"}""",
            CreateUserStatusCode = HttpStatusCode.Created,
            CreateUserLocationHeader = new Uri($"http://keycloak.local/admin/realms/aonik/users/{newUserId}"),
        };
        var provisioner = CreateProvisioner(handler);

        var registration = new IdpUserRegistration(
            Email: "alice@example.com",
            Password: "Initial!Pass123",
            FirstName: "Alice",
            LastName: "Smith",
            Phone: null);

        var result = await provisioner.CreateUserAsync(registration, CancellationToken.None);

        result.ExternalIssuer.Should().Be(Authority);
        result.ExternalSubject.Should().Be(newUserId);
        result.ExternalTenantId.Should().Be("aonik");

        handler.CreateUserRequest.Should().NotBeNull();
        handler.CreateUserRequest!.RequestUri!.AbsoluteUri.Should()
            .Be("http://keycloak.local/admin/realms/aonik/users");

        // Body inspection — ensure we send the standard Keycloak user
        // representation with an initial password credential.
        using var doc = JsonDocument.Parse(handler.CreateUserRequestBody);
        doc.RootElement.GetProperty("email").GetString().Should().Be("alice@example.com");
        doc.RootElement.GetProperty("username").GetString().Should().Be("alice@example.com");
        doc.RootElement.GetProperty("firstName").GetString().Should().Be("Alice");
        doc.RootElement.GetProperty("lastName").GetString().Should().Be("Smith");
        doc.RootElement.GetProperty("enabled").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("emailVerified").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("credentials")[0].GetProperty("type").GetString().Should().Be("password");
        doc.RootElement.GetProperty("credentials")[0].GetProperty("value").GetString().Should().Be("Initial!Pass123");
        doc.RootElement.GetProperty("credentials")[0].GetProperty("temporary").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task CreateUserAsync_Should_ThrowConflict_When_EmailAlreadyExists()
    {
        var handler = new ScriptedHandler
        {
            TokenResponseBody = """{"access_token":"admin-token","expires_in":3600,"token_type":"Bearer"}""",
            CreateUserStatusCode = HttpStatusCode.Conflict,
            CreateUserResponseBody = """{"errorMessage":"User exists with same email"}""",
        };
        var provisioner = CreateProvisioner(handler);

        var registration = new IdpUserRegistration("dup@example.com", "p", "D", "U", null);

        var act = async () => await provisioner.CreateUserAsync(registration, CancellationToken.None);

        await act.Should().ThrowAsync<RegistrationConflictException>();
    }

    [Fact]
    public async Task CreateUserAsync_Should_Throw_When_LocationHeaderMissing()
    {
        var handler = new ScriptedHandler
        {
            TokenResponseBody = """{"access_token":"admin-token","expires_in":3600,"token_type":"Bearer"}""",
            CreateUserStatusCode = HttpStatusCode.Created,
            CreateUserLocationHeader = null,
        };
        var provisioner = CreateProvisioner(handler);

        var act = async () => await provisioner.CreateUserAsync(
            new IdpUserRegistration("x@example.com", "p", "F", "L", null),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Location header*");
    }

    private static KeycloakUserProvisioner CreateProvisioner(ScriptedHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var settings = new InMemorySettingProvider();
        settings.Set(AuthSettingNames.KeycloakAuthority, Authority);
        settings.Set(AuthSettingNames.KeycloakRealm, "aonik");
        settings.Set(AuthSettingNames.KeycloakAdminClientId, "aonik-admin");
        settings.Set(AuthSettingNames.KeycloakAdminClientSecret, "secret");

        return new KeycloakUserProvisioner(httpClient, settings);
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        public HttpRequestMessage? CreateUserRequest { get; private set; }
        public string CreateUserRequestBody { get; private set; } = string.Empty;

        public HttpStatusCode TokenResponseStatusCode { get; set; } = HttpStatusCode.OK;
        public string TokenResponseBody { get; set; } = string.Empty;
        public HttpStatusCode CreateUserStatusCode { get; set; } = HttpStatusCode.Created;
        public string CreateUserResponseBody { get; set; } = string.Empty;
        public Uri? CreateUserLocationHeader { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/protocol/openid-connect/token"))
            {
                return new HttpResponseMessage(TokenResponseStatusCode)
                {
                    Content = new StringContent(TokenResponseBody, Encoding.UTF8, "application/json"),
                };
            }

            CreateUserRequest = request;
            CreateUserRequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            var response = new HttpResponseMessage(CreateUserStatusCode)
            {
                Content = new StringContent(CreateUserResponseBody, Encoding.UTF8, "application/json"),
            };
            if (CreateUserLocationHeader is not null)
            {
                response.Headers.Location = CreateUserLocationHeader;
            }
            return response;
        }
    }
}
