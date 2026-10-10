using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.Services.Checkout;
using Aonik.Finance.Entities.Orders;
using Aonik.Infrastructure.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

public partial class CommerceBoxCartEndpointTests
{
    [Fact]
    public async Task SelectedReorder_Should_PreviewWithoutMutation_AndBuildOnlyRequestedQuantity()
    {
        var tenantId = Guid.NewGuid();
        var (bundleId, variantId) = await SeedBoxWorldAsync(tenantId);
        var customer = await CustomerAsync(tenantId);
        var source = await CreateBoxAsync(customer.Client, bundleId, variantId, 6);
        var orderId = await SeedReorderPurchaseAsync(tenantId, source, customer.PartyId);
        using var previewResponse = await customer.Client.GetAsync($"/commerce/storefront/orders/{orderId}/reorder-preview");
        previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertNoStore(previewResponse);
        var preview = (await previewResponse.Content.ReadFromJsonAsync<ReorderPreviewDto>())!;
        preview.Dishes.Should().ContainSingle();
        (await ReadCartsAsync(tenantId)).Should().ContainSingle();
        using var response = await customer.Client.PostAsJsonAsync($"/commerce/storefront/orders/{orderId}/reorder",
            new ReorderSelectionRequest([new(preview.Dishes.Single().SelectionId, 2)]));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var fresh = (await response.Content.ReadFromJsonAsync<BoxCartDto>())!;
        fresh.Box.Lines.Should().ContainSingle().Which.Quantity.Should().Be(2);
        fresh.CheckoutDraft.Should().BeNull();
    }

