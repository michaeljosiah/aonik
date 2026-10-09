using Aonik.Commerce.Services.Checkout;
using Aonik.SharedKernel.Abstractions.Loyalty;
using Aonik.SharedKernel.Abstractions.Ordering;

using FluentAssertions;

namespace Aonik.Application.Tests.Commerce;

public sealed class LoyaltyCheckoutCalculatorTests
{
    private static readonly LoyaltyPolicy Policy = new(true, "v1", new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

    [Fact]
    public void Earnings_Should_FloorOncePerOrder_AndAllocateRemaindersByItemIndex()
    {
        var result = Calculate([Goods(7, 0.3m), Goods(2, 0.3m), Goods(9, 0.3m)]);

        result.Checkout!.EarnedPoints.Should().Be(1, "three eligible 60%-point amounts round once after summing");
        result.Checkout.Lines.Select(x => (x.ItemIndex, x.EarnedPoints)).Should().Equal((2, 1L), (7, 0L), (9, 0L));
    }

    [Fact]
    public void Redemption_Should_UseWholeOrderCap_ButAllocateOnlyEligibleGoods()
    {
        var result = Calculate([Goods(0, 20m), new(1, CheckoutService.DeliveryFeeItemType, null, 10m, 0m, false, false)], 600);

        result.Quote!.MaxRedeemablePoints.Should().Be(600, "the whole £30 order supplies the 20% cap");
        result.PointsAppliedValue.Should().Be(6m);
        result.Checkout!.Lines[0].PointsAppliedValue.Should().Be(6m);
        result.Checkout.Lines[1].Should().Match<LoyaltyCheckoutLine>(x => x.EarnedPoints == 0 && x.RedeemedPoints == 0);
        result.Checkout.EarnedPoints.Should().Be(28);
    }

    [Theory]
    [InlineData(50, 10000, 1000)]
    [InlineData(5, 10000, 500)]
    [InlineData(50, 200, 200)]
    public void Maximum_Should_BeBoundedByWholeOrderEligibleValueAndAvailableBalance(int goods, long available, long expected)
    {
        var result = Calculate([Goods(0, goods)], balance: new(available, 0, available, available / 100m, 0), wholeOrder: 50m);
        result.Quote!.MaxRedeemablePoints.Should().Be(expected);
    }

    [Fact]
    public void AboveMaximum_Should_RequireAnExplicitNewChoice_WithoutClamping()
    {
        var result = Calculate([Goods(0, 10m)], 201);
        result.Checkout.Should().BeNull();
        result.Quote!.RequestedPoints.Should().Be(201);
        result.Quote.MaxRedeemablePoints.Should().Be(200);
        result.Quote.AppliedPoints.Should().Be(0);
        result.Quote.ReasonCode.Should().Be("commerce.loyalty_above_limit");
    }

    [Fact]
    public void Points_Should_FollowVoucherAndExcludeGiftFundedSpendFromEarnings()
    {
        var result = Calculate([Goods(0, 100m) with { CouponDiscount = 10m, GiftFundedValue = 20m }], 1000);

        var line = result.Checkout!.Lines.Should().ContainSingle().Subject;
        line.CouponDiscount.Should().Be(10m);
        line.PointsAppliedValue.Should().Be(10m);
        line.GiftFundedValue.Should().Be(20m);
        line.NetPaidValue.Should().Be(60m);
        line.EligibleEarnValue.Should().Be(60m);
        line.EarnedPoints.Should().Be(120);
    }

    [Fact]
    public void GiftValue_Should_EarnButNotRedeem_AndGreetingCardDoesNeither()
    {
        var result = Calculate([
            new(0, CheckoutLoyaltyLines.GiftCardValueItemType, Guid.NewGuid(), 25m, 0m, true, false),
            new(1, CheckoutService.GreetingCardItemType, null, 3m, 0m, false, false)]);

        result.Quote!.MaxRedeemablePoints.Should().Be(0);
        result.Checkout!.EarnedPoints.Should().Be(50);
        result.Checkout.Lines[1].EligibleEarnValue.Should().Be(0m);
    }

    [Fact]
    public void RefundFacts_Should_ConserveWholePointsAndFourDecimalMonetaryAllocations()
    {
        var result = Calculate([Goods(0, 1m), Goods(1, 1m), Goods(2, 1m)], 1);
        var lines = result.Checkout!.Lines;
        lines.Sum(x => x.PointsAppliedValue).Should().Be(0.01m);
        lines.Sum(x => x.RedeemedPoints).Should().Be(1);
        lines.Sum(x => x.EarnedPoints).Should().Be(result.Checkout.EarnedPoints);
        lines.Select(x => x.RedeemedPoints).Should().Equal(1L, 0L, 0L);
        lines.Should().OnlyContain(x => x.NetPaidValue >= 0m && x.PointsAppliedValue <= x.OriginalCharged);
    }

    [Fact]
    public void NegativeBalance_Should_NotAllowRedemptionOrPreventNewEarning()
    {
        var result = Calculate([Goods(0, 10m)], balance: new(-100, 0, 0, -1m, 0));
        result.Quote!.MaxRedeemablePoints.Should().Be(0);
        result.Checkout!.EarnedPoints.Should().Be(20);
    }

    private static LoyaltyChargedLine Goods(int index, decimal amount)
        => new(index, OrderTypeCodes.ProductPurchase, Guid.NewGuid(), amount, 0m, true, true);

    private static CheckoutLoyaltyQuote Calculate(IReadOnlyList<LoyaltyChargedLine> lines, long requested = 0,
        LoyaltyBalance? balance = null, decimal? wholeOrder = null)
        => LoyaltyCheckoutCalculator.Calculate(Guid.NewGuid(), Guid.NewGuid(), Policy, lines,
            wholeOrder ?? lines.Sum(x => x.OriginalCharged - x.CouponDiscount), requested,
            balance ?? new(10000, 0, 10000, 100m, 0));
}
