using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Promotions;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.GiftCards;
using Aonik.SharedKernel.Abstractions.Loyalty;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

namespace Aonik.Application.Tests.Commerce;

public sealed class CheckoutRefundSourceReaderTests
{
    [Fact]
    public async Task Read_Should_UseOriginalDiscountPointsAndGiftAllocationsIncludingFeesAndTax()
    {
        await using var f = await Fixture.CreateAsync();

        var source = await f.Reader().ReadAsync(f.Summary.OrderId);

        source.Should().NotBeNull();
        source!.OrderId.Should().Be(f.Summary.OrderId);
        source.PaymentIntentId.Should().Be(f.Summary.PaymentIntentId);
        source.InvoiceId.Should().Be(f.Summary.InvoiceId);
        source.Total.Should().Be(38m);
        source.GiftAmount.Should().Be(10m);
        source.CardAmount.Should().Be(28m);
        source.Components.Select(x => x.Amount).Should().Equal(9m, 18m, 3m, 5m, 3m);
        source.Components.Select(x => x.GiftFundedValue).Should().Equal(2.5m, 4.5m, 0.75m, 1.25m, 1m);
        source.Components.Select(x => x.EarnedPoints).Should().Equal(13, 27, 0, 0, 0);
        source.Components.Select(x => x.RedeemedPoints).Should().Equal(67, 133, 0, 0, 0);
        source.Components[0].Label.Should().Be("Original dish name");
        source.Components[0].OrderItemId.Should().Be(f.Order!.Items[0].Id);
        source.Components[^1].ComponentId.Should().Be("tax");
        source.Components[^1].OrderItemId.Should().BeNull();
        System.Text.Json.JsonSerializer.Serialize(source).Should().NotContain("private-cart-grant");
        f.Db.ChangeTracker.HasChanges().Should().BeFalse("reading original facts does not write refund effects");
    }

    [Fact]
    public async Task Read_Should_KeepFourDecimalDiscountSharesUntilRefundNormalization()
    {
        await using var f = await Fixture.CreateAsync();
        var items = f.Order!.Items.Take(2).ToArray();
        f.Order = f.Order with { Items = items, AmountIn = 30m };
        f.Summary.Subtotal = 30m;
        f.Summary.PointsAppliedValue = 0m;
        f.Summary.LoyaltyJson = null;
        f.Summary.GiftCardJson = null;
        f.Summary.TaxTotal = 0m;
        f.Summary.Total = 29m;
        await f.Db.SaveChangesAsync();

        var source = await f.Reader().ReadAsync(f.Summary.OrderId);

        source!.Components.Select(x => x.Amount).Should().Equal(9.6667m, 19.3333m);
        source.Components.Sum(x => x.Amount).Should().Be(29m);
        source.CardAmount.Should().Be(29m);
        source.Components.Should().OnlyContain(x => x.GiftFundedValue == 0m && x.EarnedPoints == 0 && x.RedeemedPoints == 0);
    }

    [Fact]
    public async Task Read_Should_PreserveZeroPaidComponentWithOriginalRedeemedPoints()
    {
        await using var f = await Fixture.CreateAsync();
        var item = f.Order!.Items[0] with { AmountIn = 1m };
        f.Order = f.Order with { Items = [item], AmountIn = 1m };
        f.Summary.Subtotal = 1m;
        f.Summary.DiscountTotal = 0m;
        f.Summary.DiscountAllocationsJson = null;
        f.Summary.PointsAppliedValue = 1m;
        f.Summary.LoyaltyJson = CheckoutLoyaltyData.Serialize(f.Loyalty with
        {
            RedeemedPoints = 100, EarnedPoints = 0, PointsAppliedValue = 1m,
            OrderValueBeforePoints = 1m, PayableTotal = 0m,
            Lines = [new(0, item.Id, item.ItemType, item.ProductId, 1m, 0m, 1m, 0m, 0m, 0m, 0, 100, true, true)]
        });
        f.Summary.GiftCardJson = null;
        f.Summary.TaxTotal = 0m;
        f.Summary.Total = 0m;
        await f.Db.SaveChangesAsync();

        var source = await f.Reader().ReadAsync(f.Summary.OrderId);

        source!.Components.Should().ContainSingle();
        source.Components[0].Amount.Should().Be(0m);
        source.Components[0].RedeemedPoints.Should().Be(100);
        source.Total.Should().Be(0m);
    }

