namespace Aonik.SharedKernel.Abstractions.Finance;

/// <summary>
/// Cross-module projection of an invoice. Carries only what PersonalFinance and other
/// non-Finance consumers actually read from <c>Aonik.Finance.Entities.Billing.Invoice</c>.
/// </summary>
public sealed record InvoiceHistoryItem(
    Guid InvoiceId,
    Guid? OrderId,
    string Status,
    string Currency,
    decimal Total,
    DateTime IssueDate,
    DateTime DueDate);
