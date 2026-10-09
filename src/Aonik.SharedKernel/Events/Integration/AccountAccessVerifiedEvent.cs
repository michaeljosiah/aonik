namespace Aonik.SharedKernel.Events.Integration;

/// <summary>Platform verified and consumed the paid guest's account-access action.</summary>
public sealed record AccountAccessVerifiedEvent(
    Guid TenantId,
    Guid ActionId,
    Guid CartId,
    Guid OrderId,
    Guid PaymentIntentId,
    Guid GuestPartyId,
    Guid AccountPartyId) : ITenantScopedEvent;
