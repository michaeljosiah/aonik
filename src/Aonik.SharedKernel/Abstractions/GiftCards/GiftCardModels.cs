namespace Aonik.SharedKernel.Abstractions.GiftCards;

public static class GiftCardSettings
{
    public const string Policy = "Finance.GiftCards.Policy";
    public const string Proportional = "Proportional";
}

/// <summary>Explicit existing GBP accounts; issued instruments retain their original binding.</summary>
public sealed record GiftCardLedgerBinding(Guid LedgerId, Guid CashAccountId, Guid ClearingAccountId, Guid LiabilityAccountId);
public sealed record GiftCardValidity(bool NeverExpires, int? ValidForDays = null);
public sealed record GiftCardPolicy(bool Enabled = false, string Version = "", string Currency = "GBP",
    GiftCardLedgerBinding? Ledger = null, GiftCardValidity? Validity = null, string? TermsVersion = null, string? FundingAllocation = null);

/// <summary>Frozen server instruction. Exactly one purchase or tender; never expose its private grant.</summary>
public sealed record GiftCardCheckout(Guid CartId, string PolicyVersion, GiftCardLedgerBinding Ledger,
    GiftCardValidity Validity, string TermsVersion, string FundingAllocation,
    GiftCardPurchase? Purchase = null, GiftCardTender? Tender = null, IReadOnlyList<GiftCardPurchase>? AdditionalPurchases = null)
{
    public IReadOnlyList<GiftCardPurchase> PurchasedCards() => Purchase is null ? Array.Empty<GiftCardPurchase>()
        : new[] { Purchase }.Concat(AdditionalPurchases ?? Array.Empty<GiftCardPurchase>()).ToArray();
};
public sealed record GiftCardPurchase(int ItemIndex, Guid OrderItemId, decimal FaceValue);
public sealed record GiftCardTender(string PrivateCartGrant, decimal Amount, decimal ExpectedCardAmount,
    decimal TaxTotal, decimal GiftFundedTaxAmount, IReadOnlyList<GiftCardFundingLine> Lines);
public sealed record GiftCardFundingLine(int ItemIndex, Guid OrderItemId, string ItemType,
    decimal OriginalCharged, decimal CouponDiscount, decimal PointsAppliedValue, decimal GiftFundedValue = 0m);
public sealed record GiftCardQuoteRequest(Guid CartId, string PrivateCartGrant, string Currency,
    decimal Total, decimal TaxTotal, decimal RequestedAmount, IReadOnlyList<GiftCardFundingLine> Lines);

/// <summary>Internal quote: Checkout contains a capability and must be mapped to a safe public response.</summary>
public sealed record GiftCardFundingQuote(decimal RequestedAmount, decimal MaxRedeemableAmount,
    decimal GiftAmount, decimal CardAmount, GiftCardCheckout? Checkout, string? ReasonCode = null);
public sealed record GiftCardBalance(string MaskedCode, string Currency, decimal Balance,
    decimal Reserved, decimal Available, string Status, DateTime? ExpiresAtUtc);
public sealed record GiftCardAuthorization(string? PrivateCartGrant, GiftCardBalance? Card, string? ReasonCode = null);
public sealed record GiftCardPurchaseSource(Guid CartId, Guid OrderId, Guid PaymentIntentId, Guid OrderItemId, int ItemIndex);
public sealed record GiftCardIssuedInfo(Guid GiftCardId, GiftCardPurchaseSource Source, decimal FaceValue,
    string Currency, string MaskedCode, DateTime IssuedAtUtc, DateTime? ExpiresAtUtc, string TermsVersion, string Status);

/// <summary>Trusted exact-source delivery/printing only. Resend returns the same issued code.</summary>
public sealed record GiftCardFulfilmentSecret(GiftCardIssuedInfo Card, string Code);
public sealed record GiftCardFunding(decimal Total, decimal GiftAmount, decimal CardAmount,
    string Currency, GiftCardLedgerBinding? Ledger = null);
