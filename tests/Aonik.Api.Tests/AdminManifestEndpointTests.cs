using System.Net;
using System.Net.Http.Json;

using FluentAssertions;

using Aonik.Platform.Endpoints.Admin.Manifest;

namespace Aonik.Api.Tests;

public class AdminManifestEndpointTests
{
    [Fact]
    public async Task AdminManifest_ShouldAllowAnonymousAccess()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/admin/manifest");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<AdminManifestResponse>();
        payload.Should().NotBeNull();
        payload!.EnabledModules.Should().Contain("core");
        payload.EnabledModules.Should().Contain("platform");
        payload.EnabledModules.Should().Contain("finance");
    }
}
