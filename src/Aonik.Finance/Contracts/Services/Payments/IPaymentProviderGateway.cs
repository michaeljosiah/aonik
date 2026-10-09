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

    Task<PaymentProviderCheckoutSnapshot> GetCheckoutAsync(
        PaymentProviderCheckoutReference reference, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This provider does not support checkout reconciliation.");

    Task<PaymentProviderCheckoutSnapshot> ExpireCheckoutAsync(
        PaymentProviderCheckoutReference reference, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This provider does not support checkout expiration.");

    Task<PaymentProviderRefundSnapshot> CreateRefundAsync(
        PaymentProviderRefundRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This provider does not support refunds.");

    Task<PaymentProviderRefundSnapshot> GetRefundAsync(
        PaymentProviderRefundRequest request, string providerRefundId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This provider does not support refund reconciliation.");

    Task<PaymentProviderRefundSnapshot?> FindRefundAsync(
        PaymentProviderRefundRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This provider does not support refund reconciliation.");

    Task<PaymentProviderRefundBudget> GetRefundBudgetAsync(
        PaymentProviderRefundRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This provider does not support refund reconciliation.");
}

public record PaymentProviderIntentRequest(
    Guid OrderId,
    decimal Amount,
    string Currency,
    string PaymentMethodType,
    string? ReturnUrl,
    string? CancelUrl,
    string Reference,
    Guid PaymentIntentId = default,
    Guid? ConnectorId = null,
    string? ProviderAccountId = null,
    bool? LiveMode = null,
    string? IdempotencyKey = null);

public record PaymentProviderIntentResult(
    string Provider,
    string ProviderReference,
    string Status,
    string? ClientSecret,
    string? CheckoutUrl,
    PaymentProviderCheckoutSnapshot? Checkout = null);

public record PaymentProviderSetupIntentRequest(
    Guid CustomerPartyId,
    string? ProviderCustomerRef);

public record PaymentProviderSetupIntentResult(
    string Provider,
    string SetupIntentReference,
    string ClientSecret,
    IReadOnlyList<string> PaymentMethodTypes,
    string? ProviderCustomerRef);
