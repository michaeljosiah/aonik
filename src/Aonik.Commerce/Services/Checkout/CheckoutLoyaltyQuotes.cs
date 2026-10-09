using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Promotions;
using Aonik.SharedKernel.Abstractions.Loyalty;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;

namespace Aonik.Commerce.Services.Checkout;

internal sealed class CheckoutLoyaltyQuotes(CommerceDbContext db, ITenantProvider tenantProvider, ILoyaltyService loyalty)
{
    public async Task<CheckoutLoyaltyQuote> CalculateAsync(Cart cart, IReadOnlyList<OrderItemCommand> items,
        DiscountComputation discount, decimal orderValueBeforePoints, CancellationToken cancellationToken = default)
    {
        var requested = CartDraftData.Read(cart)?.RequestedPoints ?? 0;
        var policy = await loyalty.GetPolicyAsync(cancellationToken);
        if (!policy.Enabled) return requested == 0 ? new(null, null)
            : Rejected(requested, LoyaltyQuoteReasons.Disabled, "Loyalty redemption is not currently available.");
        if (!string.Equals(cart.Currency, "GBP", StringComparison.OrdinalIgnoreCase))
            return Rejected(requested, LoyaltyQuoteReasons.CurrencyUnsupported, "Loyalty is available only for GBP checkout.");
        if (requested > 0 && cart.BuyerPartyId is null)
            return Rejected(requested, LoyaltyQuoteReasons.SignInRequired, "Sign in before redeeming points.");
        var balance = cart.BuyerPartyId is { } partyId ? await loyalty.GetBalanceAsync(partyId, cancellationToken) : null;
        var lines = await CheckoutLoyaltyLines.ResolveAsync(db, tenantProvider.GetCurrentTenantId(), items, discount, policy, cancellationToken);
        return LoyaltyCheckoutCalculator.Calculate(cart.Id, cart.BuyerPartyId, policy, lines, orderValueBeforePoints, requested, balance);
    }

    public static LoyaltyQuoteDto? Frozen(LoyaltyCheckout? value)
        => value is null ? null : new(value.RedeemedPoints, value.RedeemedPoints, value.RedeemedPoints,
            value.PointsAppliedValue, value.EarnedPoints);

    private static CheckoutLoyaltyQuote Rejected(long requested, string code, string message)
        => new(null, new(requested, 0, 0, 0m, 0, ReasonCode: code, Message: message));
}
