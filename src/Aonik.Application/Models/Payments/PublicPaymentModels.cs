namespace Aonik.Application.Models.Payments;

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
