namespace Aonik.Commerce.Contracts.Models.Checkout;

/// <summary>A read-only estimate. Earning is recorded only after verified payment.</summary>
public record LoyaltyQuoteDto(long RequestedPoints, long MaxRedeemablePoints, long AppliedPoints,
    decimal AppliedValue, long EstimatedEarnedPoints, long? BalancePoints = null, long? AvailablePoints = null,
    string? ReasonCode = null, string? Message = null);

public static class LoyaltyQuoteReasons
{
    public const string Disabled = "commerce.loyalty_disabled";
    public const string SignInRequired = "commerce.loyalty_sign_in_required";
    public const string CurrencyUnsupported = "commerce.loyalty_currency_unsupported";
    public const string AboveLimit = "commerce.loyalty_above_limit";
}
