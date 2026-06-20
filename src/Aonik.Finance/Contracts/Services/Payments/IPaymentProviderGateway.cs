namespace Aonik.Finance.Contracts.Services.Payments;

public interface IPaymentProviderGateway
{
    string ProviderCode { get; }

    Task<PaymentProviderIntentResult> CreateIntentAsync(
        PaymentProviderIntentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a setup intent for vaulting a reusable payment instrument (Spec 007). Returns the
    /// client secret the provider SDK uses to collect and tokenise a card client-side, so no card
    /// data passes through Aonik.
    /// </summary>
    Task<PaymentProviderSetupIntentResult> CreateSetupIntentAsync(
        PaymentProviderSetupIntentRequest request,
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

public record PaymentProviderSetupIntentRequest(
    Guid CustomerPartyId,
    string? ProviderCustomerRef);

public record PaymentProviderSetupIntentResult(
    string Provider,
    string SetupIntentReference,
    string ClientSecret,
    IReadOnlyList<string> PaymentMethodTypes,
    string? ProviderCustomerRef);
