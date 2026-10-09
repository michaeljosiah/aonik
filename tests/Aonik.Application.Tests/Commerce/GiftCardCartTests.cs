using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Contracts.Models.GiftCards;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Promotions;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.GiftCards;
using Aonik.SharedKernel.Abstractions.Loyalty;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Aonik.Application.Tests.Commerce;

public sealed class GiftCardCartTests
{
    [Theory]
    [InlineData("Email", false, 0, 0)]
    [InlineData("Post", false, 4, 3)]
    [InlineData("InFoodBox", true, 0, 3)]
    public async Task Purchase_Should_UseExactFaceAndMethodFees_WithOnlyRelevantDeliveryData(string method, bool box, int postage, int greeting)
    {
        using var h = await Harness.CreateAsync();
        var cart = new Cart { TenantId = h.TenantId, Currency = "GBP", BoxBundleProductId = box ? Guid.NewGuid() : null };
        var selected = await h.Pricing.SelectAsync(cart, await h.SelectionAsync(method));
        selected.Selection.FaceValue.Should().Be(50);
        selected.Selection.RecipientName.Should().Be("Alex Buyer");
        selected.Postage.Should().Be(postage);
        selected.GreetingCardPrice.Should().Be(greeting);
        if (method == "Email")
        {
            selected.SendAtUtc.Should().Be(new DateTime(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc));
            selected.Selection.PostalAddress.Should().BeNull();
            selected.Selection.PostingDate.Should().BeNull();
            selected.Selection.IncludeGreetingCard.Should().BeFalse();
        }
        else
        {
            selected.SendAtUtc.Should().BeNull();
            selected.Selection.RecipientEmail.Should().BeNull();
            selected.Selection.SendDate.Should().BeNull();
        }
        cart.GiftCardPurchaseJson = JsonSerializer.Serialize(selected, GiftCardPurchasePricing.Json);
        cart.Items.Add(new CartItem { TenantId = h.TenantId, CartId = cart.Id, ProductVariantId = h.GiftVariant.Id,
            LineKind = CartLineKinds.GiftCardValue, Quantity = 1, UnitPriceSnapshot = 50 });
        var items = new List<OrderItemCommand>();
        var quoted = await h.Pricing.AppendAsync(cart, items);
        quoted!.Checkout.Purchase!.FaceValue.Should().Be(50);
        quoted.Checkout.Purchase.ItemIndex.Should().Be(0);
        items.Single(x => x.ItemType == CartLineKinds.GiftCardValue).AmountIn.Should().Be(50);
        items.Sum(x => x.AmountIn).Should().Be(50 + postage + greeting);
        items.Should().OnlyContain(x => x.Quantity == 1);
    }

    [Theory]
    [InlineData("value")]
    [InlineData("fraction")]
    [InlineData("posting-day")]
    [InlineData("past-email")]
    [InlineData("address")]
    [InlineData("in-box")]
    [InlineData("method")]
    public async Task Selection_Should_RejectUnsupportedValuesAndIncompleteDelivery(string invalid)
    {
        using var h = await Harness.CreateAsync();
        var cart = new Cart { TenantId = h.TenantId, Currency = "GBP" };
        var selection = await h.SelectionAsync("Post");
        selection = invalid switch
        {
            "value" => selection with { FaceValue = 1001 },
            "fraction" => selection with { FaceValue = 19.999m },
            "posting-day" => selection with { PostingDate = new(2026, 10, 10) },
            "past-email" => selection with { DeliveryMethod = "Email", SendDate = new(2026, 10, 8) },
            "address" => selection with { PostalAddress = selection.PostalAddress! with { Line1 = "" } },
            "in-box" => selection with { DeliveryMethod = "InFoodBox" },
            _ => selection with { DeliveryMethod = "Courier" }
        };
        await h.Pricing.Invoking(x => x.SelectAsync(cart, selection)).Should().ThrowAsync<StorefrontValidationException>();
    }

    [Fact]
    public async Task AcceptedOptions_Should_BindFinanceTermsAndStoreFees_AndRequireAvailableProduct()
    {
        using var h = await Harness.CreateAsync();
        var cart = new Cart { TenantId = h.TenantId, Currency = "GBP" };
        var selection = await h.SelectionAsync("Post");
        h.FinancePolicy = h.FinancePolicy with { TermsVersion = "changed" };
        await h.Pricing.Invoking(x => x.SelectAsync(cart, selection)).Should().ThrowAsync<StorefrontValidationException>();
        selection = await h.SelectionAsync("Post");
        h.Configure(h.Store with { Postage = 5 });
        await h.Pricing.Invoking(x => x.SelectAsync(cart, selection)).Should().ThrowAsync<StorefrontValidationException>();
        selection = await h.SelectionAsync("Post");
        h.GiftVariant.IsActive = false;
        await h.Db.SaveChangesAsync();
        await h.Pricing.Invoking(x => x.SelectAsync(cart, selection)).Should().ThrowAsync<StorefrontValidationException>();
    }

