namespace Aonik.Api.Contracts.Payments;

public record CreatePaymentIntentRequest(
    decimal Amount,
    string Currency,
    string Reference);
