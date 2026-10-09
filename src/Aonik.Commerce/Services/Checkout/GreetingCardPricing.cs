using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Catalog;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Abstractions.Settings;

namespace Aonik.Commerce.Services.Checkout;

/// <summary>The one tenant-authored greeting-card amount used by quotes and checkout.</summary>
internal static class GreetingCardPricing
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        { RespectRequiredConstructorParameters = true };

    public static async Task<GreetingCardPriceDto?> ReadAsync(ITenantSettingStore settings, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var json = await settings.GetTenantValueAsync(CommerceSettingNames.StorefrontGreetingCard, tenantId, cancellationToken);
        if (string.IsNullOrWhiteSpace(json) || json.Length > 1000) return null;
        Configuration? configuration;
        try { configuration = JsonSerializer.Deserialize<Configuration>(json, Json); }
        catch (JsonException) { return null; }
        if (configuration is not { IsEnabled: true } || configuration.Amount is <= 0 or > 999999.99m
            || decimal.Round(configuration.Amount, 2) != configuration.Amount
            || configuration.Currency is not { Length: 3 } || !configuration.Currency.All(char.IsAsciiLetter))
            return null;
        return new(configuration.Amount, configuration.Currency.ToUpperInvariant());
    }

    public static async Task<decimal> ResolveAsync(ITenantSettingStore settings, Guid tenantId, string currency,
        CartGiftDraftDto? gift, CancellationToken cancellationToken = default)
    {
        if (gift is not { GiftIntent: true, IncludeGreetingCard: true }) return 0m;
        var price = await ReadAsync(settings, tenantId, cancellationToken);
        if (price is null || !string.Equals(price.Currency, currency, StringComparison.OrdinalIgnoreCase))
            throw new StorefrontValidationException("Gift.IncludeGreetingCard: a greeting card is not currently available in this currency. Remove the card or try again later.");
        return price.Amount;
    }

    public static OrderItemCommand Item(int index, decimal amount, string currency) => new(
        CheckoutService.GreetingCardItemType, index, amount, currency, Quantity: 1m, UnitPrice: amount,
        ProductId: null, Sku: "greeting-card", NameSnapshot: "Greeting card");

    private sealed record Configuration(bool IsEnabled, string Currency, decimal Amount);
}
