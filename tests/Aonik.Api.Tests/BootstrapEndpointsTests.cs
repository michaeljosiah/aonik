using System.Net;
using System.Net.Http.Json;

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

using Aonik.Platform.Contracts.Api.Bootstrap;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Persistence;

namespace Aonik.Api.Tests;

public class BootstrapEndpointsTests
{
    [Fact]
    public async Task BootstrapStatus_ShouldReturnReady_WhenNoTenantExists()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetFromJsonAsync<BootstrapStatusResponse>("/bootstrap/status");

        // Assert
        response.Should().NotBeNull();
        response!.State.Should().Be("ready");
        response.BootstrapEnabled.Should().BeTrue();
        response.SetupSecretConfigured.Should().BeTrue();
        response.CanBootstrap.Should().BeTrue();
        response.TenantCount.Should().Be(0);
    }

    [Fact]
    public async Task Bootstrap_ShouldCreateTenantAndPendingOwner_WhenInstallCodeIsValid()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var request = new BootstrapInitializeRequest(
            SetupSecret: "test-install-code",
            OwnerEmail: "owner@example.com",
            OwnerDisplayName: "Bootstrap Owner");

        // Act
        var response = await client.PostAsJsonAsync("/bootstrap", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<BootstrapTenantResult>();
        payload.Should().NotBeNull();
        payload!.TenantCreated.Should().BeTrue();
        payload.PlatformAdminAssigned.Should().BeTrue();
        payload.TenantAdminAssigned.Should().BeTrue();
        payload.OwnerEmail.Should().Be("owner@example.com");
        payload.RequiresIdentityLink.Should().BeTrue();

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var tenant = await dbContext.Tenants.FindAsync(payload.TenantId);
        tenant.Should().NotBeNull();
        tenant!.Name.Should().Be("Bootstrap Test Tenant");
        tenant.Status.Should().Be("Active");

        var user = await dbContext.Users.FindAsync(payload.UserId);
        user.Should().NotBeNull();
        user!.Email.Should().Be("owner@example.com");
        user.ExternalIssuer.Should().Be("aonik-bootstrap");
    }

    [Fact]
    public async Task Bootstrap_ShouldRejectInvalidInstallCode()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var request = new BootstrapInitializeRequest(
            SetupSecret: "wrong-code",
            OwnerEmail: "owner@example.com",
            OwnerDisplayName: null);

        // Act
        var response = await client.PostAsJsonAsync("/bootstrap", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        payload.Should().NotBeNull();
        payload!["error"].Should().Contain("install code");
    }
}
