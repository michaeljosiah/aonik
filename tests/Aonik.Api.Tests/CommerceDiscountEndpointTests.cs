using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Promotions;
using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Entities.Identity;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

public sealed class CommerceDiscountEndpointTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private const string AdminPath = "/commerce/admin/discounts";

    [Fact]
    public async Task CouponJourney_Should_PreviewApplyRejectReplacementAndRemove_WithOwnedVersionedDraft()
    {
        var seeded = await SeedAsync();
        using var client = Client(seeded);
        var path = $"/commerce/carts/{seeded.CartId}/discount";
        using var preview = await client.PostAsJsonAsync(path + "/validate", new { code = " save10 " });
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        Private(preview);
        var quote = (await preview.Content.ReadFromJsonAsync<CartDiscountQuoteDto>())!;
        quote.Total.Should().Be(90m);
        quote.CartVersion.Should().Be(seeded.Version);
        (await ReadCartAsync(seeded)).CheckoutDraftJson.Should().Contain("Keep me").And.NotContain("SAVE10");

        using var apply = await client.PutAsJsonAsync(path, new { code = "save10" });
        apply.StatusCode.Should().Be(HttpStatusCode.OK);
        Private(apply);
        var applied = (await apply.Content.ReadFromJsonAsync<CartDiscountQuoteDto>())!;
        applied.Discount.Should().Be(new DiscountCodeStatusDto("SAVE10", 10m));
        SetVersion(client, applied.CartVersion);
        using var invalid = await client.PutAsJsonAsync(path, new { code = "missing" });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await invalid.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be(DiscountException.Invalid);
        Private(invalid);
        (await ReadCartAsync(seeded)).CheckoutDraftJson.Should().Contain("SAVE10").And.Contain("Keep me");

        SetVersion(client, seeded.Version);
        using var stale = await client.DeleteAsync(path);
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        Private(stale);
        SetVersion(client, applied.CartVersion);
        using var remove = await client.DeleteAsync(path);
        remove.StatusCode.Should().Be(HttpStatusCode.OK);
        Private(remove);
        var removed = (await remove.Content.ReadFromJsonAsync<CartDiscountQuoteDto>())!;
        removed.Total.Should().Be(100m);
        removed.Discount.Should().BeNull();
        (await ReadCartAsync(seeded)).CheckoutDraftJson.Should().Contain("Keep me").And.NotContain("SAVE10");
        var cart = (await client.GetFromJsonAsync<CartDto>($"/commerce/carts/{seeded.CartId}"))!;
        cart.Total.Should().Be(100m);
        cart.Quote!.Total.Should().Be(100m);
    }

    [Theory]
    [InlineData("missing", DiscountException.Invalid)]
    [InlineData("EXPIRED", DiscountException.Expired)]
    [InlineData("INACTIVE", DiscountException.Inactive)]
    [InlineData("USED", DiscountException.AlreadyUsed)]
    [InlineData("USD", DiscountException.CurrencyMismatch)]
    [InlineData("INELIGIBLE", DiscountException.NotEligible)]
    public async Task CouponRejection_Should_ReturnAnActualReason_WithoutChangingTheDraft(string code, string reason)
    {
        var seeded = await SeedAsync();
        using var client = Client(seeded);
        var path = $"/commerce/carts/{seeded.CartId}/discount";
        using var preview = await client.PostAsJsonAsync(path + "/validate", new { code });
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        (await preview.Content.ReadFromJsonAsync<CartDiscountQuoteDto>())!.Discount!.ReasonCode.Should().Be(reason);
        using var apply = await client.PutAsJsonAsync(path, new { code });
        apply.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Private(apply);
        (await apply.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be(reason);
        var unchanged = await ReadCartAsync(seeded);
        Convert.ToBase64String(unchanged.RowVersion).Should().Be(seeded.Version);
        unchanged.CheckoutDraftJson.Should().NotContain(code);
    }

    [Fact]
    public async Task CouponBoundary_Should_RequireCartOwnershipAndWriteVersion_AndKeepOrdinaryDraftCompatible()
    {
        var seeded = await SeedAsync();
        var other = await SeedAsync();
        using var absent = Client(seeded, token: false);
        using var foreign = Client(other);
        var path = $"/commerce/carts/{seeded.CartId}/discount";
        foreach (var client in new[] { absent, foreign })
        {
            using var response = await client.PostAsJsonAsync(path + "/validate", new { code = "SAVE10" });
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            Private(response);
        }
        using var owner = Client(seeded);
        owner.DefaultRequestHeaders.Remove("X-Cart-Version");
        using var unversioned = await owner.PutAsJsonAsync(path, new { code = "SAVE10" });
        unversioned.StatusCode.Should().Be(HttpStatusCode.Conflict);
        Private(unversioned);
        SetVersion(owner, seeded.Version);
        using var malformed = await owner.PutAsJsonAsync(path, new { code = new string('x', 65) });
        malformed.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        Private(malformed);
        using var save = await owner.PutAsJsonAsync($"/commerce/carts/{seeded.CartId}/checkout-draft", new { discountCode = "EXPIRED" });
        save.StatusCode.Should().Be(HttpStatusCode.OK);
        var read = (await owner.GetFromJsonAsync<CartDto>($"/commerce/carts/{seeded.CartId}"))!;
        read.CheckoutDraft!.DiscountCode.Should().Be("EXPIRED");
        read.Quote!.Discount!.ReasonCode.Should().Be(DiscountException.Expired);
    }

    [Fact]
    public async Task AdminDiscounts_Should_ListAndReplaceWithVersion_UsingExistingRolePolicies()
    {
        var seeded = await SeedAsync();
        using var admin = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(seeded.TenantId).WithRoles("Operations"));
        using var reader = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(seeded.TenantId).WithRoles("ReadOnly"));
        using var customer = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(seeded.TenantId).WithRoles("Customer"));
        using var anonymous = Client(seeded);
        using var denied = await customer.GetAsync(AdminPath);
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        Private(denied);
        using var unauthorized = await anonymous.GetAsync(AdminPath);
        unauthorized.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        Private(unauthorized);
        using var list = await reader.GetAsync(AdminPath + "?search=save&page=1&pageSize=10");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        Private(list);
        var discount = (await list.Content.ReadFromJsonAsync<PagedResult<DiscountDto>>())!.Items.Single();
        var replacement = new { kind = "Percentage", value = 15m, isActive = false, currency = (string?)null,
            maxRedemptions = (int?)null, expiresAt = (DateTime?)null, eligibleProductIds = (Guid[]?)null, expectedVersion = discount.Version };
        using var readOnlyWrite = await reader.PutAsJsonAsync(AdminPath + "/" + discount.Id, replacement);
        readOnlyWrite.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var changed = await admin.PutAsJsonAsync(AdminPath + "/" + discount.Id, replacement);
        changed.StatusCode.Should().Be(HttpStatusCode.OK);
        Private(changed);
        var updated = (await changed.Content.ReadFromJsonAsync<DiscountDto>())!;
        updated.Code.Should().Be("SAVE10");
        updated.Value.Should().Be(15);
        updated.IsActive.Should().BeFalse();
        // InMemory does not synthesize native discount rowversions; send an explicit wrong token.
        using var wrong = await admin.PutAsJsonAsync(AdminPath + "/" + discount.Id,
            new { kind = "Percentage", value = 15m, isActive = true, expectedVersion = "AQIDBAUGBwg=" });
        wrong.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var bounds = await admin.GetAsync(AdminPath + "?pageSize=101");
        bounds.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using var invalidDefinition = await admin.PostAsJsonAsync(AdminPath, new { code = "BAD", kind = "Unknown", value = 10m });
        invalidDefinition.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using var negative = await admin.PostAsJsonAsync(AdminPath, new { code = "BAD", kind = "FixedAmount", value = -5m, currency = "GBP" });
        negative.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private sealed record Seed(Guid TenantId, Guid CartId, string Token, string Version);

    private async Task<Seed> SeedAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Discount API " + tenantId, Status = TenantStatus.Active,
            Environment = "Testing", DefaultCurrency = "GBP", SupportedCountriesJson = "[]" });
        var product = new Product { TenantId = tenantId, Name = "Extra", Slug = "extra", Status = ProductStatuses.Active };
        var variant = new ProductVariant { TenantId = tenantId, ProductId = product.Id, Sku = "extra", Name = "Extra" };
        var cart = new Cart { TenantId = tenantId, AnonymousToken = CartAccess.MintToken(), Currency = "GBP",
            CheckoutDraftJson = "{\"notes\":\"Keep me\"}", RowVersion = [1, 2, 3, 4, 5, 6, 7, 8], LastActivityAtUtc = DateTime.UtcNow };
        cart.Items.Add(new CartItem { TenantId = tenantId, CartId = cart.Id, ProductVariantId = variant.Id, Quantity = 1, UnitPriceSnapshot = 100 });
        db.Products.Add(product); db.ProductVariants.Add(variant); db.Carts.Add(cart);
        db.Discounts.AddRange(new Discount { TenantId = tenantId, Code = "SAVE10", Value = 10 },
            new Discount { TenantId = tenantId, Code = "EXPIRED", Value = 10, ExpiresAt = DateTime.UtcNow.AddDays(-1) },
            new Discount { TenantId = tenantId, Code = "INACTIVE", Value = 10, IsActive = false },
            new Discount { TenantId = tenantId, Code = "USED", Value = 10, MaxRedemptions = 0 },
            new Discount { TenantId = tenantId, Code = "USD", Kind = DiscountKinds.FixedAmount, Value = 5, Currency = "USD" },
            new Discount { TenantId = tenantId, Code = "INELIGIBLE", Value = 10, EligibleProductIdsJson = JsonSerializer.Serialize(new[] { Guid.NewGuid() }) });
        await db.SaveChangesAsync();
        return new(tenantId, cart.Id, cart.AnonymousToken!, Convert.ToBase64String(cart.RowVersion));
    }

    private HttpClient Client(Seed seed, bool token = true)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", seed.TenantId.ToString());
        if (token) client.DefaultRequestHeaders.Add("X-Cart-Token", seed.Token);
        SetVersion(client, seed.Version);
        return client;
    }

    private async Task<Cart> ReadCartAsync(Seed seed)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = seed.TenantId;
        return await scope.ServiceProvider.GetRequiredService<AonikDbContext>().Carts.AsNoTracking().SingleAsync(x => x.Id == seed.CartId);
    }

    private static void SetVersion(HttpClient client, string version)
    {
        client.DefaultRequestHeaders.Remove("X-Cart-Version");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Cart-Version", version);
    }

    private static void Private(HttpResponseMessage response) => (response.Headers.CacheControl?.NoStore).Should().BeTrue();
}
