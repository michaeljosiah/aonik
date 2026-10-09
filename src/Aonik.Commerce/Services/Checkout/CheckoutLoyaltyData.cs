using System.Text.Json;

using Aonik.SharedKernel.Abstractions.Loyalty;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Promotions;

namespace Aonik.Commerce.Services.Checkout;

internal static class CheckoutLoyaltyData
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        { RespectRequiredConstructorParameters = true };

    public static LoyaltyCheckout? Read(string? json) => json is null ? null
        : JsonSerializer.Deserialize<LoyaltyCheckout>(json, Json)
            ?? throw new InvalidOperationException("The saved loyalty calculation is invalid.");

    public static string? Serialize(LoyaltyCheckout? loyalty)
    {
        if (loyalty is null) return null;
        var json = JsonSerializer.Serialize(loyalty, Json);
        if (json.Length > 262144) throw new InvalidOperationException("The loyalty calculation is too large.");
        return json;
    }

    public static OrderLoyaltyDto? ForOrder(OrderChargeSummary summary, Entities.Cart.Cart cart)
    {
        var loyalty = Read(summary.LoyaltyJson);
        if (loyalty is null) return null;
        var paid = summary.PaymentStatus == CheckoutPaymentStatuses.Captured;
        var optedIn = cart.CheckoutPreparationJson is not null && CheckoutPreparation.Read(cart).CreateAccount;
        var visible = cart.BuyerPartyId is not null || optedIn;
        return new(loyalty.RedeemedPoints, loyalty.PointsAppliedValue,
            paid && visible ? loyalty.EarnedPoints : null,
            !paid ? "NotPaid" : cart.BuyerPartyId is not null ? "Earned"
                : optedIn ? "AccountSetupRequired" : "AccountNotLinked");
    }
}
