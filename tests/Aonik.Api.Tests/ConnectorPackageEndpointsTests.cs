using System.Net;
using System.Net.Http.Json;

using FluentAssertions;

using Aonik.Agents.Contracts.Models.Tenant;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Api.Tests;

public sealed class ConnectorPackageEndpointsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Catalog_Should_BeEmptyByDefault_AndRequireCurrentReadAuthority()
    {
        using HttpClient anonymous = factory.CreateClient();
        (await anonymous.GetAsync("/v1/integrations/catalog/installations")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using HttpClient denied = await ClientAsync();
        (await denied.GetAsync("/v1/integrations/catalog/installations")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using HttpClient reader = await ClientAsync("Integration.Read");
        (await reader.GetFromJsonAsync<List<ConnectorPackageSummary>>("/v1/integrations/catalog/packages")).Should().BeEmpty();
        (await reader.GetFromJsonAsync<List<ConnectorPackageInstallationItem>>("/v1/integrations/catalog/installations")).Should().BeEmpty();
    }

    [Fact]
    public async Task Import_Should_RequireTenantPermissions_AndRejectUnavailableReleasesWithoutInstallingAnything()
    {
        var request = new ImportConnectorPackageRequest("unprovisioned", "1.0.0", new string('a', 64));
        using HttpClient denied = await ClientAsync("Integration.Read");
        (await denied.PostAsJsonAsync("/v1/integrations/catalog/packages/import", request)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using HttpClient manager = await ClientAsync("Integration.Read", "Integration.Manage", "Workflow.Write");
        (await manager.PostAsJsonAsync("/v1/integrations/catalog/packages/import", request)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await manager.GetFromJsonAsync<List<ConnectorPackageInstallationItem>>("/v1/integrations/catalog/installations")).Should().BeEmpty();
    }

    [Fact]
    public async Task Lifecycle_Should_RequirePlatformReview_AndNotRevealForeignInstallationIds()
    {
        using HttpClient manager = await ClientAsync("Integration.Read", "Integration.Manage", "Workflow.Write");
        string path = $"/v1/integrations/catalog/installations/{Guid.NewGuid()}";
        (await manager.PostAsJsonAsync(path + "/submit", new ConnectorPackageInstallationCommand(""))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await manager.PostAsJsonAsync(path + "/activate", new ConnectorPackageInstallationCommand(""))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await manager.PostAsJsonAsync(path + "/disable", new ConnectorPackageInstallationCommand(""))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await manager.PostAsJsonAsync(path + "/review", new ReviewConnectorPackageInstallationRequest("", true))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<HttpClient> ClientAsync(params string[] permissions)
    {
        Guid tenantId = Guid.NewGuid();
        HttpClient client = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenantId).WithRoles("PersonalUser").WithPermissions(permissions));
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }
}
