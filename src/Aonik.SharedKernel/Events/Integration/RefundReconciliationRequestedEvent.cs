namespace Aonik.SharedKernel.Events.Integration;

/// <summary>Recovery of an already authorized refund; never authorizes another money movement.</summary>
public sealed record RefundReconciliationRequestedEvent(
    Guid TenantId, Guid RefundId, Guid? WebhookEventId = null) : ITenantScopedEvent;
