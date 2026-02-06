using System.Net;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Aonik.Domain.Identity.Entities;
using Aonik.Infrastructure.Persistence;

namespace Aonik.Api.Tests;

public class PublicCatalogEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PublicCatalogEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetPublicCatalogCountries_ShouldResolveTenantFromHeader_ForAnonymousRequest()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // Act
        var response = await client.GetAsync("/public/catalog/countries");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task SeedTenantAsync(Guid tenantId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AonikDbContext>();

        var existingTenant = await dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
        if (existingTenant != null)
        {
            return;
        }

        dbContext.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Payabo Test Tenant",
            Environment = "Testing",
            DefaultCurrency = "USD",
            SupportedCountriesJson = "[]",
            Status = TenantStatus.Active
        });

        await dbContext.SaveChangesAsync();
    }
}
