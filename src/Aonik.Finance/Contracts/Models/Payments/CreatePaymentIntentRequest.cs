namespace Aonik.Finance.Contracts.Models.Payments;

public record CreatePaymentIntentRequest(
    decimal Amount,
    string Currency,
    string Reference,
    Guid OrderId,
    Guid? InvoiceId,
    /// <summary>
    /// Party funding the payment. Optional today because not every caller
    /// (agent tools, internal flows) resolves a payer up front; when supplied
    /// it is persisted instead of the empty placeholder.
    /// </summary>
    Guid? PayerPartyId = null,
    /// <summary>
    /// Rail used to fund the payment (e.g. "Card", "BankTransfer"). Defaults to
    /// "Card" when not supplied.
    /// </summary>
    string? PaymentMethodType = null);
