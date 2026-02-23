namespace Aonik.Finance.Contracts.Models.Payments;

public record CreatePaymentIntentRequest(
    decimal Amount,
    string Currency,
    string Reference,
    Guid OrderId,
    Guid? InvoiceId);
