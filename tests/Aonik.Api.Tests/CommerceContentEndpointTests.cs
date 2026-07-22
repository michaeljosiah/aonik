using System.Net;
using System.Net.Http.Json;

using Aonik.Commerce.Entities.Catalog;
using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Entities.Identity;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

/// <summary>
/// Spec 067 endpoints over the real DI container: the embedded product-DTO content, the
/// resolution endpoint's bounded caching (A15/A17), 404 paths (A12), and admin authz.
/// </summary>
public class CommerceContentEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CommerceContentEndpointTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task ProductDto_Should_EmbedResolvedContent_AndNullWhenUnauthored()
    {
        // §8 — the product page renders its panels from the first call; A12 — absence is null,
        // never an empty panel presented as fact.
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        var productId = await SeedProductAsync(tenantId, "jollof");
        var admin = await AdminClient(tenantId);
        var anonymous = AnonymousClient(tenantId);

        (await anonymous.GetFromJsonAsync<ProductResponse>("/commerce/catalog/products/jollof"))!
            .Content.Should().BeNull("no default block is authored yet");

        var upsert = await admin.PutAsJsonAsync($"/commerce/admin/products/{productId}/content", new
        {
            servingLabel = "Standard 300g",
            kcal = 500m,
            ingredients = "Rice",
            allergens = "None",
        });
        upsert.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await anonymous.GetFromJsonAsync<ProductResponse>("/commerce/catalog/products/jollof");
        dto!.Content.Should().NotBeNull();
        dto.Content!.ServingLabel.Should().Be("Standard 300g");
        dto.Content.IsStandardPreparation.Should().BeFalse();
        dto.ContentVersion.Should().Be(dto.Content.ContentVersion);
    }

    [Fact]
    public async Task ContentEndpoint_Should_ApplyTheVersionedCacheRules()
    {
        // A17 — only v == current gets public,max-age; absent, stale AND FUTURE values get
        // no-store, so an anonymous caller cannot pre-poison a shared cache under the URL a
        // later correction will occupy. A14 — tenant-partitioned via Vary.
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        var productId = await SeedProductAsync(tenantId, "jollof");
        var admin = await AdminClient(tenantId);
        await admin.PutAsJsonAsync($"/commerce/admin/products/{productId}/content", new
        {
            servingLabel = "Standard 300g",
            kcal = 500m,
        });
        var anonymous = AnonymousClient(tenantId);

        var current = (await anonymous.GetFromJsonAsync<ProductResponse>("/commerce/catalog/products/jollof"))!.ContentVersion!.Value;

        var matched = await anonymous.GetAsync($"/commerce/catalog/products/jollof/content?v={current}");
        matched.Headers.CacheControl!.Public.Should().BeTrue();
        matched.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromMinutes(5));
        matched.Headers.Vary.Should().Contain("X-Tenant-Id");

        foreach (var v in new[] { $"{current + 1}", $"{current - 1}", "" })
        {
            var query = v.Length == 0 ? "" : $"?v={v}";
            var response = await anonymous.GetAsync($"/commerce/catalog/products/jollof/content{query}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Headers.CacheControl!.NoStore.Should().BeTrue($"v='{v}' must never be cacheable");
        }
    }

    [Fact]
    public async Task ContentEndpoint_Should_Return404_WhenNoBlockExists_AndRejectBadSelectionJson()
    {
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        await SeedProductAsync(tenantId, "jollof");
        var anonymous = AnonymousClient(tenantId);

        (await anonymous.GetAsync("/commerce/catalog/products/jollof/content"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await anonymous.GetAsync("/commerce/catalog/products/jollof/content?selection=%7Bnot-json"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ACorrection_Should_ChangeTheAdvertisedVersion()
    {
        // A15 — the changed version changes the URL the storefront requests, so previously
        // cached responses become unreachable rather than stale-served.
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        var productId = await SeedProductAsync(tenantId, "jollof");
        var admin = await AdminClient(tenantId);
        var anonymous = AnonymousClient(tenantId);

        await admin.PutAsJsonAsync($"/commerce/admin/products/{productId}/content",
            new { servingLabel = "Standard 300g", allergens = "None" });
        var v1 = (await anonymous.GetFromJsonAsync<ProductResponse>("/commerce/catalog/products/jollof"))!.ContentVersion;

        await admin.PutAsJsonAsync($"/commerce/admin/products/{productId}/content",
            new { servingLabel = "Standard 300g", allergens = "Mustard" });
        var v2 = (await anonymous.GetFromJsonAsync<ProductResponse>("/commerce/catalog/products/jollof"))!.ContentVersion;

        v2.Should().BeGreaterThan(v1!.Value);
    }

    [Fact]
    public async Task AdminContentEndpoints_Should_RejectAnonymousCallers()
    {
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        var productId = await SeedProductAsync(tenantId, "jollof");
        var anonymous = AnonymousClient(tenantId);

        var upsert = await anonymous.PutAsJsonAsync($"/commerce/admin/products/{productId}/content", new { servingLabel = "X" });
        var coverage = await anonymous.GetAsync($"/commerce/admin/products/{productId}/content-coverage");

        upsert.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        coverage.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    // ─── Seeding ─────────────────────────────────────────────────────────────

    private HttpClient AnonymousClient(Guid tenantId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }

    private Task<HttpClient> AdminClient(Guid tenantId)
        => _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithRoles("Operations").WithTenant(tenantId));

    private async Task SeedTenantAsync(Guid tenantId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        if (await db.Tenants.AnyAsync(t => t.Id == tenantId)) return;

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Content Test Tenant",
            Environment = "Testing",
            DefaultCurrency = "GBP",
            SupportedCountriesJson = "[]",
            Status = TenantStatus.Active,
        });
        await db.SaveChangesAsync();
    }

    private async Task<Guid> SeedProductAsync(Guid tenantId, string slug)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;

        var productId = Guid.NewGuid();
        db.Products.Add(new Product
        {
            Id = productId,
            TenantId = tenantId,
            Slug = slug,
            Name = slug,
            Description = "A dish",
            Kind = ProductKinds.Simple,
            Status = ProductStatuses.Active,
        });
        await db.SaveChangesAsync();
        return productId;
    }

    // ─── Response shapes ─────────────────────────────────────────────────────

    private sealed record ProductResponse(string Slug, ContentResponse? Content, int? ContentVersion);

    private sealed record ContentResponse(string ServingLabel, bool IsStandardPreparation, int ContentVersion);
}
