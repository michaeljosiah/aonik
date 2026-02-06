namespace Aonik.Api.Contracts.Payments;

public record CreatePaymentIntentRequest(
    decimal Amount,
    string Currency,
    string Reference,
    Guid OrderId,
    Guid? InvoiceId);

public record CreatePublicPaymentIntentRequest(
    Guid OrderId,
    string Provider,
    string PaymentMethodType,
    string? ReturnUrl,
    string? CancelUrl);
