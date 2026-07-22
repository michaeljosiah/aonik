using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Entities.Inventory;
using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Entities.Identity;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

/// <summary>
/// Spec 068 §11 over the real DI container: the box session routes, the X-Cart-Token boundary
/// (A16 — absent/wrong tokens are the same 404 an unknown cart gets), the box-plan read, the
/// continue gate (AC-20) and the component-sum quote contract (A24).
/// </summary>
public class CommerceBoxCartEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CommerceBoxCartEndpointTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task BoxJourney_Should_CreateAddGrowFillContinue_UnderTheToken()
    {
        var tenantId = Guid.NewGuid();
        var (bundleId, variantId) = await SeedBoxWorldAsync(tenantId);
        var client = Client(tenantId);

        // Create — the ONLY token disclosure.
        var createResponse = await client.PostAsJsonAsync("/commerce/carts/box", new { bundleProductId = bundleId, size = 6 });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var cartId = created.GetProperty("box").GetProperty("cartId").GetGuid();
        var token = created.GetProperty("cartToken").GetString();
        token.Should().NotBeNullOrEmpty();

        client.DefaultRequestHeaders.Add("X-Cart-Token", token);

        // AC-20 — continue on an incomplete box names the shortfall.
        (await client.PostAsync($"/commerce/carts/{cartId}/continue", null))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Fill via the lines route; a client-supplied price field is simply not part of the
        // contract and is ignored (AC-22) — the server's figures come back regardless.
        var add = await client.PostAsJsonAsync($"/commerce/carts/{cartId}/lines",
            new { productVariantId = variantId, quantity = 6, price = 0.01m });
        add.StatusCode.Should().Be(HttpStatusCode.OK);
        var filled = await add.Content.ReadFromJsonAsync<JsonElement>();
        var quote = filled.GetProperty("quote");
        quote.GetProperty("total").GetDecimal().Should().Be(95m, "server figures only");
        quote.GetProperty("isFull").GetBoolean().Should().BeTrue();

        // A24 — the total is the component sum, whatever the keys are.
        var componentSum = quote.GetProperty("components").EnumerateArray()
            .Sum(c => c.GetProperty("amount").GetDecimal());
        componentSum.Should().Be(quote.GetProperty("total").GetDecimal());

        (await client.PostAsync($"/commerce/carts/{cartId}/continue", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // GET serves the §7 payload for box carts.
        var get = await client.GetFromJsonAsync<JsonElement>($"/commerce/carts/{cartId}");
        get.GetProperty("box").GetProperty("lines").GetArrayLength().Should().Be(1);
        get.TryGetProperty("cartToken", out var echoed).Should().BeTrue();
        echoed.ValueKind.Should().Be(JsonValueKind.Null, "the token is disclosed exactly once, at create");
    }

    [Fact]
    public async Task A16_TokenMatrix_Should_Be404_ForAbsentOrWrongTokens()
    {
        var tenantId = Guid.NewGuid();
        var (bundleId, _) = await SeedBoxWorldAsync(tenantId);
        var creator = Client(tenantId);
        var created = await (await creator.PostAsJsonAsync("/commerce/carts/box",
            new { bundleProductId = bundleId, size = 6 })).Content.ReadFromJsonAsync<JsonElement>();
        var cartId = created.GetProperty("box").GetProperty("cartId").GetGuid();

        var absent = Client(tenantId);
        (await absent.GetAsync($"/commerce/carts/{cartId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await absent.PostAsJsonAsync($"/commerce/carts/{cartId}/lines", new { productVariantId = Guid.NewGuid(), quantity = 1 }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var wrong = Client(tenantId);
        wrong.DefaultRequestHeaders.Add("X-Cart-Token", new string('z', 43));
        (await wrong.GetAsync($"/commerce/carts/{cartId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var right = Client(tenantId);
        right.DefaultRequestHeaders.Add("X-Cart-Token", created.GetProperty("cartToken").GetString());
        (await right.GetAsync($"/commerce/carts/{cartId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task BoxPlanRead_Should_ServeThePricingTable()
    {
        var tenantId = Guid.NewGuid();
        await SeedBoxWorldAsync(tenantId);
        var client = Client(tenantId);

        var response = await client.GetAsync("/commerce/catalog/products/meal-box/box-plan");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Vary.Should().Contain("X-Tenant-Id");
        var plan = await response.Content.ReadFromJsonAsync<JsonElement>();
        plan.GetProperty("minSize").GetInt32().Should().Be(6);
        plan.GetProperty("maxSize").GetInt32().Should().Be(30);
        plan.GetProperty("presets").EnumerateArray().Single().GetProperty("price").GetDecimal().Should().Be(170m);

        (await client.GetAsync("/commerce/catalog/products/no-such/box-plan"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task HttpCreates_Should_RejectPartyBinding_UntilIdentityLands()
    {
        // J1/R10 — these anonymous routes carry no principal-to-party mapping yet; accepting a
        // party id would mint a cart its own creator can never read again (party carts ignore
        // the guest token by design). Loud rejection beats a silent lock-out.
        var tenantId = Guid.NewGuid();
        var (bundleId, _) = await SeedBoxWorldAsync(tenantId);
        var client = Client(tenantId);

        (await client.PostAsJsonAsync("/commerce/carts/box",
            new { bundleProductId = bundleId, size = 6, buyerPartyId = Guid.NewGuid() }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/commerce/carts",
            new { currency = "GBP", buyerPartyId = Guid.NewGuid() }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─── Seeding ─────────────────────────────────────────────────────────────

    private HttpClient Client(Guid tenantId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }

    private async Task<(Guid BundleId, Guid VariantId)> SeedBoxWorldAsync(Guid tenantId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Box Test Tenant",
            Environment = "Testing",
            DefaultCurrency = "GBP",
            SupportedCountriesJson = "[]",
            Status = TenantStatus.Active,
        });

        var categoryId = Guid.NewGuid();
        db.ProductCategories.Add(new ProductCategory
        {
            Id = categoryId, TenantId = tenantId, Slug = "dishes", Name = "Dishes",
        });

        var dishId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        db.Products.Add(new Product
        {
            Id = dishId, TenantId = tenantId, Slug = "jollof", Name = "Jollof",
            Description = "A dish", Kind = ProductKinds.Simple, Status = ProductStatuses.Active,
            CategoryId = categoryId,
        });
        db.ProductVariants.Add(new ProductVariant
        {
            Id = variantId, TenantId = tenantId, ProductId = dishId, Sku = "SKU-jollof", Name = "Jollof",
        });
        db.InventoryLevels.Add(new InventoryLevel
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ProductVariantId = variantId,
            StockItemKind = StockItemKinds.ProductVariant, OnHand = 50m,
        });

        var bundleId = Guid.NewGuid();
        db.Products.Add(new Product
        {
            Id = bundleId, TenantId = tenantId, Slug = "meal-box", Name = "Meal Box",
            Description = "The box", Kind = ProductKinds.Bundle, Status = ProductStatuses.Active,
            BundlePricingMode = BundlePricingModes.SizeTiered,
        });
        db.BundleSlots.Add(new BundleSlot
        {
            Id = Guid.NewGuid(), TenantId = tenantId, BundleProductId = bundleId,
            Name = "Pick dishes", MinItems = 0, MaxItems = 99, FromCategoryId = categoryId,
        });

        var planId = Guid.NewGuid();
        db.BundleSizePlans.Add(new BundleSizePlan
        {
            Id = planId, TenantId = tenantId, BundleProductId = bundleId,
            MinSize = 6, MaxSize = 30, BaseSize = 6, BasePrice = 95m, PerSpacePrice = 15m, Currency = "GBP",
        });
        db.BundleSizePresets.Add(new BundleSizePreset
        {
            Id = Guid.NewGuid(), TenantId = tenantId, BundleSizePlanId = planId, Size = 12, Price = 170m,
        });

        await db.SaveChangesAsync();
        return (bundleId, variantId);
    }
}
