namespace Aonik.SharedKernel.Abstractions.Groups;

// Spec 086 — the cross-module contract surface for group membership and scoped sharing.
// Platform owns the entities; these records are all any other module ever sees.

/// <summary>A group of people with roles — a household, a family (Spec 086 §4).</summary>
public sealed record GroupDto(
    Guid Id,
    string Kind,
    string Name,
    IReadOnlyList<GroupMemberDto> Members);

/// <summary>
/// One member of a group.
/// </summary>
/// <param name="PartyId">
/// <b>The member is a party, not a user.</b> That is the decision the whole spec turns on: a child
/// has no login, so requiring an authenticated principal would make them unrepresentable.
/// </param>
/// <param name="UserId">
/// The party's user, when it has one. Null for a member who cannot log in — consumers must tolerate
/// that rather than treating it as missing data.
/// </param>
public sealed record GroupMemberDto(
    Guid Id,
    Guid GroupId,
    Guid PartyId,
    Guid? UserId,
    string Role,
    string InvitationStatus,
    DateTime? InvitedAt,
    DateTime? RespondedAt);

/// <summary>
/// Scoped, revocable visibility of one party's records to another (Spec 086 §4).
/// </summary>
/// <param name="ResourceKind">
/// What sort of thing is shared. The platform stores this and never interprets it; the module that
/// registered a resolver for the kind is the only thing that can.
/// </param>
/// <param name="TermsJson">
/// Domain-specific terms the owning module wrote and is the only thing that reads. PersonalFinance
/// keeps its amount-redaction flag here rather than on a platform entity.
/// </param>
/// <param name="OwnerUserId">
/// The owner's user, through the Spec 086 transition. Present for the same reason
/// <see cref="GroupMemberDto.UserId"/> is: the deployed wire contract is user-keyed, the columns are
/// dual-written, and a DTO that dropped them would force every consumer to resolve party to user
/// per grant. Goes when the columns do.
/// </param>
public sealed record ShareGrantDto(
    Guid Id,
    Guid OwnerPartyId,
    Guid? MemberPartyId,
    Guid OwnerUserId,
    Guid? MemberUserId,
    Guid? GroupId,
    string Scope,
    string ResourceKind,
    IReadOnlyList<Guid> ResourceIds,
    string? TermsJson,
    string Status,
    DateTime CreatedAt);

/// <summary>
/// An opaque, single-use, expiring invitation to join someone's circle of access.
/// </summary>
/// <param name="Token">
/// A 256-bit cryptographically random bearer capability. No signature or MAC — the row is the
/// record of truth, which is why revocation is immediate and why the token is never re-derivable.
/// </param>
public sealed record ShareInviteDto(
    Guid Id,
    string Token,
    string Scope,
    string ResourceKind,
    IReadOnlyList<Guid> ResourceIds,
    string? TermsJson,
    string? Channel,
    DateTime ExpiresAt,
    string Status,
    DateTime? ConsumedAt,
    Guid? GrantId);

/// <summary>What an invite discloses before it is accepted, to someone not yet authenticated.</summary>
/// <param name="Resources">
/// Empty when the tenant has dialled disclosure back to counts — the recipient sees how much is
/// shared without learning what.
/// </param>
public sealed record ShareInvitePreviewDto(
    string OwnerDisplayName,
    string Scope,
    string? TermsJson,
    int ResourceCount,
    IReadOnlyList<ShareResourceRef> Resources,
    DateTime ExpiresAt);

/// <summary>A resource a grant names, resolved for display by its owning module.</summary>
public sealed record ShareResourceRef(Guid Id, string Kind, string DisplayName);

/// <summary>
/// A membership change a module may veto or react to (Spec 086 §7.1).
/// </summary>
/// <param name="MemberUserId">
/// Null for a party-only member. Contributors <b>must</b> tolerate that: a child has no
/// personal-finance profile to link, and skipping is correct rather than an error.
/// </param>
/// <param name="ActorUserId">
/// The acting user, when the actor has one. Added in P4: <c>HouseholdMemberRemovedEvent</c> has
/// always carried <c>removedByUserId</c>, and a contributor holding only <see cref="ActorPartyId"/>
/// cannot reproduce it — the transition would silently drop information the events it replaces
/// already published.
/// </param>
public sealed record GroupTransition(
    string Kind,
    Guid GroupId,
    Guid? MemberPartyId,
    Guid? MemberUserId,
    Guid ActorPartyId,
    Guid? ActorUserId = null);

/// <summary>Create a group.</summary>
public sealed record CreateGroupCommand(string Kind, string Name);

/// <summary>Invite someone who can consent. For a member who cannot, see <see cref="IGroupService.AddMemberAsync"/>.</summary>
/// <param name="PartyId">Who is being invited. Either this or <paramref name="UserId"/> is required.</param>
/// <param name="UserId">
/// The invitee's user. Rev 1 of this command named nobody at all — only an email and a phone — which
/// made it impossible to express the invitation PersonalFinance actually sends, where the invitee is
/// an existing user chosen by id. Both identifiers are accepted through the Spec 086 transition;
/// <paramref name="UserId"/> goes when the user columns do.
/// </param>
/// <param name="Email">Delivery hint only. The platform does not send anything.</param>
public sealed record InviteGroupMemberCommand(
    Guid GroupId,
    string Role,
    Guid? PartyId = null,
    Guid? UserId = null,
    string? Email = null,
    string? Phone = null);

/// <summary>Create a grant directly, when the member is already known.</summary>
/// <param name="MemberUserId">
/// The member's user, through the Spec 086 transition. Carried rather than reverse-resolved from
/// <paramref name="MemberPartyId"/>: a member who has a profile but no <c>AnkUserParties</c> row
/// cannot be resolved back, and the grant would then be written with a null member — a dangling
/// pending grant instead of the active one the caller asked for. Goes with the columns.
/// </param>
public sealed record CreateShareGrantCommand(
    string Scope,
    string ResourceKind,
    IReadOnlyList<Guid> ResourceIds,
    Guid? MemberPartyId = null,
    Guid? MemberUserId = null,
    Guid? GroupId = null,
    string? TermsJson = null);

/// <summary>Mint an invite carrying the grant terms, to be materialised on accept.</summary>
public sealed record CreateShareInviteCommand(
    string Scope,
    string ResourceKind,
    IReadOnlyList<Guid> ResourceIds,
    string? TermsJson = null,
    string? Channel = null,
    TimeSpan? ValidFor = null);
