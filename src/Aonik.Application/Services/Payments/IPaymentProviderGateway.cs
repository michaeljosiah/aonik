namespace Aonik.Application.Services.Payments;

public interface IPaymentProviderGateway
{
    string ProviderCode { get; }

    Task<PaymentProviderIntentResult> CreateIntentAsync(
        PaymentProviderIntentRequest request,
        CancellationToken cancellationToken = default);
}

public record PaymentProviderIntentRequest(
    Guid OrderId,
    decimal Amount,
    string Currency,
    string PaymentMethodType,
    string? ReturnUrl,
    string? CancelUrl,
    string Reference);

public record PaymentProviderIntentResult(
    string Provider,
    string ProviderReference,
    string Status,
    string? ClientSecret,
    string? CheckoutUrl);
