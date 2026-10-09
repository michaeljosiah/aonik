using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Promotions;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Aonik.Application.Tests.Commerce;

public class GreetingCardQuoteTests
{
    private const string Offer = """{"isEnabled":true,"currency":"GBP","amount":3}""";

    [Fact]
    public async Task LiveBoxAndCouponQuotes_Should_IncludeOneCardInGoodsAndReuseDiscountAndTaxArithmetic()
    {
        var (h, _, box, access) = await ArrangeAsync();
        await using var db = h.Commerce();
        await new DiscountService(db, new TestTenantProvider(h.TenantId), h.Clock)
            .CreateAsync(new("TEN", DiscountKinds.FixedAmount, 10m, "GBP"));
        await h.Carts().SaveCheckoutDraftAsync(box.Box.CartId,
            new CartCheckoutDraftDto(Gift: new(true, IncludeGreetingCard: true), DiscountCode: "TEN"), access);
        var tax = new Mock<ITaxCalculator>();
        tax.Setup(x => x.CalculateAsync(91m, "GBP", It.IsAny<CancellationToken>())).ReturnsAsync(2m);
        var boxes = Boxes(h, tax.Object);

        var quoted = await boxes.GetAsync(box.Box.CartId, access);
        var cart = await db.Carts.AsNoTracking().Include(x => x.Items).SingleAsync();
        var couponQuote = await CommerceTestHarness.NewDiscountQuotes(db, h.TenantId, h.Clock,
            new DictionaryTenantSettingStore(h.Settings), tax.Object).SnapshotAsync(cart, "TEN");

        quoted.Quote.Components.Should().Contain(new QuoteComponentDto("boxPrice", 90m))
            .And.Contain(new QuoteComponentDto("addOns", 8m))
            .And.Contain(new QuoteComponentDto("greetingCard", 3m))
            .And.Contain(new QuoteComponentDto("discount", -10m))
            .And.Contain(new QuoteComponentDto("tax", 2m))
            .And.Contain(new QuoteComponentDto("deliveryCharged", 7m));
        quoted.Quote.Total.Should().Be(100m);
        quoted.Quote.Components.Count(x => x.Key == "greetingCard").Should().Be(1);
        quoted.Box.Lines.Should().HaveCount(2, "the card is not a food selection or inventory add-on");
        couponQuote.Subtotal.Should().Be(101m);
        couponQuote.Total.Should().Be(quoted.Quote.Total);
        tax.Verify(x => x.CalculateAsync(91m, "GBP", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FrozenBox_Should_KeepRecordedCardFeeAndBoxSplit_AfterCurrentOfferDisappears(bool preparing)
    {
        var (h, _, box, access) = await ArrangeAsync();
        await h.Carts().SaveCheckoutDraftAsync(box.Box.CartId, new(Gift: new(true, IncludeGreetingCard: true)), access);
        await using (var db = h.Commerce())
        {
            var cart = await db.Carts.SingleAsync();
            cart.OrderId = Guid.NewGuid();
            if (preparing)
            {
                cart.CheckoutState = CartCheckoutStates.Preparing;
                cart.CheckoutPreparationJson = new CheckoutPreparation(Guid.NewGuid(), null, "GBP", "Stripe", "Card",
                    null, null, null, 101m, 10m, null, "TEN", 2m, 100m, [], [], [], [], null,
                    GreetingCardCharged: 3m).Serialize();
            }
            else cart.Status = CartStatuses.CheckedOut;
            db.OrderChargeSummaries.Add(new OrderChargeSummary
            {
                TenantId = h.TenantId, OrderId = cart.OrderId.Value, Currency = "GBP",
                Subtotal = preparing ? 999m : 101m, DiscountTotal = 10m, DiscountCode = "TEN", TaxTotal = 2m,
                Total = preparing ? 1000m : 100m, GreetingCardCharged = preparing ? 99m : 3m
            });
            await db.SaveChangesAsync();
        }
        h.Settings.Remove(CommerceSettingNames.StorefrontGreetingCard);

        var frozen = await h.BoxCarts().GetAsync(box.Box.CartId, access);

        frozen.Quote.Components.Single(x => x.Key == "boxPrice").Amount.Should().Be(90m);
        frozen.Quote.Components.Single(x => x.Key == "addOns").Amount.Should().Be(8m);
        frozen.Quote.Components.Single(x => x.Key == "greetingCard").Amount.Should().Be(3m);
        frozen.Quote.Components.Single(x => x.Key == "deliveryCharged").Amount.Should().Be(7m);
        frozen.Quote.Total.Should().Be(100m);
    }

    [Fact]
    public async Task BoxPreparation_Should_KeepCardSeparateFromBoxAggregateAndInventory()
    {
        var (h, _, box, access) = await ArrangeAsync();
        await h.Carts().SaveCheckoutDraftAsync(box.Box.CartId, new(Gift: new(true, IncludeGreetingCard: true)), access);
        await using var db = h.Commerce();
        var cart = await db.Carts.Include(x => x.Items).ThenInclude(x => x.Selections).SingleAsync();

        var shape = await h.BoxCarts(db).PrepareForCheckoutAsync(cart);

        shape.GoodsTotal.Should().Be(90m);
        shape.AddOnGoodsTotal.Should().Be(8m);
        shape.GreetingCardCharged.Should().Be(3m);
        shape.Lines.Should().ContainSingle();
        shape.AddOnLines.Should().ContainSingle();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("""{"isEnabled":true,"currency":"USD","amount":3}""")]
    public async Task SelectedUnavailableCard_Should_NeverQuoteFree_AndCanBeRemovedWithoutLosingTheBox(string? offer)
    {
        var (h, _, box, access) = await ArrangeAsync();
        var draft = new CartCheckoutDraftDto(Gift: new(true, IncludeGreetingCard: true, GreetingCardMessage: "Enjoy!"));
        await h.Carts().SaveCheckoutDraftAsync(box.Box.CartId, draft, access);
        if (offer is null) h.Settings.Remove(CommerceSettingNames.StorefrontGreetingCard);
        else h.Settings[CommerceSettingNames.StorefrontGreetingCard] = offer;
        var quote = () => h.BoxCarts().GetAsync(box.Box.CartId, access);

        (await quote.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("Gift.IncludeGreetingCard");
        await h.Carts().SaveCheckoutDraftAsync(box.Box.CartId, draft with { Gift = null }, access);
        var restored = await h.BoxCarts().GetAsync(box.Box.CartId, access);

        restored.Box.Lines.Should().HaveCount(2);
        restored.Quote.Components.Should().NotContain(x => x.Key == "greetingCard");
        restored.Quote.Total.Should().Be(105m);
    }

    [Fact]
    public async Task Helper_Should_ReadOnlyTheExactTenantOffer_AndNotReadSettingsForUnselectedCard()
    {
        var tenant = Guid.NewGuid();
        var settings = new Mock<ITenantSettingStore>(MockBehavior.Strict);
        settings.Setup(x => x.GetTenantValueAsync(CommerceSettingNames.StorefrontGreetingCard, tenant, It.IsAny<CancellationToken>())).ReturnsAsync(Offer);
        settings.Setup(x => x.GetTenantValueAsync(CommerceSettingNames.StorefrontGreetingCard, It.Is<Guid>(id => id != tenant), It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        (await GreetingCardPricing.ResolveAsync(settings.Object, tenant, "GBP", new(true))).Should().Be(0m);
        settings.Verify(x => x.GetTenantValueAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        (await GreetingCardPricing.ResolveAsync(settings.Object, tenant, "GBP", new(true, IncludeGreetingCard: true))).Should().Be(3m);
        (await GreetingCardPricing.ReadAsync(settings.Object, Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task DiscountAdapter_Should_IncludeGreetingGoodsWithoutInventingProductIdentity_AndExcludeStoredValue()
    {
        var h = new BoxTestHarness();
        await using var db = h.Commerce();
        OrderItemCommand[] items = [GreetingCardPricing.Item(2, 3m, "GBP"),
            new("GiftCardValue", 3, 50, "GBP"), new(CheckoutService.DeliveryFeeItemType, 4, 7, "GBP")];

        var lines = await CheckoutDiscountLines.FromOrderItemsAsync(db, h.TenantId, items);

        lines.Should().Equal(new DiscountChargeLine(2, null, 3m));
        var missingProduct = () => CheckoutDiscountLines.FromOrderItemsAsync(db, h.TenantId,
            [new(OrderTypeCodes.ProductPurchase, 0, 10, "GBP")]);
        await missingProduct.Should().ThrowAsync<DiscountException>();
    }

    private static async Task<(BoxTestHarness H, BoxTestHarness.BoxFixture Fixture, BoxCartDto Box, CartAccessContext Access)> ArrangeAsync()
    {
        var h = new BoxTestHarness();
        h.Settings[CommerceSettingNames.StorefrontGreetingCard] = Offer;
        h.Settings[CommerceSettingNames.StorefrontDeliveryChargedAmount] = "7";
        var fixture = await h.BuildAsync("dish");
        await h.Plans().UpsertAsync(fixture.BundleProductId, new(6, 30, 6, 90m, 15m, "GBP", []));
        var (_, extra) = await h.AddExtraAsync("sauce", 8m);
        var box = await h.BoxCarts().CreateAsync(new(fixture.BundleProductId, 6, new(fixture.DishVariants["dish"], 6)));
        var access = CartAccessContext.ForGuest(box.CartToken, box.CartVersion);
        var withExtra = await h.BoxCarts().AddExtraLineAsync(box.Box.CartId, new(extra, 1), access);
        return (h, fixture, withExtra, access with { ExpectedCartVersion = withExtra.CartVersion });
    }

    private static BoxCartService Boxes(BoxTestHarness h, ITaxCalculator tax)
    {
        var db = h.Commerce();
        var tenant = new TestTenantProvider(h.TenantId);
        var settings = new DictionaryTenantSettingStore(h.Settings);
        return new(db, tenant, CommerceTestHarness.NewSelectionService(db, h.TenantId), h.Inventory(),
            settings, new NullSettingProvider(), new GbpTenantCurrencyProvider(), h.Pricing(), h.Clock,
            CommerceTestHarness.NewDiscountQuotes(db, h.TenantId, h.Clock, settings, tax));
    }
}
