using Aonik.Domain.Payments;

namespace Aonik.Application.Models.Payments;

public record PaymentIntentResponse(
    Guid Id,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    string Reference,
    DateTime CreatedUtc);
