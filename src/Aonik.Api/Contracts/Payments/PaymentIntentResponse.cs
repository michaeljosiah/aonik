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
