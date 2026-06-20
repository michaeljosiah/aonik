using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Finance.Contracts.Services.PersonalFinance;

/// <summary>
/// The Circle (Spec 048): entity-scoped sharing grants, link invites, and the
/// member's scoped read of an owner's records. All reads pass through the single
/// fail-closed visibility filter (<see cref="ICircleVisibility"/>); no grant →
/// no access (404, existence not revealed).
/// </summary>
public interface ICircleService
{
    Task<CircleGrantResponse> CreateGrantAsync(CreateCircleGrantRequest request, CancellationToken cancellationToken = default);

    /// <summary>Grants the current user (owner) has issued — "Shared with".</summary>
    Task<IReadOnlyList<CircleGrantResponse>> ListGrantsForOwnerAsync(CancellationToken cancellationToken = default);

    /// <summary>Grants where the current user is the member — "Can see".</summary>
    Task<IReadOnlyList<CircleGrantResponse>> ListGrantsForMemberAsync(CancellationToken cancellationToken = default);

    /// <summary>Revokes a grant the current user owns (effective immediately). False if not owned.</summary>
    Task<bool> RevokeGrantAsync(Guid grantId, CancellationToken cancellationToken = default);

    Task<CircleInviteResponse> CreateInviteAsync(CreateCircleInviteRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// The current (authenticated) user accepts an invite token, becoming the
    /// member of a newly materialised active grant. Returns null if the token is
    /// invalid, expired, or already consumed. (Unregistered-invitee registration +
    /// OTP provisioning is a Spec 001 follow-up; accept requires an authenticated user.)
    /// </summary>
    Task<CircleGrantResponse?> AcceptInviteAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>The entities the current member may see of an owner, per the grant. Null if no active grant.</summary>
    Task<IReadOnlyList<CareEntityRef>?> ListSharedEntitiesAsync(Guid ownerUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The current member's scoped view of one of the owner's entities — full
    /// (amounts) for scope=all|entities, or the amount-free docs-only view for
    /// scope=docsOnly. Null if no grant, the entity is out of scope, or not found.
    /// </summary>
    Task<CircleSharedEntityResult?> GetSharedEntityAsync(Guid ownerUserId, Guid careEntityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The current member's paged list of an owner's expenses for one shared entity — the full
    /// list behind <see cref="GetSharedEntityAsync"/>'s recent-log preview, newest first, each row
    /// carrying its corroboration status. Null (→ 404) when no grant covers the entity, the entity
    /// is not found, or amounts are not permitted (docsOnly / NoAmounts) — fail-closed, the
    /// no-amounts property is preserved and the existence of spend is not revealed.
    /// </summary>
    Task<CircleSharedPaymentLogsResult?> GetSharedPaymentLogsAsync(
        Guid ownerUserId, Guid careEntityId, int page, int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>
/// The single, fail-closed visibility resolver (Spec 048 §5) — the one place the
/// no-amounts security property lives. Every shared read resolves this first.
/// </summary>
public interface ICircleVisibility
{
    /// <summary>The active grant for (current member → owner), or null if none.</summary>
    Task<CircleGrantView?> ResolveAsync(Guid ownerUserId, CancellationToken cancellationToken = default);
}

/// <summary>The Support Statement compose projection (Spec 048 §9) — the owner's own entity.</summary>
public interface ISupportStatementService
{
    /// <summary>Composes a date-ranged statement for the caller's own entity. Null if not owned.</summary>
    Task<StatementData?> ComposeAsync(
        Guid careEntityId,
        DateTime from,
        DateTime to,
        string? preparedFor,
        CancellationToken cancellationToken = default);
}