    [Fact]
    public async Task Reorder_Should_CreateFreshOwnedBox_AndReturnExistingConflictOnRepeat()
    {
        var tenantId = Guid.NewGuid();
        var (bundleId, variantId) = await SeedBoxWorldAsync(tenantId);
        var customer = await CustomerAsync(tenantId);
        var source = await CreateBoxAsync(customer.Client, bundleId, variantId, 6);
        var orderId = await SeedReorderPurchaseAsync(tenantId, source, customer.PartyId);
        var route = $"/commerce/storefront/orders/{orderId}/reorder";

        using var response = await customer.Client.PostAsync(route, null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertNoStore(response);
        response.Headers.GetValues("Referrer-Policy").Should().Contain("no-referrer");
        var fresh = (await response.Content.ReadFromJsonAsync<BoxCartDto>())!;
        fresh.Box.CartId.Should().NotBe(source.Box.CartId);
        fresh.Box.Lines.Should().ContainSingle().Which.Quantity.Should().Be(6);
        fresh.CartVersion.Should().NotBeNullOrEmpty();
        fresh.CartToken.Should().BeNull(); fresh.CheckoutDraft.Should().BeNull(); fresh.OrderId.Should().BeNull();
        var current = (await customer.Client.GetFromJsonAsync<BoxCartDto>(CurrentBoxRoute))!;
        current.Box.CartId.Should().Be(fresh.Box.CartId);
        using var duplicate = await customer.Client.PostAsync(route, null);
        await ReadConflictAsync(duplicate, ActiveBoxConflictException.Existing);
        (await ReadCartsAsync(tenantId)).Should().HaveCount(2);
    }

    [Fact]
    public async Task Reorder_Should_RequireCurrentPartyOwnership_RegardlessOfTokensOrSuppliedParty()
    {
        var tenantId = Guid.NewGuid();
        var (bundleId, variantId) = await SeedBoxWorldAsync(tenantId);
        var customer = await CustomerAsync(tenantId);
        var source = await CreateBoxAsync(customer.Client, bundleId, variantId, 6);
        var orderId = await SeedReorderPurchaseAsync(tenantId, source, customer.PartyId);
        var route = $"/commerce/storefront/orders/{orderId}/reorder";
        using var anonymous = Client(tenantId);
        anonymous.DefaultRequestHeaders.Add("X-Cart-Token", source.CartToken!);
        using var unauthenticated = await anonymous.PostAsJsonAsync(route, new { selections = new[] { new { selectionId = Guid.NewGuid(), quantity = 1 } }, buyerPartyId = customer.PartyId });
        unauthenticated.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        AssertNoStore(unauthenticated);

        var other = await CustomerAsync(tenantId);
        var otherTenantId = Guid.NewGuid();
        await SeedBoxWorldAsync(otherTenantId);
        var otherTenant = await CustomerAsync(otherTenantId);
        foreach (var client in new[] { other.Client, otherTenant.Client })
        {
            using var denied = await client.PostAsJsonAsync(route + $"?partyId={customer.PartyId}", new { selections = new[] { new { selectionId = Guid.NewGuid(), quantity = 1 } }, buyerPartyId = customer.PartyId });
            denied.StatusCode.Should().Be(HttpStatusCode.NotFound);
            AssertNoStore(denied);
            (await denied.Content.ReadAsStringAsync()).Should().NotContain(source.Box.CartId.ToString());
        }
        (await ReadCartsAsync(tenantId)).Should().ContainSingle();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reorder_Should_RejectUnpaidSource_OrDescribeUnavailablePurchasedDish(bool paid)
    {
        var tenantId = Guid.NewGuid();
        var (bundleId, variantId) = await SeedBoxWorldAsync(tenantId);
        var customer = await CustomerAsync(tenantId);
        var source = await CreateBoxAsync(customer.Client, bundleId, variantId, 6);
        var orderId = await SeedReorderPurchaseAsync(tenantId, source, customer.PartyId, paid);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
            var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
            (await db.ProductVariants.SingleAsync(x => x.Id == variantId)).IsActive = false;
            await db.SaveChangesAsync();
        }

        using var response = await customer.Client.PostAsync($"/commerce/storefront/orders/{orderId}/reorder", null);

        AssertNoStore(response);
        response.StatusCode.Should().Be(paid ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
        if (paid)
        {
            var fresh = (await response.Content.ReadFromJsonAsync<BoxCartDto>())!;
            fresh.Box.Size.Should().Be(6); fresh.Box.Lines.Should().BeEmpty();
            var notice = fresh.Changes.Should().ContainSingle().Subject;
            notice.SourceVariantId.Should().Be(variantId); notice.SourceName.Should().Be("Original Jollof");
            notice.Reason.Should().Be(BoxChangeReasons.ReorderOffMenu);
        }
        else (await ReadCartsAsync(tenantId)).Should().ContainSingle();
    }

    private async Task<Guid> SeedReorderPurchaseAsync(Guid tenantId, BoxCartDto source, Guid partyId, bool paid = true)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        var orderId = Guid.NewGuid();
        var cart = await db.Carts.Include(x => x.Items).SingleAsync(x => x.Id == source.Box.CartId);
        cart.Status = paid ? CartStatuses.CheckedOut : CartStatuses.Open;
        cart.OrderId = orderId;
        cart.CheckoutDraftJson = JsonSerializer.Serialize(new CartCheckoutDraftDto(Notes: "Private old instructions",
            Gift: new(true, GreetingCardMessage: "Old gift"), DiscountCode: "OLD"));
        db.Set<Order>().Add(new Order
        {
            Id = orderId, TenantId = tenantId, PayerPartyId = partyId, OrderType = OrderTypeCodes.ProductPurchase,
            Status = paid ? OrderStatusCodes.Complete : OrderStatusCodes.Draft, AmountIn = 95m, CurrencyIn = "GBP",
            Items = [new OrderItem { TenantId = tenantId, OrderId = orderId, ItemType = OrderTypeCodes.ProductPurchase,
                ItemIndex = 0, ProductId = source.Box.BundleProductId, Quantity = 1, UnitPrice = 95, AmountIn = 95,
                CurrencyIn = "GBP", CurrencyOut = "GBP", Sku = "meal-box" }]
        });
        db.OrderChargeSummaries.Add(new OrderChargeSummary
        {
            TenantId = tenantId, OrderId = orderId, Currency = "GBP", Subtotal = 95, Total = 95,
            PaymentStatus = paid ? "Captured" : "RequiresAction"
        });
        foreach (var item in cart.Items)
            db.OrderBundleSelections.Add(new OrderBundleSelection
            {
                TenantId = tenantId, OrderId = orderId, OrderItemIndex = 0, ProductVariantId = item.ProductVariantId,
                BundleSlotId = item.BoxBundleSlotId!.Value, Quantity = item.Quantity, Sku = item.Sku,
                NameSnapshot = "Original Jollof", PersonalisationJson = item.PersonalisationJson
            });
        await db.SaveChangesAsync();
        return orderId;
    }
}
