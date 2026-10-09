namespace Aonik.Finance.Contracts.Services.Payments;

public sealed record PaymentProviderCheckoutReference(
    Guid ConnectorId,
    string ProviderAccountId,
    bool LiveMode,
    string SessionId,
    string? ProviderPaymentIntentId = null);

/// <summary>Current account-bound provider observation, produced by the server's SDK adapter.</summary>
public sealed record PaymentProviderCheckoutSnapshot(
    Guid TenantId,
    Guid PaymentIntentId,
    Guid OrderId,
    Guid ConnectorId,
    string ProviderAccountId,
    bool LiveMode,
    string SessionId,
    string? ProviderPaymentIntentId,
    decimal Amount,
    string Currency,
    string Status,
    bool CanNoLongerPay,
    string? CheckoutUrl,
    decimal? ReceivedAmount = null,
    DateTime? CapturedAtUtc = null);

/// <summary>Allowlisted data from a signature-verified Stripe event; no raw customer/card data.</summary>
public sealed record VerifiedStripeWebhook(
    string EventId,
    string EventType,
    bool LiveMode,
    Guid? PaymentIntentId,
    Guid? OrderId,
    Guid? TenantId,
    Guid? ConnectorId,
    string? SessionId,
    string? ProviderPaymentIntentId,
    string PayloadHash,
    bool Supported);
