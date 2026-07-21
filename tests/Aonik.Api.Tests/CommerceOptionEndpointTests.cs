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

        // Strictly 400. An earlier version of this test also permitted 500, which masked the fact
        // that OptionValidationException was unmapped and every invalid selection surfaced as an
        // internal error.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var payload = await response.Content.ReadFromJsonAsync<OptionErrorResponse>();
        payload!.Code.Should().Be("commerce.option_validation");
        payload.Rule.Should().Be("V1");
    }

    [Fact]
    public async Task SelectionQuote_Should_Reject_When_CurrencyIsOmitted()
    {
        // A quote without a currency has nothing to validate its amounts against (V10), so a
        // product mixing denominations would otherwise return a total wearing one arbitrary label.
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        await SeedCatalogueAsync(tenantId);
        await SeedProductAsync(tenantId, "jollof", offerPortion: true);

        var response = await AnonymousClient(tenantId).PostAsJsonAsync(
            "/commerce/catalog/products/jollof/selection-quote",
            new { selection = new { portion = "full" } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<OptionErrorResponse>())!.Rule.Should().Be("V10");
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

    [Fact]
    public async Task SetProductOptionGroups_Should_Reject_When_TheGroupsPropertyIsMissing()
    {
        // A malformed payload must not be able to do the one thing only an explicit clear should do.
        // Model binding leaves Groups null when the property is omitted or misspelled, and treating
        // that as an empty list would strip the product's entire personalisation surface — silently,
        // and with a 200.
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        await SeedCatalogueAsync(tenantId);
        var productId = await SeedProductAsync(tenantId, "malformed-payload", offerPortion: true);
        var client = await AdminClient(tenantId);

        var malformed = await client.PutAsJsonAsync(
            $"/commerce/admin/products/{productId}/option-groups",
            new { groupz = new[] { new { groupKey = "portion" } } });

        malformed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CountNarrowingsAsync(tenantId, productId)).Should().Be(1, "a rejected request must change nothing");
    }

    [Fact]
    public async Task SetProductOptionGroups_Should_Clear_When_AnEmptyArrayIsExplicit()
    {
        // The counterpart: clearing is legitimate, it just has to be said out loud.
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        await SeedCatalogueAsync(tenantId);
        var productId = await SeedProductAsync(tenantId, "explicit-clear", offerPortion: true);
        var client = await AdminClient(tenantId);

        var cleared = await client.PutAsJsonAsync(
            $"/commerce/admin/products/{productId}/option-groups",
            new { groups = Array.Empty<object>() });

        cleared.StatusCode.Should().Be(HttpStatusCode.OK);
        (await CountNarrowingsAsync(tenantId, productId)).Should().Be(0);
    }

    [Fact]
    public async Task UpdateOptionGroup_Should_PreserveCurrencyAndMode_When_TheUpdateOmitsThem()
    {
        // Currency denominates the group's ABSOLUTE choice prices. An update that says nothing about
        // currency must not redenominate them: renaming a group would otherwise reinterpret every
        // USD price as GBP without altering a single number, which no amount of eyeballing the data
        // afterwards would reveal.
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        var groupId = await SeedUsdMultiGroupAsync(tenantId);
        var client = await AdminClient(tenantId);

        var response = await client.PutAsJsonAsync(
            $"/commerce/admin/option-groups/{groupId}",
            new { label = "Extras (renamed)", sortOrder = 1, isActive = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<UpdatedGroupResponse>();
        payload!.Label.Should().Be("Extras (renamed)");
        payload.Currency.Should().Be("USD");
        payload.SelectionMode.Should().Be(OptionSelectionModes.Multi);
    }

    // ─── Seeding ─────────────────────────────────────────────────────────────

    private async Task<Guid> SeedUsdMultiGroupAsync(Guid tenantId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;

        var groupId = Guid.NewGuid();
        db.OptionGroups.Add(new OptionGroup
        {
            Id = groupId,
            TenantId = tenantId,
            Key = "extras",
            Label = "Extras",
            SelectionMode = OptionSelectionModes.Multi,
            Currency = "USD",
            SortOrder = 1,
            IsActive = true,
        });
        db.OptionChoices.Add(new OptionChoice
        {
            Id = Guid.NewGuid(), TenantId = tenantId, OptionGroupId = groupId,
            Key = "cheese", Label = "Cheese", Price = 2m, IsRecommendedDefault = true, SortOrder = 0, IsActive = true,
        });

        await db.SaveChangesAsync();
        return groupId;
    }

    private Task<HttpClient> AdminClient(Guid tenantId)
        => _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithRoles("Operations").WithTenant(tenantId));

    private async Task<int> CountNarrowingsAsync(Guid tenantId, Guid productId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
        return await db.ProductOptionGroups.CountAsync(x => x.ProductId == productId);
    }

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

    private sealed record OptionErrorResponse(string Error, string Code, string Rule);

    private sealed record OptionGroupResponse(string Key, string Label, List<OptionChoiceResponse> Choices);

    private sealed record UpdatedGroupResponse(string Key, string Label, string SelectionMode, string Currency);

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