    [Theory]
    [InlineData("missing-discounts")]
    [InlineData("missing-points")]
    [InlineData("missing-gift")]
    [InlineData("gift-line-mismatch")]
    [InlineData("gift-points-mismatch")]
    [InlineData("unknown-discount-line")]
    [InlineData("duplicate-discount-line")]
    [InlineData("points-total-mismatch")]
    [InlineData("original-charge-mismatch")]
    [InlineData("total-mismatch")]
    public async Task Read_Should_FailClosedWhenSavedFundingFactsDoNotReconcile(string corruption)
    {
        await using var f = await Fixture.CreateAsync();
        switch (corruption)
        {
            case "missing-discounts": f.Summary.DiscountAllocationsJson = null; break;
            case "missing-points": f.Summary.LoyaltyJson = null; break;
            case "missing-gift": f.Summary.GiftCardJson = null; break;
            case "gift-line-mismatch":
                f.Summary.GiftCardJson = CheckoutGiftCards.Serialize(f.Gift with
                {
                    Tender = f.Gift.Tender! with { Lines = f.Gift.Tender.Lines.Select((x, i) => i == 0 ? x with { CouponDiscount = 0m } : x).ToArray() }
                });
                break;
            case "gift-points-mismatch":
                f.Summary.LoyaltyJson = CheckoutLoyaltyData.Serialize(f.Loyalty with
                {
                    Lines = f.Loyalty.Lines.Select((x, i) => i == 0 ? x with { GiftFundedValue = 0m } : x).ToArray()
                });
                break;
            case "unknown-discount-line":
                f.Summary.DiscountAllocationsJson = DiscountAllocationSnapshot.Serialize([new(Guid.NewGuid(), 1m)]);
                break;
            case "duplicate-discount-line":
                f.Summary.DiscountAllocationsJson = DiscountAllocationSnapshot.Serialize([new(f.Order!.Items[0].Id, 0.5m), new(f.Order.Items[0].Id, 0.5m)]);
                break;
            case "points-total-mismatch":
                f.Summary.LoyaltyJson = CheckoutLoyaltyData.Serialize(f.Loyalty with { EarnedPoints = 41 });
                break;
            case "original-charge-mismatch":
                f.Summary.LoyaltyJson = CheckoutLoyaltyData.Serialize(f.Loyalty with
                {
                    Lines = f.Loyalty.Lines.Select((x, i) => i == 0 ? x with { OriginalCharged = 11m } : x).ToArray()
                });
                break;
            case "total-mismatch": f.Summary.Total = 39m; break;
        }
        await f.Db.SaveChangesAsync();

        var read = () => f.Reader().ReadAsync(f.Summary.OrderId);

        await read.Should().ThrowAsync<InvalidStateException>();
    }

    [Theory]
    [InlineData("missing-order")]
    [InlineData("foreign-order")]
    [InlineData("other-currency")]
    [InlineData("unpaid")]
    public async Task Read_Should_RejectUnavailableOrMismatchedOrder(string state)
    {
        await using var f = await Fixture.CreateAsync();
        switch (state)
        {
            case "missing-order": f.Order = null; break;
            case "foreign-order": f.Order = f.Order! with { TenantId = Guid.NewGuid() }; break;
            case "other-currency": f.Order = f.Order! with { CurrencyIn = "USD" }; break;
            case "unpaid": f.Summary.PaymentStatus = "Processing"; break;
        }
        await f.Db.SaveChangesAsync();

        var read = () => f.Reader().ReadAsync(f.Summary.OrderId);

        await read.Should().ThrowAsync<InvalidStateException>();
    }

