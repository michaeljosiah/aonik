using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Contracts.Models.GiftCards;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Inventory;
using Aonik.Commerce.Services.Promotions;
using Aonik.Infrastructure.Multitenancy;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.GiftCards;
using Aonik.SharedKernel.Abstractions.Loyalty;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Aonik.Application.Tests.Commerce;

public sealed class GiftCardQuoteAvailabilityTests
{
    [Theory]
    [InlineData("disabled")]
    [InlineData("changed")]
    [InlineData("retired")]
    [InlineData("deleted")]
    [InlineData("finance-unavailable")]
    public async Task CartRead_Should_RetainSavedFaceAndFees_WithUnavailableStatusAndRemovalVersion(string change)
    {
        using var h = await Harness.CreateAsync();
        var selected = await h.SelectAsync();
        var before = await h.LoadAsync();
        await h.MakeUnavailableAsync(change);

        var read = await h.Carts.GetCartAsync(h.CartId, h.Access(selected.CartVersion));

        read.Should().NotBeNull();
        read!.GiftCardPurchase.Should().Be(selected.Purchase);
        read.Quote!.GiftCardPurchaseStatus!.Code.Should().Be("gift_card.purchase_unavailable");
        read.Quote.GiftCardPurchaseStatus.Message.Should().NotContain("private configuration detail");
        read.Quote.Subtotal.Should().Be(57m, "the saved £50 face, £4 postage and £3 greeting remain visible");
        read.Quote.TaxTotal.Should().Be(.7m, "face value remains excluded from tax");
        read.Quote.Total.Should().Be(57.7m);
        read.Quote.Loyalty.Should().BeNull("unavailable selections must not promise future earning");
        var after = await h.LoadAsync();
        after.GiftCardPurchaseJson.Should().Be(before.GiftCardPurchaseJson);
        after.LastActivityAtUtc.Should().Be(before.LastActivityAtUtc);
        after.RowVersion.Should().Equal(before.RowVersion);
        after.Items.Single().Id.Should().Be(before.Items.Single().Id);

        var removed = await h.GiftCarts.SetPurchaseAsync(h.CartId, null, h.Access(read.CartVersion));
        removed.Purchase.Should().BeNull();
        removed.Quote.GiftCardPurchaseStatus.Should().BeNull();
        removed.Quote.Total.Should().Be(0m);
    }

    [Fact]
    public async Task CurrentBoxRead_Should_KeepGiftComponentsAndStatus_WhenOfferChanges()
    {
        using var h = await Harness.CreateAsync(box: true);
        var selected = await h.SelectAsync();
        await h.MakeUnavailableAsync("changed");

        var read = await h.Boxes.GetCurrentAsync(h.PartyId);

        read.Should().NotBeNull();
        read!.GiftCardPurchase.Should().Be(selected.Purchase);
        read.Quote.GiftCardPurchaseStatus!.Code.Should().Be("gift_card.purchase_unavailable");
        read.Quote.Components.Single(x => x.Key == "giftCardValue").Amount.Should().Be(50m);
        read.Quote.Components.Single(x => x.Key == "giftCardPostage").Amount.Should().Be(4m);
        read.Quote.Components.Single(x => x.Key == "giftCardGreeting").Amount.Should().Be(3m);
        read.Quote.Total.Should().Be(162.2m, "£95 box plus the saved gift and tax on food/fees");
        read.Quote.Components.Sum(x => x.Amount).Should().Be(read.Quote.Total);
        await using var verify = h.Base.Commerce();
        (await verify.CartDeliveryReservations.CountAsync()).Should().Be(0);
        (await verify.InventoryReservations.CountAsync()).Should().Be(0);
        (await h.GiftCarts.SetPurchaseAsync(h.CartId, null, h.Access(read.CartVersion))).Purchase.Should().BeNull();
    }

