using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Promotions;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Commerce;

public class CartDiscountTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData(" save10 ", "SAVE10")]
    [InlineData("save10", " save10 ")]
    public async Task UnchangedCoupon_Should_NotRewriteTheDraftOrRenewActivity(string? savedCode, string? requestedCode)
    {
        var h = new BoxTestHarness();
        var (id, token, _) = await SeedGenericAsync(h);
        await using var db = h.Commerce();
        var original = await db.Carts.SingleAsync();
        original.CheckoutDraftJson = savedCode is null ? null
            : System.Text.Json.JsonSerializer.Serialize(new CartCheckoutDraftDto(DiscountCode: savedCode),
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        await db.SaveChangesAsync();
        var json = original.CheckoutDraftJson;
        var activity = original.LastActivityAtUtc;
        var version = Convert.ToBase64String(original.RowVersion);
        h.Clock.UtcNow = h.Clock.UtcNow.AddHours(2);
        var access = CartAccessContext.ForGuest(token, version);

        var result = requestedCode is null
            ? await h.Carts().RemoveDiscountAsync(id, access)
            : await h.Carts().ApplyDiscountAsync(id, requestedCode, access);

        result.CartVersion.Should().Be(version);
        var unchanged = await db.Carts.AsNoTracking().SingleAsync();
        unchanged.CheckoutDraftJson.Should().Be(json);
        unchanged.LastActivityAtUtc.Should().Be(activity);
        unchanged.RowVersion.Should().Equal(original.RowVersion);
    }

    [Fact]
    public async Task CouponEdits_Should_PreserveDraft_RejectReplacementAndStaleWrites_AndClearOnlyCode()
    {
        var h = new BoxTestHarness();
        var (id, token, _) = await SeedGenericAsync(h);
        var service = h.Carts();
        var cart = (await service.GetCartAsync(id, CartAccessContext.ForGuest(token)))!;
        var draft = await service.SaveCheckoutDraftAsync(id, new CartCheckoutDraftDto(Notes: "Keep this note", CreateAccount: true),
            CartAccessContext.ForGuest(token, cart.CartVersion));
        var access = CartAccessContext.ForGuest(token, draft.CartVersion);

        var preview = await service.PreviewDiscountAsync(id, " save10 ", access);
        preview.Total.Should().Be(90m);
        preview.Discount!.Code.Should().Be("SAVE10");
        (await service.GetCartAsync(id, access))!.CheckoutDraft!.DiscountCode.Should().BeNull();
        var applied = await service.ApplyDiscountAsync(id, " save10 ", access);
        applied.Total.Should().Be(90m);
        var current = (await service.GetCartAsync(id, access))!;
        current.Total.Should().Be(100m, "the legacy cart Total remains the goods subtotal");
        current.Quote!.Total.Should().Be(90m);
        current.CheckoutDraft.Should().Be(new CartCheckoutDraftDto(Notes: "Keep this note", CreateAccount: true, DiscountCode: "SAVE10"));
        var latest = access with { ExpectedCartVersion = applied.CartVersion };
        var repeated = await service.ApplyDiscountAsync(id, "SAVE10", latest);
        repeated.CartVersion.Should().Be(applied.CartVersion);
        var invalid = () => service.ApplyDiscountAsync(id, "MISSING", latest);
        await invalid.Should().ThrowAsync<DiscountException>().Where(x => x.Code == DiscountException.Invalid);
        // InMemory does not generate a new native rowversion after a saved edit.
        var stale = () => service.RemoveDiscountAsync(id, access with { ExpectedCartVersion = "CAcGBQQDAgE=" });
        await stale.Should().ThrowAsync<CartWriteConflictException>();

        var removed = await service.RemoveDiscountAsync(id, latest);
        removed.Discount.Should().BeNull();
        removed.Total.Should().Be(100m);
        var remaining = (await service.GetCartAsync(id, access))!.CheckoutDraft;
        remaining.Should().Be(new CartCheckoutDraftDto(Notes: "Keep this note", CreateAccount: true));
        (await service.RemoveDiscountAsync(id, latest with { ExpectedCartVersion = removed.CartVersion })).CartVersion.Should().Be(removed.CartVersion);
    }

    [Fact]
    public async Task BoxCouponPreviewAndEdits_Should_NotRenewTheSelectedDeliveryHold()
    {
        var h = new BoxTestHarness();
        h.Settings[CommerceSettingNames.StorefrontDeliveryChargedAmount] = "7.50";
        var fixture = await h.BuildAsync("dish");
        var box = await h.BoxCarts().CreateAsync(new CreateBoxCartCommand(fixture.BundleProductId, 6));
        box = await h.HoldDeliveryAsync(box);
        await using var context = h.Commerce();
        await new DiscountService(context, new TestTenantProvider(h.TenantId), h.Clock)
            .CreateAsync(new CreateDiscountCommand("SAVE10", DiscountKinds.Percentage, 10m));
        var original = await context.CartDeliveryReservations.AsNoTracking().SingleAsync();
        var activity = (await context.Carts.AsNoTracking().SingleAsync()).LastActivityAtUtc;
        h.Clock.UtcNow = h.Clock.UtcNow.AddMinutes(2);
        var access = CartAccessContext.ForGuest(box.CartToken, box.CartVersion);

        var preview = await h.Carts().PreviewDiscountAsync(box.Box.CartId, "SAVE10", access);
        preview.Total.Should().Be(93m);
        (await context.Carts.AsNoTracking().SingleAsync()).LastActivityAtUtc.Should().Be(activity);
        var applied = await h.Carts().ApplyDiscountAsync(box.Box.CartId, "SAVE10", access);
        var quoted = await h.BoxCarts().QuoteAsync(box.Box.CartId, access);
        quoted.Quote.Total.Should().Be(applied.Total);
        quoted.Quote.Components.Single(x => x.Key == "discount").Amount.Should().Be(-9.50m);
        quoted.Quote.Components.Sum(x => x.Amount).Should().Be(quoted.Quote.Total);
        var hold = await context.CartDeliveryReservations.AsNoTracking().SingleAsync();
        hold.ExpiresAtUtc.Should().Be(original.ExpiresAtUtc);
        hold.SelectedAtUtc.Should().Be(original.SelectedAtUtc);
        hold.RowVersion.Should().Equal(original.RowVersion);
        quoted.CheckoutDraft!.DeliveryDate.Should().Be(BoxTestHarness.ValidDelivery.DeliveryDate);
    }

    [Fact]
    public async Task IncompleteDraft_Should_RetainInvalidCodeAndExplainIt_InsteadOfBreakingCartReads()
    {
        var h = new BoxTestHarness();
        var (id, token, _) = await SeedGenericAsync(h);
        var carts = h.Carts();
        var cart = (await carts.GetCartAsync(id, CartAccessContext.ForGuest(token)))!;
        await carts.SaveCheckoutDraftAsync(id, new CartCheckoutDraftDto(DiscountCode: "EXPIRED"),
            CartAccessContext.ForGuest(token, cart.CartVersion));
        var read = (await carts.GetCartAsync(id, CartAccessContext.ForGuest(token)))!;
        read.CheckoutDraft!.DiscountCode.Should().Be("EXPIRED");
        read.Quote!.Discount!.ReasonCode.Should().Be(DiscountException.Expired);
        read.Quote.Total.Should().Be(100m);
    }

    [Fact]
    public async Task FrozenQuote_Should_KeepItsActualCouponCharge_AfterCampaignWithdrawal()
    {
        var h = new BoxTestHarness();
        var (id, token, _) = await SeedGenericAsync(h);
        await using var db = h.Commerce();
        var cart = await db.Carts.SingleAsync();
        cart.Status = CartStatuses.CheckedOut;
        cart.OrderId = Guid.NewGuid();
        db.OrderChargeSummaries.Add(new OrderChargeSummary { TenantId = h.TenantId, OrderId = cart.OrderId.Value,
            Currency = "GBP", Subtotal = 100, DiscountTotal = 15, DiscountCode = "WITHDRAWN", TaxTotal = 2, Total = 87 });
        await db.SaveChangesAsync();

        var result = (await h.Carts().GetCartAsync(id, CartAccessContext.ForGuest(token)))!;
        result.Quote!.Total.Should().Be(87m);
        result.Quote.Discount.Should().Be(new DiscountCodeStatusDto("WITHDRAWN", 15m));
        var change = () => h.Carts().PreviewDiscountAsync(id, "SAVE10", CartAccessContext.ForGuest(token));
        await change.Should().ThrowAsync<CartWriteConflictException>();
    }

    [Fact]
    public async Task RetryingBoxQuote_Should_PreferCurrentPreparationOverTheCancelledOrderCharge()
    {
        var h = new BoxTestHarness();
        var fixture = await h.BuildAsync("dish");
        var box = await h.BoxCarts().CreateAsync(new CreateBoxCartCommand(fixture.BundleProductId, 6));
        await using var db = h.Commerce();
        var cart = await db.Carts.SingleAsync();
        cart.OrderId = Guid.NewGuid();
        cart.CheckoutState = CartCheckoutStates.Preparing;
        cart.CheckoutPreparationJson = new CheckoutPreparation(Guid.NewGuid(), null, "GBP", "Stripe", "Card",
            null, null, null, 95m, 19m, Guid.NewGuid(), "CURRENT", 2m, 85m,
            [], [], [], [], null).Serialize();
        db.OrderChargeSummaries.Add(new OrderChargeSummary { TenantId = h.TenantId, OrderId = cart.OrderId.Value,
            Currency = "GBP", Subtotal = 95m, DiscountTotal = 9.50m, DiscountCode = "PREVIOUS",
            TaxTotal = 0m, Total = 85.50m, PaymentStatus = "Cancelled" });
        await db.SaveChangesAsync();

        var result = await h.BoxCarts().QuoteAsync(box.Box.CartId, CartAccessContext.ForGuest(box.CartToken));

        result.Quote.Total.Should().Be(85m);
        result.Quote.Discount.Should().Be(new DiscountCodeStatusDto("CURRENT", 19m));
        result.Quote.Components.Single(x => x.Key == "discount").Amount.Should().Be(-19m);
        result.Quote.Components.Single(x => x.Key == "deliveryCharged").Amount.Should().Be(7m);
        result.Quote.Components.Sum(x => x.Amount).Should().Be(85m);
        (await db.OrderChargeSummaries.AsNoTracking().SingleAsync()).DiscountCode.Should().Be("PREVIOUS");
    }

    [Fact]
    public async Task TrustedLines_Should_ResolveCatalogProducts_KeepRealIndices_AndExcludeNonGoods()
    {
        var h = new BoxTestHarness();
        var (_, _, variantId) = await SeedGenericAsync(h);
        await using var db = h.Commerce();
        var variant = await db.ProductVariants.SingleAsync();
        var lines = await CheckoutDiscountLines.FromOrderItemsAsync(db, h.TenantId,
        [
            new(OrderTypeCodes.ProductPurchase, 7, 100, "GBP", ProductId: variantId),
            new("DeliveryFee", 8, 5, "GBP"),
            new("GiftCardValue", 9, 20, "GBP"),
            new("FutureFee", 10, 2, "GBP")
        ]);
        lines.Should().Equal(new DiscountChargeLine(7, variant.ProductId, 100));
        var foreign = () => CheckoutDiscountLines.FromOrderItemsAsync(db, Guid.NewGuid(),
            [new(OrderTypeCodes.ProductPurchase, 7, 100, "GBP", ProductId: variantId)]);
        await foreign.Should().ThrowAsync<DiscountException>().Where(x => x.Code == DiscountException.NotEligible);
    }

    private static async Task<(Guid CartId, string Token, Guid VariantId)> SeedGenericAsync(BoxTestHarness h)
    {
        await using var db = h.Commerce();
        var product = new Product { TenantId = h.TenantId, Name = "Extra", Slug = "extra", Status = ProductStatuses.Active };
        var variant = new ProductVariant { TenantId = h.TenantId, ProductId = product.Id, Name = "Extra", Sku = "extra" };
        var cart = new Cart { TenantId = h.TenantId, Currency = "GBP", AnonymousToken = CartAccess.MintToken(),
            LastActivityAtUtc = h.Clock.UtcNow, RowVersion = [1, 2, 3, 4, 5, 6, 7, 8] };
        cart.Items.Add(new CartItem { TenantId = h.TenantId, CartId = cart.Id, ProductVariantId = variant.Id, Quantity = 1, UnitPriceSnapshot = 100 });
        db.Products.Add(product); db.ProductVariants.Add(variant); db.Carts.Add(cart);
        db.Discounts.AddRange(new Discount { TenantId = h.TenantId, Code = "SAVE10", Value = 10 },
            new Discount { TenantId = h.TenantId, Code = "EXPIRED", Value = 10, ExpiresAt = h.Clock.UtcNow.AddMinutes(-1) });
        await db.SaveChangesAsync();
        return (cart.Id, cart.AnonymousToken, variant.Id);
    }
}
