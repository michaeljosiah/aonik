using System.Net;

using FluentAssertions;

namespace Aonik.Api.Tests;

/// <summary>
/// Guards the FastEndpoints assembly list in <c>Program.cs</c> (Spec 097 §5 / P1). Endpoints in
/// <c>Aonik.Subscriptions</c> are only discovered when the assembly is enumerated there explicitly;
/// if it drops out of the list again the routes vanish silently at startup and every call 404s.
/// </summary>
public class SubscriptionsEndpointDiscoveryTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SubscriptionsEndpointDiscoveryTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task ListMeters_Should_BeRouted_When_CallerIsAdmin()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithRoles("Operations").WithTenant(tenantId));

        // Act
        var response = await client.GetAsync("/subscriptions/admin/meters");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the Subscriptions assembly must be in the FastEndpoints assembly list so its endpoints are discovered");
    }

    [Fact]
    public async Task ListMeters_Should_BeRoutedAndDenied_When_CallerIsAnonymous()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/subscriptions/admin/meters");

        // Assert — the route exists (not 404); the policy, not routing, rejects the caller.
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }
}
