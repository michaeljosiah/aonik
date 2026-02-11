namespace Aonik.Api.Contracts.Payments;

public record PaymentIntentResponse(
    Guid Id,
    Guid OrderId,
    Guid? InvoiceId,
    decimal Amount,
    string Currency,
    string Status,
    string Reference,
    DateTime CreatedUtc);

public record PublicPaymentIntentResponse(
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


public record PublicPaymentIntentStatusResponse(
    Guid PaymentIntentId,
    Guid OrderId,
    decimal Amount,
    string Currency,
    string Status,
    string ProviderReference,
    DateTime CreatedAt,
    string OrderStatus);
