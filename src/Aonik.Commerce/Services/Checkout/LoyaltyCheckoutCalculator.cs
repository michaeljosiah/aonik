using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Promotions;
using Aonik.SharedKernel.Abstractions.Loyalty;

namespace Aonik.Commerce.Services.Checkout;

internal static class LoyaltyCheckoutCalculator
{
    public static CheckoutLoyaltyQuote Calculate(Guid cartId, Guid? partyId, LoyaltyPolicy policy,
        IReadOnlyList<LoyaltyChargedLine> chargedLines, decimal orderValueBeforePoints, long requestedPoints,
        LoyaltyBalance? balance)
    {
        if (requestedPoints < 0 || orderValueBeforePoints < 0 || policy.Ledger is null)
            throw new StorefrontValidationException("The loyalty calculation inputs are invalid.");
        var lines = chargedLines.OrderBy(line => line.ItemIndex).ToList();
        if (lines.Select(line => line.ItemIndex).Distinct().Count() != lines.Count
            || lines.Any(line => line.OriginalCharged < 0 || line.CouponDiscount < 0 || line.GiftFundedValue < 0
                || line.CouponDiscount + line.GiftFundedValue > line.OriginalCharged))
            throw new StorefrontValidationException("The loyalty line amounts are invalid.");
        var redemptionWeights = lines.Select(line => line.RedeemEligible ? line.OriginalCharged - line.CouponDiscount : 0m).ToArray();
        var maximum = Math.Min(Math.Max(0, balance?.AvailablePoints ?? 0), Math.Min(
            ToPoints(orderValueBeforePoints * 0.20m), ToPoints(redemptionWeights.Sum())));
        if (requestedPoints > maximum)
            return new(null, new(requestedPoints, maximum, 0, 0m, 0, balance?.BalancePoints, balance?.AvailablePoints,
                LoyaltyQuoteReasons.AboveLimit, "The requested points exceed the available balance, eligible value or 20% order limit. Review the quote."));
        var benefit = requestedPoints / 100m;
        var monetaryAllocations = DiscountAllocationMath.Allocate(benefit, redemptionWeights);
        if (lines.Where((line, index) => line.GiftFundedValue > line.OriginalCharged - line.CouponDiscount - monetaryAllocations[index]).Any())
            throw new StorefrontValidationException("Gift-funded amounts cannot exceed the amount remaining after points.");
        var eligiblePaid = lines.Select((line, index) => line.EarnEligible
            ? line.OriginalCharged - line.CouponDiscount - monetaryAllocations[index] - line.GiftFundedValue : 0m).ToArray();
        var earned = checked((long)decimal.Floor(eligiblePaid.Sum() * 2m));
        var earnedAllocations = AllocatePoints(earned, eligiblePaid);
        var redeemedAllocations = AllocatePoints(requestedPoints, monetaryAllocations);
        var frozen = new LoyaltyCheckout(cartId, partyId ?? Guid.Empty, partyId is null, policy.Version, policy.Ledger,
            requestedPoints, earned, benefit, orderValueBeforePoints, lines.Select((line, index) => new LoyaltyCheckoutLine(
                line.ItemIndex, Guid.Empty, line.ItemType, line.ProductId, line.OriginalCharged, line.CouponDiscount,
                monetaryAllocations[index], line.GiftFundedValue,
                line.OriginalCharged - line.CouponDiscount - monetaryAllocations[index] - line.GiftFundedValue,
                eligiblePaid[index], earnedAllocations[index], redeemedAllocations[index], line.EarnEligible, line.RedeemEligible)).ToList());
        return new(frozen, new(requestedPoints, maximum, requestedPoints, benefit, earned, balance?.BalancePoints, balance?.AvailablePoints));
    }

    private static long ToPoints(decimal amount) => checked((long)decimal.Floor(amount * 100m));

    // Allocate the already-rounded order total, with ItemIndex order breaking equal remainders.
    private static long[] AllocatePoints(long total, IReadOnlyList<decimal> weights)
    {
        var result = new long[weights.Count];
        if (total == 0) return result;
        var sum = weights.Sum();
        if (sum <= 0) throw new StorefrontValidationException("Points require an eligible paid amount.");
        var quotas = weights.Select(weight => total * weight / sum).ToArray();
        for (var index = 0; index < quotas.Length; index++) result[index] = checked((long)decimal.Floor(quotas[index]));
        var remaining = total - result.Sum();
        foreach (var index in Enumerable.Range(0, quotas.Length).OrderByDescending(index => quotas[index] - result[index]).ThenBy(index => index))
        {
            if (remaining == 0) break;
            result[index]++; remaining--;
        }
        if (remaining != 0) throw new InvalidOperationException("Point allocations did not reconcile.");
        return result;
    }
}

internal sealed record CheckoutLoyaltyQuote(LoyaltyCheckout? Checkout, LoyaltyQuoteDto? Quote)
{
    public decimal PointsAppliedValue => Checkout?.PointsAppliedValue ?? 0m;
}
