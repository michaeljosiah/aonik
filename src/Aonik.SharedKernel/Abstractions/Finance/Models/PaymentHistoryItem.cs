namespace Aonik.SharedKernel.Abstractions.Finance;

/// <summary>
/// Cross-module projection of a payment intent. Carries only what PersonalFinance
/// and other non-Finance consumers actually read from
/// <c>Aonik.Finance.Entities.Payments.PaymentIntent</c>.
/// </summary>
public sealed record PaymentHistoryItem(
    Guid PaymentIntentId,
    Guid OrderId,
    Guid? InvoiceId,
    string Status,
    decimal Amount,
    string Currency,
    string PurposeType,
    Guid PurposeId);