    [Fact]
    public async Task Read_Should_ReturnNullBeforeOrderLookupForForeignMissingOrDeletedSummary()
    {
        await using var f = await Fixture.CreateAsync();

        (await f.Reader(Guid.NewGuid()).ReadAsync(f.Summary.OrderId)).Should().BeNull();
        (await f.Reader().ReadAsync(Guid.NewGuid())).Should().BeNull();
        f.Summary.IsDeleted = true;
        await f.Db.SaveChangesAsync();
        (await f.Reader().ReadAsync(f.Summary.OrderId)).Should().BeNull();

        f.Orders.Verify(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly BoxTestHarness _h = new();
        public CommerceDbContext Db { get; }
        public Mock<IOrderService> Orders { get; } = new(MockBehavior.Strict);
        public OrderDto? Order { get; set; }
        public OrderChargeSummary Summary { get; }
        public LoyaltyCheckout Loyalty { get; }
        public GiftCardCheckout Gift { get; }

        private Fixture()
        {
            Db = _h.Commerce();
            var items = new[]
            {
                Item(0, "ProductPurchase", 10m, "Original dish name"),
                Item(1, "ProductPurchase", 20m, "Second dish"),
                Item(2, CheckoutService.GreetingCardItemType, 3m, "Greeting card"),
                Item(3, CheckoutService.DeliveryFeeItemType, 5m, "Delivery")
            };
            Order = new(Guid.NewGuid(), _h.TenantId, "ProductPurchase", "Paid", Guid.NewGuid(), 38m,
                "GBP", _h.Clock.UtcNow, items, "ORDER-123");
            Orders.Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => Order);
            var cartId = Guid.NewGuid();
            Loyalty = new(cartId, Order.PayerPartyId!.Value, false, "original-policy",
                new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), 200, 40, 2m, 40m,
                [new(0, items[0].Id, items[0].ItemType, items[0].ProductId, 10m, 0.3333m, 0.6667m, 2.5m, 6.5m, 6.5m, 13, 67, true, true),
                 new(1, items[1].Id, items[1].ItemType, items[1].ProductId, 20m, 0.6667m, 1.3333m, 4.5m, 13.5m, 13.5m, 27, 133, true, true),
                 new(2, items[2].Id, items[2].ItemType, null, 3m, 0m, 0m, 0.75m, 2.25m, 0m, 0, 0, false, false),
                 new(3, items[3].Id, items[3].ItemType, null, 5m, 0m, 0m, 1.25m, 3.75m, 0m, 0, 0, false, false)], 38m);
            Gift = new(cartId, "original-gift-policy", new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
                new(true), "original-terms", GiftCardSettings.Proportional,
                Tender: new("private-cart-grant", 10m, 28m, 3m, 1m,
                    Loyalty.Lines.Select(x => new GiftCardFundingLine(x.ItemIndex, x.OrderItemId, x.ItemType,
                        x.OriginalCharged, x.CouponDiscount, x.PointsAppliedValue, x.GiftFundedValue)).ToArray()));
            Summary = new()
            {
                TenantId = _h.TenantId, OrderId = Order.Id, Currency = "GBP", Subtotal = 33m,
                GreetingCardCharged = 3m, DiscountTotal = 1m, PointsAppliedValue = 2m, TaxTotal = 3m, Total = 38m,
                PaymentIntentId = Guid.NewGuid(), InvoiceId = Guid.NewGuid(), PaymentStatus = "Captured",
                LoyaltyJson = CheckoutLoyaltyData.Serialize(Loyalty), GiftCardJson = CheckoutGiftCards.Serialize(Gift),
                DiscountAllocationsJson = DiscountAllocationSnapshot.Serialize([new(items[0].Id, 0.3333m), new(items[1].Id, 0.6667m)])
            };
        }

        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            fixture.Db.OrderChargeSummaries.Add(fixture.Summary);
            await fixture.Db.SaveChangesAsync();
            return fixture;
        }

        public CheckoutRefundSourceReader Reader(Guid? tenantId = null)
            => new(Db, new TestTenantProvider(tenantId ?? _h.TenantId), Orders.Object);

        public ValueTask DisposeAsync() => Db.DisposeAsync();

        private static OrderItemDto Item(int index, string type, decimal amount, string name)
            => new(Guid.NewGuid(), type, index, "Paid", amount, "GBP", null, 1m, amount,
                type == "ProductPurchase" ? Guid.NewGuid() : null, $"SKU-{index}", "{}", name);
    }
}
