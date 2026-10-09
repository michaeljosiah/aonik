namespace Aonik.Finance.Contracts.Services.Payments;

/// <summary>Immutable server-authorized cash return to the original bound payment.</summary>
public sealed record PaymentProviderRefundRequest(
    Guid RefundId, Guid PaymentIntentId, Guid OrderId, Guid ConnectorId,
    string ProviderAccountId, bool LiveMode, string ProviderPaymentIntentId,
    decimal Amount, string Currency, string IdempotencyKey);

/// <summary>Allowlisted current provider evidence; amount is the external cash portion only.</summary>
public sealed record PaymentProviderRefundSnapshot(
    Guid TenantId, Guid RefundId, Guid PaymentIntentId, Guid OrderId, Guid ConnectorId,
    string ProviderAccountId, bool LiveMode, string ProviderRefundId,
    string ProviderPaymentIntentId, string ProviderChargeId, decimal Amount, string Currency,
    string Status, DateTime CreatedAtUtc, string? FailureCode = null,
    string? BalanceTransactionId = null, string? FailureBalanceTransactionId = null);

public sealed record PaymentProviderRefundObservation(
    string ProviderRefundId, decimal Amount, string Currency, string Status, Guid? LocalRefundId);

/// <summary>Complete bounded enumeration. An incomplete provider read throws rather than inventing available funds.</summary>
public sealed record PaymentProviderRefundBudget(
    decimal CapturedAmount, string Currency, IReadOnlyList<PaymentProviderRefundObservation> Refunds);