    [Fact]
    public async Task UnavailableQuote_Should_NotResolveDeletedGiftForPoints_OrRelaxPurchaseAndCheckoutValidation()
    {
        using var h = await Harness.CreateAsync();
        var selected = await h.SelectAsync();
        var cart = await h.Db.Carts.SingleAsync(x => x.Id == h.CartId);
        cart.CheckoutDraftJson = CartDraftData.Serialize(new(RequestedPoints: 100));
        await h.Db.SaveChangesAsync();
        await h.MakeUnavailableAsync("deleted");

        var read = (await h.Carts.GetCartAsync(h.CartId, h.Access(selected.CartVersion)))!;

        read.Quote!.Loyalty!.RequestedPoints.Should().Be(100);
        read.Quote.Loyalty.ReasonCode.Should().Be("gift_card.purchase_unavailable");
        read.Quote.PointsAppliedValue.Should().Be(0m);
        read.Quote.Total.Should().Be(57.7m);
        var current = await h.LoadAsync();
        await h.Pricing.Invoking(x => x.AppendAsync(current, new List<OrderItemCommand>()))
            .Should().ThrowAsync<StorefrontValidationException>();
        await h.GiftCarts.Invoking(x => x.SetPurchaseAsync(h.CartId, selected.Purchase!.Selection, h.Access(read.CartVersion)))
            .Should().ThrowAsync<StorefrontValidationException>();
        (await h.LoadAsync()).GiftCardPurchaseJson.Should().Be(current.GiftCardPurchaseJson);
    }

