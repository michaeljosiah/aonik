using Aonik.SharedKernel.Abstractions.GiftCards;

namespace Aonik.SharedKernel.Events.Integration;

/// <summary>Committed issuance reference only; no bearer code or recipient details in the outbox.</summary>
public sealed record GiftCardIssuedEvent(Guid TenantId, Guid GiftCardId, GiftCardPurchaseSource Source) : ITenantScopedEvent;
