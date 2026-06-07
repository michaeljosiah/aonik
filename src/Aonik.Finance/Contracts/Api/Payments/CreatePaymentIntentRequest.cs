namespace Aonik.Finance.Contracts.Api.Payments;

public record CreatePaymentIntentRequest(
    decimal Amount,
    string Currency,
    string Reference,
    Guid OrderId,
    Guid? InvoiceId,
    // Funding rail (e.g. "Card", "BankTransfer"). Optional at creation (a draft may not
    // know it yet); required before the intent can be authorized. No silent "Card" default.
    string? PaymentMethodType = null);

public record CreatePublicPaymentIntentRequest(
    Guid OrderId,
    string Provider,
    string PaymentMethodType,
    string? ReturnUrl,
    string? CancelUrl);
