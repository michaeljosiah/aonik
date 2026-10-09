using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Promotions;
using Aonik.SharedKernel.Abstractions.Loyalty;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Aonik.Application.Tests.Commerce;

public sealed class CartLoyaltyQuoteTests
{
    private static readonly LoyaltyPolicy Policy = new(true, "v1", new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

    [Theory]
    [InlineData(false, 0, null)]
    [InlineData(false, 1, LoyaltyQuoteReasons.Disabled)]
    [InlineData(true, 1, LoyaltyQuoteReasons.SignInRequired)]
    public async Task DisabledOrGuestRedemption_Should_NotReadCatalogOrBalance(bool enabled, long points, string? reason)
    {
        var (options, tenant) = CommerceTestHarness.NewDb();
        await using var db = CommerceTestHarness.CreateContext(options, tenant);
        var loyalty = new Mock<ILoyaltyService>(MockBehavior.Strict);
        loyalty.Setup(x => x.GetPolicyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(enabled ? Policy : new());
        var cart = new Cart { TenantId = tenant, Currency = "GBP", CheckoutDraftJson = CartDraftData.Serialize(new(RequestedPoints: points)) };
        var helper = new CheckoutLoyaltyQuotes(db, new TestTenantProvider(tenant), loyalty.Object);

        var result = await helper.CalculateAsync(cart,
            [new(OrderTypeCodes.ProductPurchase, 0, 10m, "GBP", ProductId: Guid.NewGuid())], new(null, null, 0m, []), 10m);

        result.Quote?.ReasonCode.Should().Be(reason);
        result.Checkout.Should().BeNull();
        db.ChangeTracker.HasChanges().Should().BeFalse();
        loyalty.Verify(x => x.GetPolicyAsync(It.IsAny<CancellationToken>()), Times.Once);
        loyalty.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Quote_Should_UseCanonicalProductExclusions_KeepVoucherDistinct_AndRecalculateTaxAfterPoints()
    {
        var (options, tenant) = CommerceTestHarness.NewDb();
        await using var db = CommerceTestHarness.CreateContext(options, tenant);
        var product = new Product { TenantId = tenant, Name = "Eligible food" };
        var variant = new ProductVariant { TenantId = tenant, ProductId = product.Id, Name = "Food", Sku = "FOOD" };
        db.Products.Add(product); db.ProductVariants.Add(variant);
        await db.SaveChangesAsync();
        var discountId = Guid.NewGuid();
        var discounts = new Mock<IDiscountService>(MockBehavior.Strict);
        discounts.Setup(x => x.ComputeAsync("SAVE", It.IsAny<IReadOnlyList<DiscountChargeLine>>(), "GBP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscountComputation(discountId, "SAVE", 10m, [new(0, 10m)]));
        var loyalty = Loyalty(Policy with { EarnExcludedProductIds = [product.Id] });
        var cart = new Cart { TenantId = tenant, BuyerPartyId = Guid.NewGuid(), Currency = "GBP",
            CheckoutDraftJson = CartDraftData.Serialize(new(RequestedPoints: 1000)) };
        var tax = new Mock<ITaxCalculator>(MockBehavior.Strict);
        tax.Setup(x => x.CalculateAsync(90m, "GBP", It.IsAny<CancellationToken>())).ReturnsAsync(9m);
        tax.Setup(x => x.CalculateAsync(80m, "GBP", It.IsAny<CancellationToken>())).ReturnsAsync(8m);
        var quotes = new CartDiscountQuotes(db, new TestTenantProvider(tenant), discounts.Object, tax.Object,
            new NullTenantSettingStore(), new NullSettingProvider(), new GbpTenantCurrencyProvider(),
            new CheckoutLoyaltyQuotes(db, new TestTenantProvider(tenant), loyalty.Object));

        var quote = await quotes.CalculateAsync(cart, [new(OrderTypeCodes.ProductPurchase, 0, 100m, "GBP", ProductId: variant.Id)], 5m, "SAVE");

        quote.DiscountTotal.Should().Be(10m);
        quote.PointsAppliedValue.Should().Be(10m);
        quote.TaxTotal.Should().Be(8m);
        quote.DeliveryTotal.Should().Be(5m);
        quote.Total.Should().Be(93m);
        quote.Loyalty!.MaxRedeemablePoints.Should().Be(2080, "the pre-points total includes tax and delivery");
        quote.Loyalty.EstimatedEarnedPoints.Should().Be(0, "variant IDs are mapped to the explicitly excluded parent product");
        db.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Fact]
    public async Task FrozenQuote_Should_RestoreRecordedPointsAndDelivery_WithoutCurrentPolicyOrBalance()
    {
        var (options, tenant) = CommerceTestHarness.NewDb();
        await using var db = CommerceTestHarness.CreateContext(options, tenant);
        var order = Guid.NewGuid();
        var frozen = new LoyaltyCheckout(Guid.NewGuid(), Guid.NewGuid(), false, "old-policy", Policy.Ledger!,
            1000, 160, 10m, 95m, [], 85m);
        db.OrderChargeSummaries.Add(new OrderChargeSummary { TenantId = tenant, OrderId = order, Subtotal = 100m,
            DiscountTotal = 10m, PointsAppliedValue = 10m, Total = 85m, DiscountCode = "OLD",
            LoyaltyJson = CheckoutLoyaltyData.Serialize(frozen) });
        await db.SaveChangesAsync();
        var cart = new Cart { TenantId = tenant, Currency = "GBP", OrderId = order, Status = CartStatuses.CheckedOut };
        var strict = new Mock<ILoyaltyService>(MockBehavior.Strict);
        var quotes = new CartDiscountQuotes(db, new TestTenantProvider(tenant), Mock.Of<IDiscountService>(), Mock.Of<ITaxCalculator>(),
            new NullTenantSettingStore(), new NullSettingProvider(), new GbpTenantCurrencyProvider(),
            new CheckoutLoyaltyQuotes(db, new TestTenantProvider(tenant), strict.Object));

        var quote = await quotes.SnapshotAsync(cart, "CHANGED");

        quote.DeliveryTotal.Should().Be(5m);
        quote.PointsAppliedValue.Should().Be(10m);
        quote.Total.Should().Be(85m);
        quote.Loyalty!.AppliedPoints.Should().Be(1000);
        quote.Loyalty.EstimatedEarnedPoints.Should().Be(160);
        quote.Loyalty.AvailablePoints.Should().BeNull();
        strict.VerifyNoOtherCalls();
    }

    [Fact]
    public void Draft_Should_RoundTripLongPoints_ClearToZero_AndRejectNegative()
    {
        var cart = new Cart { CheckoutDraftJson = CartDraftData.Serialize(new(RequestedPoints: 3000000000L)) };
        CartDraftData.Read(cart)!.RequestedPoints.Should().Be(3000000000L);
        CartDraftData.Serialize(new()).Should().BeNull();
        var invalid = () => CartDraftData.Serialize(new(RequestedPoints: -1));
        invalid.Should().Throw<StorefrontValidationException>();
    }

    [Fact]
    public async Task RetryingBox_Should_UseCurrentFrozenPoints_AndKeepDeliveryAsItsOwnCharge()
    {
        var h = new BoxTestHarness();
        var fixture = await h.BuildAsync("dish");
        var box = await h.BoxCarts().CreateAsync(new(fixture.BundleProductId, 6));
        await using var db = h.Commerce();
        var cart = await db.Carts.SingleAsync();
        cart.OrderId = Guid.NewGuid();
        cart.CheckoutState = CartCheckoutStates.Preparing;
        var points = new LoyaltyCheckout(cart.Id, Guid.NewGuid(), false, "frozen", Policy.Ledger!, 500, 152, 5m, 83m, []);
        cart.CheckoutPreparationJson = new CheckoutPreparation(Guid.NewGuid(), null, "GBP", "Stripe", "Card",
            null, null, null, 95m, 19m, Guid.NewGuid(), "CURRENT", 0m, 78m,
            [], [], [], [], null, Loyalty: points).Serialize();
        db.OrderChargeSummaries.Add(new OrderChargeSummary { TenantId = h.TenantId, OrderId = cart.OrderId.Value,
            Currency = "GBP", Subtotal = 95m, Total = 95m, PaymentStatus = "Cancelled" });
        await db.SaveChangesAsync();

        var result = await h.BoxCarts().QuoteAsync(box.Box.CartId, CartAccessContext.ForGuest(box.CartToken));

        result.Quote.Total.Should().Be(78m);
        result.Quote.Components.Single(x => x.Key == "points").Amount.Should().Be(-5m);
        result.Quote.Components.Single(x => x.Key == "deliveryCharged").Amount.Should().Be(7m);
        result.Quote.Loyalty!.AppliedPoints.Should().Be(500);
        result.Quote.Loyalty.AvailablePoints.Should().BeNull();
    }

    [Fact]
    public async Task UnchangedPointsDraft_Should_NotRenewCartActivity_AndWritesStillRequireCurrentVersion()
    {
        var h = new BoxTestHarness();
        var cart = await h.Carts().CreateCartAsync(new("GBP"));
        var access = CartAccessContext.ForGuest(cart.AnonymousToken, cart.CartVersion);
        var saved = await h.Carts().SaveCheckoutDraftAsync(cart.Id, new(RequestedPoints: 500, Notes: "Keep me"), access);
        await using var db = h.Commerce();
        var before = await db.Carts.AsNoTracking().SingleAsync();
        h.Clock.UtcNow = h.Clock.UtcNow.AddHours(1);

        await h.Carts().SaveCheckoutDraftAsync(cart.Id, saved.Draft!, access with { ExpectedCartVersion = saved.CartVersion });

        (await db.Carts.AsNoTracking().SingleAsync()).LastActivityAtUtc.Should().Be(before.LastActivityAtUtc);
        var stale = () => h.Carts().SaveCheckoutDraftAsync(cart.Id, new(RequestedPoints: 600),
            access with { ExpectedCartVersion = "CAcGBQQDAgE=" });
        await stale.Should().ThrowAsync<CartWriteConflictException>();
        (await db.Carts.AsNoTracking().SingleAsync()).CheckoutDraftJson.Should().Be(before.CheckoutDraftJson);
    }

    private static Mock<ILoyaltyService> Loyalty(LoyaltyPolicy policy)
    {
        var result = new Mock<ILoyaltyService>(MockBehavior.Strict);
        result.Setup(x => x.GetPolicyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(policy);
        result.Setup(x => x.GetBalanceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new LoyaltyBalance(10000, 0, 10000, 100m, 0));
        return result;
    }
}
