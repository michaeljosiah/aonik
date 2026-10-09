namespace Aonik.SharedKernel.Events.Integration;

/// <summary>References a durably accepted, signature-verified provider inbox entry.</summary>
public sealed record CheckoutPaymentReconciliationRequestedEvent(
    Guid TenantId,
    Guid PaymentIntentId,
    Guid WebhookEventId) : ITenantScopedEvent;
