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
    /// cached capability to outlive it.
    /// </summary>
    Task RevokeAsync(Guid grantId, CancellationToken cancellationToken = default);

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
    /// <remarks>Single-use: the token is consumed, so a leaked link cannot be replayed.</remarks>
    /// <exception cref="InvalidStateException">The invite is consumed, revoked, expired, or belongs to the caller.</exception>
    Task<ShareGrantDto> AcceptInviteAsync(string token, CancellationToken cancellationToken = default);
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
        Guid ownerPartyId,
        IReadOnlyCollection<Guid> resourceIds,
        CancellationToken cancellationToken = default);
}
