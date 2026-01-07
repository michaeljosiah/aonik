namespace Aonik.Application.Models.Payments;

public record CreatePaymentIntentRequest(
    decimal Amount,
    string Currency,
    string Reference);