    [Fact]
    public async Task Purchase_Should_RemainExactlyOneLine_AndClearThroughSameAuthorizedRoute()
    {
        using var h = await Harness.CreateAsync();
        var cart = await h.CartAsync();
        var access = h.Access(cart);
        await h.Carts.SetPurchaseAsync(cart.Id, await h.SelectionAsync("Email"), access);
        var changed = (await h.SelectionAsync("Post")) with { FaceValue = 100 };
        var result = await h.Carts.SetPurchaseAsync(cart.Id, changed, access);
        result.Purchase!.Selection.FaceValue.Should().Be(100);
        var stored = await h.LoadAsync(cart.Id);
        stored.Items.Count(x => x.LineKind == CartLineKinds.GiftCardValue).Should().Be(1);
        stored.Items.Single(x => x.LineKind == CartLineKinds.GiftCardValue).UnitPriceSnapshot.Should().Be(100);
        result.Quote.Total.Should().Be(167, "food £60 plus face £100, postage £4 and greeting £3");
        await h.Carts.SetPurchaseAsync(cart.Id, null, access);
        stored = await h.LoadAsync(cart.Id);
        stored.GiftCardPurchaseJson.Should().BeNull();
        stored.Items.Should().NotContain(x => x.LineKind == CartLineKinds.GiftCardValue);
        stored.Items.Should().ContainSingle(x => x.ProductVariantId == h.FoodVariant.Id);
    }

    [Fact]
    public async Task GiftTender_Should_StayPrivate_AndClearWithoutExposingCodeOrGrant()
    {
        using var h = await Harness.CreateAsync();
        var cart = await h.CartAsync();
        h.SetTenderQuote(20, 40);
        var result = await h.Carts.SetTenderAsync(cart.Id, Harness.Code, 20, h.Access(cart));
        result.Quote.GiftCard!.GiftAmount.Should().Be(20);
        result.Quote.GiftCard.CardAmount.Should().Be(40);
        var publicJson = JsonSerializer.Serialize(result);
        publicJson.Should().NotContain(Harness.Code).And.NotContain(Harness.Grant).And.NotContain("PrivateCartGrant");
        publicJson.Should().Contain("4321");
        var stored = await h.LoadAsync(cart.Id);
        stored.GiftCardTenderJson.Should().Contain(Harness.Grant).And.NotContain(Harness.Code);
        var ordinary = new CartService(h.Db, h.Tenant, new ProductPricingService(h.Db, h.Tenant, h.Clock), h.Clock, h.Quotes, h.Pricing);
        var dto = await ordinary.GetCartAsync(cart.Id, h.Access(cart));
        JsonSerializer.Serialize(dto).Should().NotContain(Harness.Code).And.NotContain(Harness.Grant);
        await h.Carts.SetTenderAsync(cart.Id, null, 0, h.Access(cart));
        (await h.LoadAsync(cart.Id)).GiftCardTenderJson.Should().BeNull();
    }

    [Fact]
    public async Task GiftPurchaseAndTender_Should_RejectEachOtherBeforeChangingSavedCart()
    {
        using var h = await Harness.CreateAsync();
        var purchaseCart = await h.CartAsync();
        await h.Carts.SetPurchaseAsync(purchaseCart.Id, await h.SelectionAsync("Email"), h.Access(purchaseCart));
        await h.Carts.Invoking(x => x.SetTenderAsync(purchaseCart.Id, Harness.Code, 10, h.Access(purchaseCart)))
            .Should().ThrowAsync<StorefrontValidationException>();
        var tenderCart = await h.CartAsync();
        h.SetTenderQuote(20, 40);
        await h.Carts.SetTenderAsync(tenderCart.Id, Harness.Code, 20, h.Access(tenderCart));
        var selection = await h.SelectionAsync("Email");
        await h.Carts.Invoking(x => x.SetPurchaseAsync(tenderCart.Id, selection, h.Access(tenderCart)))
            .Should().ThrowAsync<StorefrontValidationException>();
        await h.Db.SaveChangesAsync();
        (await h.LoadAsync(purchaseCart.Id)).GiftCardTenderJson.Should().BeNull();
        (await h.LoadAsync(tenderCart.Id)).GiftCardPurchaseJson.Should().BeNull();
    }

