namespace Aonik.Api.Contracts.Payments;

public record PaymentIntentResponse(
    Guid Id,
    decimal Amount,
    string Currency,
    string Status,
    string Reference,
    DateTime CreatedUtc);
