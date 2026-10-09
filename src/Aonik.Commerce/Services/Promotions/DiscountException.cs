namespace Aonik.Commerce.Services.Promotions;

public sealed class DiscountException(string code) : InvalidOperationException(MessageFor(code))
{
    public const string Invalid = "commerce.discount_invalid";
    public const string Expired = "commerce.discount_expired";
    public const string AlreadyUsed = "commerce.discount_already_used";
    public const string NotEligible = "commerce.discount_not_eligible";
    public const string CurrencyMismatch = "commerce.discount_currency_mismatch";
    public const string Inactive = "commerce.discount_inactive";
    public const string PriceChanged = "commerce.discount_price_changed";
    public const string Conflict = "commerce.discount_conflict";
    public string Code { get; } = code;

    private static string MessageFor(string code) => code switch
    {
        Expired => "This discount code has expired.",
        AlreadyUsed => "This discount code has reached its usage limit.",
        NotEligible => "This discount code does not apply to these items.",
        CurrencyMismatch => "This discount code does not apply to this currency.",
        Inactive => "This discount code is not active.",
        PriceChanged => "Review and accept the current total before checkout.",
        Conflict => "The discount has changed. Reload it before trying again.",
        _ => "This discount code is invalid."
    };
}