    [Fact]
    public async Task GiftWrites_Should_RequireOwnerAndVersionBeforeProviderOrInputValidation()
    {
        using var h = await Harness.CreateAsync();
        var cart = await h.CartAsync();
        await h.Carts.Invoking(x => x.SetTenderAsync(cart.Id, "", -1, CartAccessContext.ForParty(Guid.NewGuid(), "")))
            .Should().ThrowAsync<NotFoundException>();
        await h.Carts.Invoking(x => x.SetTenderAsync(cart.Id, "", -1, h.Access(cart) with { ExpectedCartVersion = "AQ==" }))
            .Should().ThrowAsync<CartWriteConflictException>();
        h.Gifts.Verify(x => x.AuthorizeForCartAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        (await h.LoadAsync(cart.Id)).GiftCardTenderJson.Should().BeNull();
    }

    [Fact]
    public async Task ConfigChangingDuringReplacement_Should_NotLeaveTrackedDeletionForLaterSave()
    {
        using var h = await Harness.CreateAsync();
        var cart = await h.CartAsync();
        await h.Carts.SetPurchaseAsync(cart.Id, await h.SelectionAsync("Email"), h.Access(cart));
        var selection = (await h.SelectionAsync("Email")) with { FaceValue = 100 };
        var original = await h.LoadAsync(cart.Id);
        var originalItemId = original.Items.Single(x => x.LineKind == CartLineKinds.GiftCardValue).Id;
        var raw = h.Raw;
        var changed = JsonSerializer.Serialize(h.Store with { Version = "changed" }, GiftCardPurchasePricing.Json);
        var reads = 0;
        h.Settings.Setup(x => x.GetTenantValueAsync(GiftCardPurchasePricing.SettingName, h.TenantId, It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<string?>(++reads == 1 ? raw : changed));
        await h.Carts.Invoking(x => x.SetPurchaseAsync(cart.Id, selection, h.Access(cart))).Should().ThrowAsync<StorefrontValidationException>();
        await h.Db.SaveChangesAsync();
        var saved = await h.LoadAsync(cart.Id);
        saved.GiftCardPurchaseJson.Should().Be(original.GiftCardPurchaseJson);
        saved.Items.Single(x => x.LineKind == CartLineKinds.GiftCardValue).Id.Should().Be(originalItemId);
    }

    [Fact]
    public async Task OrdinaryFoodGuard_Should_NotDependOnGiftFinancePolicyAvailability()
    {
        using var h = await Harness.CreateAsync();
        h.Gifts.Setup(x => x.GetPolicyAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidStateException("Gift policy unavailable"));
        await h.Pricing.RejectOrdinaryVariantAsync(h.FoodVariant.Id);
        await h.Pricing.Invoking(x => x.RejectOrdinaryVariantAsync(h.GiftVariant.Id)).Should().ThrowAsync<StorefrontValidationException>();
        h.Gifts.Verify(x => x.GetPolicyAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CouponAndPoints_Should_LeaveFaceAndFeesWhole_AndEarnOnlyOnEligibleNewMoney()
    {
        using var h = await Harness.CreateAsync();
        h.Db.Discounts.Add(new Discount { TenantId = h.TenantId, Code = "TEN", Kind = DiscountKinds.Percentage, Value = 10 });
        await h.Db.SaveChangesAsync();
        var cart = await h.CartAsync();
        cart.CheckoutDraftJson = CartDraftData.Serialize(new(RequestedPoints: 1000));
        await h.Db.SaveChangesAsync();
        var result = await h.Carts.SetPurchaseAsync(cart.Id, await h.SelectionAsync("Post"), h.Access(cart));
        var saved = await h.LoadAsync(cart.Id);
        var quote = await h.Quotes.SnapshotAsync(saved, "TEN");

        quote.Subtotal.Should().Be(117);
        quote.DiscountTotal.Should().Be(6, "only the £60 food accepts the coupon");
        quote.PointsAppliedValue.Should().Be(10);
        quote.Total.Should().Be(101);
        quote.Loyalty!.EstimatedEarnedPoints.Should().Be(188, "£44 food plus £50 purchased gift value earns; £7 fees do not");
        result.Purchase!.Selection.FaceValue.Should().Be(50);
        result.Purchase.Postage.Should().Be(4);
        result.Purchase.GreetingCardPrice.Should().Be(3);
    }

    [Fact]
    public async Task MixedFundingQuote_Should_KeepSaleAndPointsBenefit_AndReduceEarningByActualGiftShare()
    {
        using var h = await Harness.CreateAsync();
        var cart = await h.CartAsync();
        cart.CheckoutDraftJson = CartDraftData.Serialize(new(RequestedPoints: 1000));
        await h.Db.SaveChangesAsync();
        h.SetTenderQuote(20, 30);
        var result = await h.Carts.SetTenderAsync(cart.Id, Harness.Code, 20, h.Access(cart));
        result.Quote.Subtotal.Should().Be(60);
        result.Quote.PointsAppliedValue.Should().Be(10);
        result.Quote.Total.Should().Be(50);
        result.Quote.GiftCard!.GiftAmount.Should().Be(20);
        result.Quote.GiftCard.CardAmount.Should().Be(30);
        result.Quote.Loyalty!.EstimatedEarnedPoints.Should().Be(60, "only £30 of new-money eligible food earns");
        h.LastGiftRequest!.Lines.Single().PointsAppliedValue.Should().Be(10);
        h.LastGiftRequest.Total.Should().Be(50);
    }

    [Fact]
    public void GiftLineShares_Should_NotSubtractTaxOrFeesFromFoodEarning_AndKeepIntegerRefundAllocations()
    {
        var policy = new LoyaltyPolicy(true, "v1", new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        var original = LoyaltyCheckoutCalculator.Calculate(Guid.NewGuid(), Guid.NewGuid(), policy,
            [new(0, OrderTypeCodes.ProductPurchase, Guid.NewGuid(), 10, 0, true, true),
             new(1, CheckoutService.DeliveryFeeItemType, null, 2, 0, false, false)], 14, 100, new(1000, 0, 1000, 10, 0)).Checkout!;
        var adjusted = LoyaltyCheckoutCalculator.ApplyGiftFunding(original,
            [new(0, Guid.Empty, OrderTypeCodes.ProductPurchase, 10, 0, 1, 4.5m),
             new(1, Guid.Empty, CheckoutService.DeliveryFeeItemType, 2, 0, 0, 1)]);
        adjusted.EarnedPoints.Should().Be(9, "£4.50 new-money food; separate £1 tax gift share is not food spend");
        adjusted.Lines[0].NetPaidValue.Should().Be(4.5m);
        adjusted.Lines[1].EligibleEarnValue.Should().Be(0);
        adjusted.RedeemedPoints.Should().Be(original.RedeemedPoints);
        adjusted.Lines.Sum(x => x.RedeemedPoints).Should().Be(100);
        adjusted.PointsAppliedValue.Should().Be(1);
    }

    private sealed class Harness : IDisposable
    {
        public const string Code = "0123456789ABCDEF0123456789ABCDEF";
        public const string Grant = "private-purpose-protected-cart-capability";
        public Guid TenantId { get; }
        public Guid BuyerId { get; } = Guid.NewGuid();
        public ITenantProvider Tenant { get; }
        public CommerceTestHarness.TestClock Clock { get; } = new() { UtcNow = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc) };
        public CommerceDbContext Db { get; }
        public Mock<ITenantSettingStore> Settings { get; } = new();
        public Mock<IGiftCardService> Gifts { get; } = new(MockBehavior.Strict);
        public Mock<ILoyaltyService> Loyalty { get; } = new(MockBehavior.Strict);
        public GiftCardPolicy FinancePolicy { get; set; } = new(true, "finance-v1", "GBP",
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), new(true), "terms-v1", GiftCardSettings.Proportional);
        public GiftCardStorefrontOptions Store { get; private set; } = new();
        public string Raw { get; private set; } = "";
        public ProductVariant GiftVariant { get; private set; } = null!;
        public ProductVariant FoodVariant { get; private set; } = null!;
        public GiftCardPurchasePricing Pricing { get; }
        public CartDiscountQuotes Quotes { get; }
        public GiftCardCartService Carts { get; }
        public GiftCardQuoteRequest? LastGiftRequest { get; private set; }

        private Harness()
        {
            var (options, tenant) = CommerceTestHarness.NewDb();
            TenantId = tenant;
            Tenant = new TestTenantProvider(tenant);
            Db = CommerceTestHarness.CreateContext(options, tenant, Clock);
            Settings.Setup(x => x.GetTenantValueAsync(GiftCardPurchasePricing.SettingName, tenant, It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<string?>(Raw));
            Gifts.Setup(x => x.GetPolicyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => FinancePolicy);
            Gifts.Setup(x => x.AuthorizeForCartAsync(Code, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GiftCardAuthorization(Grant, new("•••• 4321", "GBP", 100, 0, 100, "Active", null)));
            Loyalty.Setup(x => x.GetPolicyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new LoyaltyPolicy(true, "points-v1",
                new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())));
            Loyalty.Setup(x => x.GetBalanceAsync(BuyerId, It.IsAny<CancellationToken>())).ReturnsAsync(new LoyaltyBalance(10000, 0, 10000, 100, 0));
            Pricing = new(Db, Tenant, Settings.Object, Gifts.Object, Clock);
            Quotes = new(Db, Tenant, new DiscountService(Db, Tenant, Clock), new ZeroRateTaxCalculator(), Settings.Object,
                new NullSettingProvider(), new GbpTenantCurrencyProvider(), new CheckoutLoyaltyQuotes(Db, Tenant, Loyalty.Object), Pricing, new CheckoutGiftCards(Gifts.Object));
            Carts = new(Db, Tenant, Clock, Pricing, Gifts.Object, Quotes);
        }
        public static async Task<Harness> CreateAsync()
        {
            var h = new Harness();
            var gift = new Product { TenantId = h.TenantId, Name = "Gift value", Status = ProductStatuses.Active, Kind = ProductKinds.Simple, IsPlaceholder = false };
            var food = new Product { TenantId = h.TenantId, Name = "Food", Status = ProductStatuses.Active, Kind = ProductKinds.Simple, IsPlaceholder = false };
            h.GiftVariant = new() { TenantId = h.TenantId, ProductId = gift.Id, IsActive = true, Name = "Gift value", Sku = "GIFT" };
            h.FoodVariant = new() { TenantId = h.TenantId, ProductId = food.Id, IsActive = true, Name = "Food", Sku = "FOOD" };
            h.Db.Products.AddRange(gift, food);
            h.Db.ProductVariants.AddRange(h.GiftVariant, h.FoodVariant);
            await h.Db.SaveChangesAsync();
            h.Configure(new(true, "store-v1", h.GiftVariant.Id, [25, 50, 100], 10, 500,
                ["Email", "Post", "InFoodBox"], 4, 3, "Europe/London", new(9, 0),
                [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday], "ExcludeFaceValue"));
            return h;
        }
        public void Configure(GiftCardStorefrontOptions value)
        {
            Store = value;
            Raw = JsonSerializer.Serialize(value, GiftCardPurchasePricing.Json);
        }
        public async Task<GiftCardPurchaseSelection> SelectionAsync(string method) => new(50, method, "  Alex Buyer  ",
            (await Pricing.OptionsAsync()).Version, "alex@example.test", new(2026, 10, 12), new(2026, 10, 12),
            new("1 Test Street", null, "London", null, "SW1A 1AA", " gb "), "+44 7700 900123", "Enjoy", "Pat", true);
        public async Task<Cart> CartAsync()
        {
            var cart = new Cart { TenantId = TenantId, BuyerPartyId = BuyerId, Currency = "GBP", Status = CartStatuses.Open };
            cart.Items.Add(new CartItem { TenantId = TenantId, CartId = cart.Id, ProductVariantId = FoodVariant.Id,
                Quantity = 1, UnitPriceSnapshot = 60, Sku = "FOOD", NameSnapshot = "Food" });
            Db.Carts.Add(cart);
            await Db.SaveChangesAsync();
            return cart;
        }
        public CartAccessContext Access(Cart cart) => CartAccessContext.ForParty(BuyerId, Convert.ToBase64String(cart.RowVersion));
        public Task<Cart> LoadAsync(Guid id) => Db.Carts.AsNoTracking().Include(x => x.Items).SingleAsync(x => x.Id == id);
        public void SetTenderQuote(decimal gift, decimal card)
        {
            Gifts.Setup(x => x.QuoteAsync(It.IsAny<GiftCardQuoteRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((GiftCardQuoteRequest request, CancellationToken _) =>
                {
                    LastGiftRequest = request;
                    var tender = new GiftCardTender(request.PrivateCartGrant, gift, card, request.TaxTotal, 0,
                        [request.Lines.Single() with { GiftFundedValue = gift }]);
                    return new GiftCardFundingQuote(request.RequestedAmount, 100, gift, card,
                        new(request.CartId, FinancePolicy.Version, FinancePolicy.Ledger!, FinancePolicy.Validity!, FinancePolicy.TermsVersion!,
                            FinancePolicy.FundingAllocation!, Tender: tender));
                });
        }
        public void Dispose() => Db.Dispose();
    }
}
