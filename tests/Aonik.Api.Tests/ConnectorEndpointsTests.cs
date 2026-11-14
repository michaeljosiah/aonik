using System.Net;
using System.Net.Http.Json;

using FluentAssertions;

using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Api.Tests;

public class ConnectorEndpointsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Requests_Should_Deduplicate_Cancel_AndKeepTenantAndUserBoundaries()
    {
        Guid tenantId = Guid.NewGuid();
        using HttpClient client = await ClientAsync(tenantId, "Integration.Read", "Integration.Use", "Integration.Request");
        var request = new CreateConnectionRequest("gmail", "User", [new(GmailActionKeys.Search, 1)], "Invoice correspondence", "api-request-1");
        HttpResponseMessage response = await client.PostAsJsonAsync("/v1/integrations/connection-requests", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        ConnectionRequestDetail created = (await response.Content.ReadFromJsonAsync<ConnectionRequestDetail>())!;
        var replay = await (await client.PostAsJsonAsync("/v1/integrations/connection-requests", request)).Content.ReadFromJsonAsync<ConnectionRequestDetail>();
        replay!.Id.Should().Be(created.Id);
        created.Status.Should().Be("Pending");
        created.ReasonCode.Should().Be("ConnectorUnavailable");
        created.CanConnect.Should().BeFalse();
        created.ReviewPath.Should().StartWith("/settings/my-connections?requestId=");
        var list = await client.GetFromJsonAsync<List<ConnectionRequestDetail>>("/v1/integrations/connection-requests");
        list!.Should().ContainSingle(item => item.Id == created.Id);
        using HttpClient outsider = await ClientAsync(tenantId, "Integration.Read", "Integration.Use", "Integration.Request");
        (await outsider.GetAsync($"/v1/integrations/connection-requests/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        using HttpClient otherTenant = await ClientAsync(Guid.NewGuid(), "Integration.Read", "Integration.Use", "Integration.Request");
        (await otherTenant.GetAsync($"/v1/integrations/connection-requests/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsJsonAsync("/v1/integrations/connection-requests", request with { Purpose = "Different content" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        HttpResponseMessage cancelled = await client.PostAsJsonAsync($"/v1/integrations/connection-requests/{created.Id}/cancel", new { created.ExpectedVersion });
        cancelled.StatusCode.Should().Be(HttpStatusCode.OK);
        (await cancelled.Content.ReadFromJsonAsync<ConnectionRequestDetail>())!.Status.Should().Be("Cancelled");
        (await client.PostAsJsonAsync($"/v1/integrations/connection-requests/{created.Id}/consent", new { created.ExpectedVersion })).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CatalogAndReadiness_Should_ExposeContracts_WithoutCredentialsOrImplicitProvisioning()
    {
        using HttpClient client = await ClientAsync(Guid.NewGuid(), "Integration.Read", "Integration.Use");
        var catalog = await client.GetFromJsonAsync<List<ConnectorDescriptor>>("/v1/integrations/connectors");
        ConnectorDescriptor gmail = catalog!.Single(item => item.Key == "gmail");
        gmail.Actions.Should().HaveCount(3);
        gmail.Actions.Should().OnlyContain(action => action.Approval == null && action.RequiredScopes.Contains(GmailActionKeys.ReadScope));
        var ready = await client.GetFromJsonAsync<ConnectorReadiness>("/v1/integrations/connectors/gmail/readiness?actionKey=gmail.messages.search&version=1");
        ready!.Code.Should().Be("TenantNotEnabled");
        var accounts = await client.GetFromJsonAsync<List<ConnectorConnectionSummary>>("/v1/integrations/connectors/gmail/connections");
        accounts.Should().BeEmpty();
    }

    [Fact]
    public async Task HttpBoundary_Should_RejectUnknownContracts_AndUserPolicyOverrides()
    {
        using HttpClient client = await ClientAsync(Guid.NewGuid(), "Integration.Read", "Integration.Use", "Integration.Request", "Integration.Manage", "Settings.Write");
        var request = new CreateConnectionRequest("gmail", "User", [new(GmailActionKeys.Search, 999)], "Invoice correspondence", "bad-version");
        (await client.PostAsJsonAsync("/v1/integrations/connection-requests", request)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/v1/integrations/connection-requests?take=1000")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await client.PutAsJsonAsync("/v1/settings/user/Integrations.Gmail.Enabled", new { value = "true" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ConnectorEndpoints_Should_RequireAuthenticationAndPermissions_AndCallbackOwnerProof()
    {
        using HttpClient anonymous = factory.CreateClient();
        (await anonymous.GetAsync("/v1/integrations/connectors")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using HttpClient denied = await ClientAsync(Guid.NewGuid());
        (await denied.GetAsync("/v1/integrations/connectors")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await denied.GetAsync($"/v1/integrations/connections/{Guid.NewGuid()}/workflow-binding?workflowId={Guid.NewGuid()}&actionKey=gmail.messages.search&version=1")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await anonymous.GetAsync($"/integrations/gmail/oauth/callback?state={Guid.NewGuid():N}.guessed&code=synthetic")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<HttpClient> ClientAsync(Guid tenantId, params string[] permissions)
    {
        HttpClient client = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenantId).WithRoles("PersonalUser").WithPermissions(permissions));
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }
}
