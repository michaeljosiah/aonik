namespace Aonik.SharedKernel.Abstractions.Loyalty;

public static class LoyaltySettings
{
    public const string Policy = "Commerce.Storefront.Loyalty";
}

/// <summary>Explicit existing GBP chart accounts. No account is auto-created by loyalty.</summary>
public sealed record LoyaltyLedgerBinding(Guid LedgerId, Guid LiabilityAccountId, Guid EarnExpenseAccountId, Guid RedeemExpenseAccountId);

public sealed record LoyaltyPolicy(
    bool Enabled = false,
    string Version = "",
    LoyaltyLedgerBinding? Ledger = null,
    IReadOnlyList<Guid>? EarnExcludedProductIds = null,
    IReadOnlyList<Guid>? RedeemExcludedProductIds = null,
    IReadOnlyList<Guid>? EarnExcludedDiscountIds = null,
    IReadOnlyList<Guid>? RedeemExcludedDiscountIds = null);

/// <summary>Frozen server calculation. Item ids are mapped from indices when the order is materialized.</summary>
public sealed record LoyaltyCheckout(
    Guid CartId, Guid PartyId, bool IsGuest, string PolicyVersion, LoyaltyLedgerBinding Ledger,
    long RedeemedPoints, long EarnedPoints, decimal PointsAppliedValue,
    decimal OrderValueBeforePoints, IReadOnlyList<LoyaltyCheckoutLine> Lines, decimal? PayableTotal = null);

public sealed record LoyaltyCheckoutLine(
    int ItemIndex, Guid OrderItemId, string ItemType, Guid? ProductId,
    decimal OriginalCharged, decimal CouponDiscount, decimal PointsAppliedValue,
    decimal GiftFundedValue, decimal NetPaidValue, decimal EligibleEarnValue,
    long EarnedPoints, long RedeemedPoints, bool EarnEligible, bool RedeemEligible);

public sealed record LoyaltyBalance(long BalancePoints, long ReservedPoints, long AvailablePoints, decimal Value, long HighestFivePoundMarkSeen);
public sealed record LoyaltyActivity(Guid Id, string Kind, DateTime OccurredAtUtc, long Points, long RunningBalancePoints,
    Guid? OrderId, Guid SourceId, Guid? OriginalOperationId, string? Reason);
public sealed record LoyaltyActivityPage(IReadOnlyList<LoyaltyActivity> Items, int TotalCount, int Page, int PageSize);
public sealed record LoyaltyOperationRef(Guid Id, long Points, Guid? JournalEntryId);
public sealed record LoyaltyAdjustment(Guid PartyId, Guid AdjustmentId, long Points, string Reason);

/// <summary>Internal refund result, never permission to execute a provider refund. Quantities refer to original saved line allocations.</summary>
public sealed record LoyaltyRefund(Guid RefundId, Guid OrderId, Guid PaymentIntentId, IReadOnlyList<LoyaltyRefundLine> Lines);
public sealed record LoyaltyRefundLine(Guid OrderItemId, long EarnedPointsToReverse, long RedeemedPointsToRestore);
