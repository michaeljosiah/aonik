using Aonik.Commerce.Contracts.Models.Catalog;

namespace Aonik.Commerce.Contracts.Models.Checkout;

public record CartItemSelectionDto(
    Guid Id,
    Guid BundleSlotId,
    Guid ProductVariantId,
    decimal Quantity,
    decimal UnitPriceSnapshot,
    string Sku,
    string NameSnapshot);

public record CartItemDto(
    Guid Id,
    Guid ProductVariantId,
    bool IsBundle,
    Guid? BundleProductId,
    decimal Quantity,
    decimal UnitPriceSnapshot,
    string Sku,
    string NameSnapshot,
    decimal LineTotal,
    IReadOnlyList<CartItemSelectionDto> Selections);

public record CartDto(
    Guid Id,
    Guid? BuyerPartyId,
    /// Populated ONLY by create (Spec 068 R10) — every other read carries null.
    string? AnonymousToken,
    string Status,
    string Currency,
    Guid? OrderId,
    decimal Total,
    IReadOnlyList<CartItemDto> Items,
    /// Spec 068 — set when this cart is a box session; GET then serves the §7 box payload.
    Guid? BoxBundleProductId = null);

public record CreateCartCommand(string Currency, Guid? BuyerPartyId = null, string? AnonymousToken = null);

public record AddCartItemCommand(Guid CartId, Guid ProductVariantId, decimal Quantity = 1m);

public record AddBundleToCartCommand(Guid CartId, Guid BundleProductId, IReadOnlyCollection<BundleSelectionLine> Selection);

public record CheckoutCommand(
    Guid CartId,
    string Provider,
    string PaymentMethodType,
    string? ReturnUrl = null,
    string? CancelUrl = null,
    Guid? CustomerAccountId = null,
    string? DiscountCode = null);

public record CheckoutResult(
    Guid OrderId,
    Guid? InvoiceId,
    Guid PaymentIntentId,
    string PaymentStatus,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal Total,
    string Currency,
    string? ClientSecret = null,
    string? CheckoutUrl = null);
