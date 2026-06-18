using Aonik.Commerce.Contracts.Models.Catalog;

namespace Aonik.Commerce.Contracts.Api.Checkout;

public record CreateCartRequest(string Currency, Guid? BuyerPartyId, string? AnonymousToken);

public record AddCartItemRequest(Guid ProductVariantId, decimal Quantity);

public record AddBundleToCartRequest(Guid BundleProductId, IReadOnlyCollection<BundleSelectionLine> Selection);

public record CheckoutRequest(
    string Provider,
    string PaymentMethodType,
    string? ReturnUrl,
    string? CancelUrl,
    Guid? CustomerAccountId,
    string? DiscountCode);
