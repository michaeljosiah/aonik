namespace Aonik.SharedKernel.Events.Integration;

// ── Group-originated integration events (Spec 086 §12) ──────────────────────
//
// Moved out of FinanceEvents.cs, where group membership had ended up only because
// PersonalFinance happened to own it. Two sets live here, and the division matters.

// ── Legacy: user-keyed, published by the PersonalFinance facade ──────────────
//
// Record names and payload shapes are UNCHANGED by the move. Renaming a published event contract is
// a separate decision with subscriber impact, and there is no reason to bundle it with a relocation.
// These are emitted only when the member HAS a user, which preserves today's contract exactly.

public record HouseholdCreatedEvent(
    Guid TenantId,
    Guid HouseholdId,
    Guid OwnerUserId) : ITenantScopedEvent;

public record HouseholdMemberInvitedEvent(
    Guid TenantId,
    Guid HouseholdId,
    Guid InvitedUserId,
    Guid InvitedByUserId,
    string Role) : ITenantScopedEvent;

public record HouseholdInvitationAcceptedEvent(
    Guid TenantId,
    Guid HouseholdId,
    Guid UserId) : ITenantScopedEvent;

public record HouseholdInvitationDeclinedEvent(
    Guid TenantId,
    Guid HouseholdId,
    Guid UserId) : ITenantScopedEvent;

public record HouseholdMemberRemovedEvent(
    Guid TenantId,
    Guid HouseholdId,
    Guid UserId,
    Guid RemovedByUserId) : ITenantScopedEvent;

public record HouseholdMemberLeftEvent(
    Guid TenantId,
    Guid HouseholdId,
    Guid UserId) : ITenantScopedEvent;

public record HouseholdOwnershipTransferredEvent(
    Guid TenantId,
    Guid HouseholdId,
    Guid PreviousOwnerUserId,
    Guid NewOwnerUserId) : ITenantScopedEvent;

// ── Generic: party-keyed, published by the group service ─────────────────────
//
// Additive, not a rename, and the reason is not tidiness: EVERY legacy event above carries a
// UserId, and a party-only member has no user. Adding, removing or transferring ownership to a
// child therefore has no legal legacy payload to publish — the old contracts literally cannot
// describe the lifecycle Spec 086 introduces.
//
// A user-backed change emits BOTH: the generic event for new consumers, the legacy one for existing
// subscribers. That duplication is the price of not breaking a live subscriber, and it ends when the
// legacy set is retired.

/// <param name="Kind">One of <c>GroupKinds</c> — <c>household</c>, <c>family</c>, … .</param>
public record GroupCreatedEvent(
    Guid TenantId,
    Guid GroupId,
    string Kind,
    Guid OwnerPartyId,
    Guid? OwnerUserId) : ITenantScopedEvent;

/// <param name="MemberUserId">Null for a member who cannot log in. Subscribers must tolerate that.</param>
public record GroupMemberAddedEvent(
    Guid TenantId,
    Guid GroupId,
    Guid MemberPartyId,
    Guid? MemberUserId,
    string Role,
    Guid ActorPartyId) : ITenantScopedEvent;

public record GroupMemberInvitedEvent(
    Guid TenantId,
    Guid GroupId,
    Guid MemberPartyId,
    Guid? MemberUserId,
    string Role,
    Guid ActorPartyId) : ITenantScopedEvent;

public record GroupInvitationAcceptedEvent(
    Guid TenantId,
    Guid GroupId,
    Guid MemberPartyId,
    Guid? MemberUserId) : ITenantScopedEvent;

public record GroupMemberRemovedEvent(
    Guid TenantId,
    Guid GroupId,
    Guid MemberPartyId,
    Guid? MemberUserId,
    Guid ActorPartyId) : ITenantScopedEvent;

public record GroupOwnershipTransferredEvent(
    Guid TenantId,
    Guid GroupId,
    Guid FromPartyId,
    Guid ToPartyId) : ITenantScopedEvent;
