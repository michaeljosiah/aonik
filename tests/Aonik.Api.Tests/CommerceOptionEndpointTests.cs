using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Commerce.Entities.Catalog;
using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Entities.Identity;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

/// <summary>
/// Spec 066 option endpoints over the real DI container. Covers the anonymous catalogue and
/// selection-quote surfaces, the acceptance criteria that matter at the HTTP boundary (A2/A3), and
/// admin authorization.
/// </summary>
public class CommerceOptionEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CommerceOptionEndpointTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetOptionCatalogue_Should_ReturnServableGroups_ForAnonymousRequest()
    {
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        await SeedCatalogueAsync(tenantId);

        var client = AnonymousClient(tenantId);
        var response = await client.GetAsync("/commerce/catalog/options");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<List<OptionGroupResponse>>();
        payload.Should().NotBeNull();
        payload!.Should().ContainSingle(g => g.Key == "portion");
        payload.Single(g => g.Key == "portion").Choices.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetOptionCatalogue_Should_BeTenantIsolated()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await SeedTenantAsync(tenantA);
        await SeedTenantAsync(tenantB);
        await SeedCatalogueAsync(tenantA);

        var response = await AnonymousClient(tenantB).GetAsync("/commerce/catalog/options");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<List<OptionGroupResponse>>()).Should().BeEmpty();
    }

    [Fact]
    public async Task GetProduct_Should_CarryEffectiveOptionGroups_And_Surcharge()
    {
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        await SeedCatalogueAsync(tenantId);
        var productId = await SeedProductAsync(tenantId, "jollof", offerPortion: true, surcharge: 4m);

        var response = await AnonymousClient(tenantId).GetAsync("/commerce/catalog/products/jollof");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ProductResponse>();
        payload.Should().NotBeNull();
        payload!.EffectiveOptionGroups.Should().ContainSingle(g => g.Key == "portion");
        payload.EffectiveOptionGroups.Single().DefaultChoiceKey.Should().Be("light");
        payload.UnitSurcharge.Should().Be(4m);
        payload.UnitSurchargeCurrency.Should().Be("GBP");
        _ = productId;
    }

    [Fact]
    public async Task GetProduct_Should_ReturnEmptyOptionGroups_When_ProductIsNotPersonalisable()
    {
        // A3 — an empty list is the signal for storefronts to hide the panel entirely.
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        await SeedCatalogueAsync(tenantId);
        await SeedProductAsync(tenantId, "plain", offerPortion: false);

        var response = await AnonymousClient(tenantId).GetAsync("/commerce/catalog/products/plain");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ProductResponse>())!.EffectiveOptionGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task SelectionQuote_Should_PriceTheSelection_ForAnonymousRequest()
    {
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        await SeedCatalogueAsync(tenantId);
        await SeedProductAsync(tenantId, "jollof", offerPortion: true);

        var response = await AnonymousClient(tenantId).PostAsJsonAsync(
            "/commerce/catalog/products/jollof/selection-quote",
            new { selection = new { portion = "full" }, currency = "GBP" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<SelectionQuoteResponse>();
        payload.Should().NotBeNull();
        payload!.Adjustment.Should().Be(10m);
        payload.IsDefault.Should().BeFalse();
        payload.CanonicalSelectionJson.Should().Contain("\"portion\":\"full\"");
    }

    [Fact]
    public async Task SelectionQuote_Should_Reject_When_ProductDoesNotOfferTheGroup()
    {
        // A2 at the HTTP boundary — the group exists in the catalogue, the product does not offer it.
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        await SeedCatalogueAsync(tenantId);
        await SeedProductAsync(tenantId, "plain", offerPortion: false);

        var response = await AnonymousClient(tenantId).PostAsJsonAsync(
            "/commerce/catalog/products/plain/selection-quote",
            new { selection = new { portion = "full" }, currency = "GBP" });

        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task SelectionQuote_Should_Return404_When_ProductIsNotActive()
    {
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        await SeedCatalogueAsync(tenantId);
        await SeedProductAsync(tenantId, "draft-dish", offerPortion: true, status: ProductStatuses.Draft);

        var response = await AnonymousClient(tenantId).PostAsJsonAsync(
            "/commerce/catalog/products/draft-dish/selection-quote",
            new { selection = new { portion = "full" }, currency = "GBP" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AdminOptionEndpoints_Should_RejectAnonymousCallers()
    {
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        var client = AnonymousClient(tenantId);

        var create = await client.PostAsJsonAsync(
            "/commerce/admin/option-groups",
            new { key = "portion", label = "Portion", sortOrder = 0 });
        create.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);

        var list = await client.GetAsync("/commerce/admin/option-groups");
        list.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    // ─── Seeding ─────────────────────────────────────────────────────────────

    private HttpClient AnonymousClient(Guid tenantId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }

    private async Task SeedTenantAsync(Guid tenantId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();

        if (await db.Tenants.AnyAsync(t => t.Id == tenantId)) return;

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Commerce Option Test Tenant",
            Environment = "Testing",
            DefaultCurrency = "GBP",
            SupportedCountriesJson = "[]",
            Status = TenantStatus.Active,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>portion: light* (0) / full (+10), in GBP. * = recommended default.</summary>
    private async Task SeedCatalogueAsync(Guid tenantId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;

        var groupId = Guid.NewGuid();
        db.OptionGroups.Add(new OptionGroup
        {
            Id = groupId,
            TenantId = tenantId,
            Key = "portion",
            Label = "Portion",
            SelectionMode = OptionSelectionModes.One,
            Currency = "GBP",
            SortOrder = 1,
            IsActive = true,
        });
        db.OptionChoices.Add(new OptionChoice
        {
            Id = Guid.NewGuid(), TenantId = tenantId, OptionGroupId = groupId,
            Key = "light", Label = "Light table", Price = 0m, IsRecommendedDefault = true, SortOrder = 0, IsActive = true,
        });
        db.OptionChoices.Add(new OptionChoice
        {
            Id = Guid.NewGuid(), TenantId = tenantId, OptionGroupId = groupId,
            Key = "full", Label = "Full table", Price = 10m, IsRecommendedDefault = false, SortOrder = 1, IsActive = true,
        });

        await db.SaveChangesAsync();
    }

    private async Task<Guid> SeedProductAsync(
        Guid tenantId, string slug, bool offerPortion, decimal? surcharge = null, string status = ProductStatuses.Active)
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
            Kind = ProductKinds.Simple,
            Status = status,
            UnitSurcharge = surcharge,
            UnitSurchargeCurrency = surcharge is null ? null : "GBP",
        });

        if (offerPortion)
        {
            var group = await db.OptionGroups.FirstAsync(g => g.TenantId == tenantId && g.Key == "portion");
            db.ProductOptionGroups.Add(new ProductOptionGroup
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductId = productId,
                OptionGroupId = group.Id,
                SortOrder = 0,
            });
        }

        await db.SaveChangesAsync();
        return productId;
    }

    private sealed record OptionGroupResponse(string Key, string Label, List<OptionChoiceResponse> Choices);

    private sealed record OptionChoiceResponse(string Key, string Label, decimal Price, bool IsRecommendedDefault);

    private sealed record EffectiveOptionGroupResponse(string Key, string DefaultChoiceKey);

    private sealed record ProductResponse(
        string Slug,
        List<EffectiveOptionGroupResponse> EffectiveOptionGroups,
        decimal? UnitSurcharge,
        string? UnitSurchargeCurrency);

    private sealed record SelectionQuoteResponse(
        string CanonicalSelectionJson,
        bool IsDefault,
        decimal Adjustment,
        string Currency);
}
