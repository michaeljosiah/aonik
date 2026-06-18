namespace Aonik.Finance.Contracts.Models.Payments;

public record CreateGuestPaymentIntentRequest(
    Guid OrderId,
    string Provider,
    string PaymentMethodType,
    string? ReturnUrl,
    string? CancelUrl);

public record GuestPaymentIntentResponse(
    Guid PaymentIntentId,
    Guid OrderId,
    decimal Amount,
    string Currency,
    string Status,
    string Provider,
    string ProviderReference,
    string? ClientSecret,
    string? CheckoutUrl,
    DateTime CreatedAt);


public record GetGuestPaymentIntentStatusRequest(
    Guid OrderId,
    Guid? PaymentIntentId,
    string? ProviderReference);

public record GuestPaymentIntentStatusResponse(
    Guid PaymentIntentId,
    Guid OrderId,
    decimal Amount,
    string Currency,
    string Status,
    string ProviderReference,
    DateTime CreatedAt,
    string OrderStatus);

/// <summary>
/// Spec 042 — create a guest payment intent for a commerce <c>ProductPurchase</c> order, funding the
/// explicit payable <see cref="Amount"/> (the checkout total after discount/tax). Permission-free
/// public path so anonymous storefront checkout works.
/// </summary>
public record CreateCommerceGuestPaymentIntentRequest(
    Guid OrderId,
    decimal Amount,
    string Currency,
    string Provider,
    string PaymentMethodType,
    string? ReturnUrl,
    string? CancelUrl);
