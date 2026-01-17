using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

using Aonik.Api.Contracts.Billing;
using Aonik.Api.Contracts.Identity;

namespace Aonik.Api.Tests;

public class TenantSecurityTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TenantSecurityTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TenantContextMissing_ShouldReturnUnauthorized()
    {
        // Arrange
        var options = TestAuthOptions.Create();
        options.TenantId = null;
        options.WithPermissions("Invoice.Read");



        var client = await _factory.CreateAuthenticatedClientAsync(options);

        // Act
        var response = await client.GetAsync($"/billing/invoices/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TenantIsolation_ShouldPreventAccessAcrossTenants()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var tenantAClient = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create()
                .WithPermissions("Invoice.Create", "Invoice.Read")
                .WithTenant(tenantA));

        var tenantBClient = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create()
                .WithPermissions("Invoice.Read")
                .WithTenant(tenantB));


        var createRequest = new CreateInvoiceRequest(
            CustomerId: Guid.NewGuid(),
            InvoiceNumber: $"INV-{Guid.NewGuid().ToString()[..8]}",
            Currency: "USD",
            DueUtc: DateTime.UtcNow.AddDays(30),
            LineItems: new List<CreateInvoiceLineItemRequest>
            {
                new("Tenant A Service", 1, 250.00m)
            });

        var createResponse = await tenantAClient.PostAsJsonAsync("/billing/invoices", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdInvoice = await createResponse.Content.ReadFromJsonAsync<InvoiceResponse>();

        // Act
        var response = await tenantBClient.GetAsync($"/billing/invoices/{createdInvoice!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PlatformAdminEndpoints_ShouldRejectNonAdmin()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create());

        // Act
        var response = await client.GetAsync("/admin/tenants");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TenantAdminPolicy_ShouldRejectUserWithoutRole()
    {
        // Arrange
        var options = TestAuthOptions.Create()
            .WithPermissions("Users.Read");



        var client = await _factory.CreateAuthenticatedClientAsync(options);

        // Act
        var response = await client.GetAsync($"/tenant/users/{options.UserId}/roles");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TenantAdminPolicy_ShouldAllowTenantAdminRole()
    {
        // Arrange
        var options = TestAuthOptions.Create()
            .WithPermissions("Users.Read")
            .WithRoles("TenantAdmin");



        var client = await _factory.CreateAuthenticatedClientAsync(options);

        // Act
        var response = await client.GetAsync($"/tenant/users/{options.UserId}/roles");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<UserRoleResponse>();
        payload.Should().NotBeNull();
        payload!.UserId.Should().Be(options.UserId);
    }
}
