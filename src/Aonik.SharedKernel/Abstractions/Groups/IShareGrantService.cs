namespace Aonik.SharedKernel.Abstractions.Groups;

/// <summary>
/// Scoped, revocable sharing of one party's records with another (Spec 086 / ADR-015), implemented
/// by <c>Aonik.Platform</c>.
///
/// The platform owns the <em>mechanics</em> — the grant lifecycle, the opaque single-use invite,
/// expiry, revocation — and knows nothing about what is being shared. Each module registers an
/// <see cref="IShareResourceResolver"/> for the kinds it owns.
/// </summary>
public interface IShareGrantService
{
    /// <summary>
    /// Grant access directly, when the member is already known.
    /// </summary>
    /// <remarks>
    /// Every named resource is resolved <b>under the owner</b> and the write is rejected unless the
    /// returned set matches the request exactly. Checking only that a resolver exists would let a
    /// caller persist another party's resource ids and then read them back through
    /// <see cref="IShareGrantReader"/> — sharing what you do not own.
    /// </remarks>
    /// <exception cref="InvalidStateException">No resolver is registered for the resource kind, or a named resource is not the owner's.</exception>
    Task<ShareGrantDto> CreateGrantAsync(CreateShareGrantCommand command, CancellationToken cancellationToken = default);

    /// <summary>Grants the current caller has given out.</summary>
    Task<IReadOnlyList<ShareGrantDto>> ListMineAsync(CancellationToken cancellationToken = default);

    /// <summary>Grants the current caller has received.</summary>
    Task<IReadOnlyList<ShareGrantDto>> ListSharedWithMeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Withdraw a grant. Takes effect immediately — the row is the record of truth, so there is no
    /// cached capability to outlive it. False when there was no such grant <em>of the caller's</em>.
    /// </summary>
    /// <remarks>
    /// A bool rather than an exception, deliberately: the caller's 404 must not distinguish "no such
    /// grant" from "not yours", or revocation becomes a way to probe which grant ids exist.
    /// </remarks>
    Task<bool> RevokeAsync(Guid grantId, CancellationToken cancellationToken = default);

    /// <summary>Mint an invite carrying the grant terms, materialised into a grant on accept.</summary>
    Task<ShareInviteDto> CreateInviteAsync(CreateShareInviteCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// What an invite discloses before acceptance, to someone not yet authenticated.
    /// </summary>
    /// <remarks>
    /// Anonymous by necessity — the recipient cannot sign in to something they have not joined — so
    /// it is rate-limited per caller and discloses only what the tenant has configured. Returns null
    /// for a token that is unknown, consumed, revoked or expired, all indistinguishably, so the
    /// endpoint cannot be used to probe which tokens exist.
    /// </remarks>
    Task<ShareInvitePreviewDto?> PreviewInviteAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accept an invite, materialising its grant for the current caller.
    /// </summary>
    /// <remarks>
    /// <para>Single-use: the token is consumed, so a leaked link cannot be replayed by anyone else.</para>
    /// <para>
    /// Returns a result rather than throwing, and that is a correction to Rev 1 of this contract. The
    /// distinctions here are load-bearing and an exception flattens them: a token replayed by the
    /// <b>same</b> member returns the grant they already hold (Spec 049's parked-token flow replays
    /// accept on a cold start), a token replayed by a <b>different</b> member is indistinguishable
    /// from an invalid one so it cannot be used as an oracle, and an owner tapping their own link is
    /// a conflict rather than a not-found. Modelling those as one <c>InvalidStateException</c> would
    /// have silently broken the mobile flow that depends on the first.
    /// </para>
    /// </remarks>
    Task<ShareInviteAcceptResult> AcceptInviteAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rescind an invite that has not been accepted. Idempotent — revoking an already-revoked invite
    /// succeeds, so a DELETE can be retried.
    /// </summary>
    /// <remarks>
    /// An <em>accepted</em> invite is a spent token whose access lives in the grant; the owner cuts
    /// that by revoking the grant, which cascades here. Refusing rather than silently succeeding is
    /// what stops an owner believing they have withdrawn access they still grant.
    /// </remarks>
    /// <exception cref="InvalidStateException">The invite is accepted or expired.</exception>
    Task<bool> RevokeInviteAsync(Guid inviteId, CancellationToken cancellationToken = default);
}

/// <summary>Why an accept did not produce a grant, when it did not.</summary>
public enum ShareInviteAcceptStatus
{
    /// <summary>Bound, or already bound by this same member. <see cref="ShareInviteAcceptResult.Grant"/> is set.</summary>
    Accepted,

