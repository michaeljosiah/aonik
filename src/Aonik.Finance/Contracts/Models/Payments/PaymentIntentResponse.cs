using Aonik.Finance.Entities.Payments;

namespace Aonik.Finance.Contracts.Models.Payments;

public record PaymentIntentResponse(
    Guid Id,
    Guid OrderId,
    Guid? InvoiceId,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    string Reference,
    DateTime CreatedUtc);