    [Fact]
    public async Task FrozenRead_Should_UseRecordedTotalWithoutCheckingCurrentGiftOffer()
    {
        using var h = await Harness.CreateAsync();
        var selected = await h.SelectAsync();
        var cart = await h.Db.Carts.SingleAsync(x => x.Id == h.CartId);
        cart.Status = CartStatuses.CheckedOut;
        cart.OrderId = Guid.NewGuid();
        h.Db.OrderChargeSummaries.Add(new OrderChargeSummary
        {
            TenantId = h.Base.TenantId, OrderId = cart.OrderId.Value, Currency = "GBP",
            Subtotal = 57m, TaxTotal = .7m, Total = 57.7m, PaymentStatus = CheckoutPaymentStatuses.Captured
        });
        await h.Db.SaveChangesAsync();
        await h.MakeUnavailableAsync("finance-unavailable");
        h.Gifts.Invocations.Clear();

        var read = (await h.Carts.GetCartAsync(h.CartId, h.Access(selected.CartVersion)))!;

        read.Quote!.Total.Should().Be(57.7m);
        read.Quote.GiftCardPurchaseStatus.Should().BeNull();
        read.GiftCardPurchase.Should().Be(selected.Purchase);
        h.Gifts.Verify(x => x.GetPolicyAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CorruptSavedAmount_Should_StillFail_WithoutInventingAnAvailableOrZeroPrice()
    {
        using var h = await Harness.CreateAsync();
        var selected = await h.SelectAsync();
        await h.MakeUnavailableAsync("disabled");
        var line = await h.Db.CartItems.SingleAsync(x => x.CartId == h.CartId);
        line.UnitPriceSnapshot = 49m;
        await h.Db.SaveChangesAsync();

        await h.Carts.Invoking(x => x.GetCartAsync(h.CartId, h.Access(selected.CartVersion)))
            .Should().ThrowAsync<StorefrontValidationException>().WithMessage("*saved gift-card selection is invalid*");
    }

    private sealed class Harness : IDisposable
    {
        public BoxTestHarness Base { get; } = new();
        public Guid PartyId { get; } = Guid.NewGuid();
        public Guid CartId { get; private set; }
        public CommerceDbContext Db { get; }
        public Mock<IGiftCardService> Gifts { get; } = new(MockBehavior.Strict);
        public GiftCardPurchasePricing Pricing { get; }
        public GiftCardCartService GiftCarts { get; }
        public CartService Carts { get; }
        public BoxCartService Boxes { get; }
        private GiftCardStorefrontOptions _store = new();
        private ProductVariant _gift = null!;

        private Harness()
        {
            Base.Clock.UtcNow = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
            Db = Base.Commerce();
            var tenant = new TestTenantProvider(Base.TenantId);
            var settings = new DictionaryTenantSettingStore(Base.Settings);
            Gifts.Setup(x => x.GetPolicyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new GiftCardPolicy(true,
                "finance-v1", "GBP", new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
                new(true), "terms-v1", GiftCardSettings.Proportional));
            var loyalty = new Mock<ILoyaltyService>(MockBehavior.Strict);
            loyalty.Setup(x => x.GetPolicyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new LoyaltyPolicy(true,
                "points-v1", new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())));
            loyalty.Setup(x => x.GetBalanceAsync(PartyId, It.IsAny<CancellationToken>())).ReturnsAsync(new LoyaltyBalance(1000, 0, 1000, 10m, 0));
            Pricing = new(Db, tenant, settings, Gifts.Object, Base.Clock);
            var quotes = new CartDiscountQuotes(Db, tenant, new DiscountService(Db, tenant, Base.Clock), new TenPercentTax(),
                settings, new NullSettingProvider(), new GbpTenantCurrencyProvider(), new CheckoutLoyaltyQuotes(Db, tenant, loyalty.Object), Pricing);
            var productPricing = new ProductPricingService(Db, tenant, Base.Clock);
            GiftCarts = new(Db, tenant, Base.Clock, Pricing, Gifts.Object, quotes);
            Carts = new(Db, tenant, productPricing, Base.Clock, quotes, Pricing);
            Boxes = new(Db, tenant, CommerceTestHarness.NewSelectionService(Db, Base.TenantId),
                new InventoryService(Db, tenant, new TenantContext { TenantId = Base.TenantId }, Base.Clock),
                settings, new NullSettingProvider(), new GbpTenantCurrencyProvider(), productPricing, Base.Clock, quotes, Pricing);
        }

        public static async Task<Harness> CreateAsync(bool box = false)
        {
            var h = new Harness();
            var product = new Product { TenantId = h.Base.TenantId, Name = "Gift", Slug = "gift",
                Kind = ProductKinds.Simple, Status = ProductStatuses.Active, IsPlaceholder = false };
            h._gift = new ProductVariant { TenantId = h.Base.TenantId, ProductId = product.Id, Name = "Gift value", Sku = "GIFT", IsActive = true };
            h.Db.Products.Add(product);
            h.Db.ProductVariants.Add(h._gift);
            await h.Db.SaveChangesAsync();
            h._store = new(true, "store-v1", h._gift.Id, [50m], DeliveryMethods: ["Post"],
                Postage: 4m, GreetingCardPrice: 3m, Timezone: "Europe/London", EmailSendTime: new(9, 0),
                PostingDays: [DayOfWeek.Monday], TaxTreatment: "ExcludeFaceValue");
            h.WriteStore();
            if (box)
            {
                var fixture = await h.Base.BuildAsync("dish");
                h.CartId = (await h.Boxes.CreateAsync(new(fixture.BundleProductId, 6, BuyerPartyId: h.PartyId))).Box.CartId;
            }
            else h.CartId = (await h.Carts.CreateCartAsync(new("GBP", BuyerPartyId: h.PartyId))).Id;
            return h;
        }

        public async Task<GiftCardCartResponse> SelectAsync()
        {
            var options = await Pricing.OptionsAsync();
            var cart = await LoadAsync();
            return await GiftCarts.SetPurchaseAsync(CartId, new(50m, "Post", "Recipient", options.Version,
                PostingDate: new(2026, 10, 12), PostalAddress: new("1 Street", null, "London", null, "SW1A 1AA", "GB"),
                RecipientPhone: "+447700900123", IncludeGreetingCard: true), Access(Convert.ToBase64String(cart.RowVersion)));
        }

        public async Task MakeUnavailableAsync(string change)
        {
            if (change == "finance-unavailable")
                Gifts.Setup(x => x.GetPolicyAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidStateException("private configuration detail"));
            else if (change is "disabled" or "changed")
            {
                _store = change == "disabled" ? _store with { Enabled = false } : _store with { Postage = 9m, Version = "store-v2" };
                WriteStore();
            }
            else
            {
                _gift.IsActive = false;
                if (change == "deleted") _gift.IsDeleted = true;
                await Db.SaveChangesAsync();
            }
        }

        private void WriteStore() => Base.Settings[GiftCardPurchasePricing.SettingName] = JsonSerializer.Serialize(_store, GiftCardPurchasePricing.Json);
        public CartAccessContext Access(string version) => CartAccessContext.ForParty(PartyId, version);
        public Task<Cart> LoadAsync() => Db.Carts.AsNoTracking().Include(x => x.Items).SingleAsync(x => x.Id == CartId);
        public void Dispose() => Db.Dispose();
    }

    private sealed class TenPercentTax : ITaxCalculator
    {
        public Task<decimal> CalculateAsync(decimal taxableAmount, string currency, CancellationToken cancellationToken = default)
            => Task.FromResult(taxableAmount * .1m);
    }
}