    /// <summary>Invalid, expired, revoked, or already consumed by someone else — all alike, so this is no oracle.</summary>
    Invalid,

    /// <summary>The caller owns the invite. You cannot be a member of your own circle.</summary>
    SelfAccept,
}

/// <summary><see cref="Grant"/> is non-null if and only if <see cref="Status"/> is Accepted.</summary>
public sealed record ShareInviteAcceptResult(ShareInviteAcceptStatus Status, ShareGrantDto? Grant)
{
    public static ShareInviteAcceptResult FromGrant(ShareGrantDto grant) => new(ShareInviteAcceptStatus.Accepted, grant);

    public static readonly ShareInviteAcceptResult Invalid = new(ShareInviteAcceptStatus.Invalid, null);

    public static readonly ShareInviteAcceptResult SelfAccept = new(ShareInviteAcceptStatus.SelfAccept, null);
}

/// <summary>
/// The authorisation question a grant exists to answer (Spec 086 §9).
///
/// Separate from <see cref="IShareGrantService"/> because it is asked far more often and by code
/// that has no business creating or revoking anything.
/// </summary>
public interface IShareGrantReader
{
    /// <summary>
    /// Whether <paramref name="memberPartyId"/> may see this resource right now. Answerable without
    /// loading the grant, so an authorisation check costs one query rather than a projection.
    /// </summary>
    Task<bool> HasGrantAsync(
        Guid memberPartyId,
        string resourceKind,
        Guid resourceId,
        CancellationToken cancellationToken = default);

    /// <summary>Active grants a party has received for one resource kind — the list a shared-with-me view renders.</summary>
    Task<IReadOnlyList<ShareGrantDto>> GetActiveGrantsAsync(
        Guid memberPartyId,
        string resourceKind,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves the resources of one kind for the module that owns them (Spec 086 §6).
/// Module-contributed via <c>IEnumerable&lt;T&gt;</c> DI.
///
/// This is what keeps the platform out of every module's entities: a grant names a
/// <c>ResourceKind</c> and a list of ids, and only the registering module knows what those are.
/// </summary>
public interface IShareResourceResolver
{
    /// <summary>
    /// The kinds this resolver owns, e.g. <c>care-entity</c>. Two resolvers claiming one kind is a
    /// startup failure rather than last-writer-wins — ambiguity about who owns a resource is
    /// ambiguity about who may see it.
    /// </summary>
    IReadOnlyCollection<string> ResourceKinds { get; }

    /// <summary>
    /// Display refs for the given ids, <b>scoped to the owner</b>. Ids the owner does not own are
    /// omitted rather than reported, which is what lets grant creation detect them by comparing
    /// counts.
    /// </summary>
    Task<IReadOnlyList<ShareResourceRef>> ResolveAsync(
        string resourceKind,
        ShareResourceOwner owner,
        IReadOnlyCollection<Guid> resourceIds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Who owns the resources being resolved.
/// </summary>
/// <remarks>
/// <para>
/// Both identifiers, and at least one is always present. Party is the destination — it is what a
/// grant is keyed on, and what a member without a login can be. But sharing predates Spec 086 and
/// works today for users with no party link at all, so requiring one at the P5 cutover would take
/// the feature away from them.
/// </para>
/// <para>
/// The alternative was to skip validation when the owner has no party, and that is not an
/// alternative: unvalidated ids let a caller name another party's resources and read them back
/// through <see cref="IShareGrantReader"/>. Carrying both keys means ownership is <b>always</b>
/// checked, in whichever terms the owner actually has. <see cref="UserId"/> goes when the user
/// columns do.
/// </para>
/// </remarks>
public readonly record struct ShareResourceOwner(Guid? PartyId, Guid? UserId);
