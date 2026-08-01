namespace Aonik.SharedKernel.Abstractions.UserBrief;

/// <summary>
/// Resolves a Party Id to the internal user identifier its personal-finance
/// data is scoped under, even when the party has no <c>UserParty</c> link
/// in the Platform identity tables.
///
/// This exists for the demo / playground impersonation flow: seeded
/// personas like <i>Seamus Keane</i> and <i>Mark Keane</i> only carry a
/// <c>PersonalProfile</c> row with a synthetic UserId — no real Auth0
/// user, no <c>UserParty</c> link. The Platform's primary party→user
/// resolver returns null for them and the playground User Brief picker
/// would otherwise 400 with "no user linked to this party".
///
/// Implemented in the Finance module against <c>PersonalProfile.PartyId</c>;
/// consumed by <c>ProjectUserBriefEndpoint</c> as a fallback when the
/// Platform-side <c>IUserBriefContextDataProvider.GetUserIdForPartyAsync</c>
/// returns null.
/// </summary>
public interface IPersonalFinancePartyResolver
{
    /// <summary>
    /// Returns the UserId that owns the <see cref="Aonik.Finance"/>
    /// PersonalProfile linked to <paramref name="partyId"/> in the given
    /// tenant, or <c>null</c> if no PersonalProfile exists for that party.
    /// </summary>
    Task<Guid?> GetUserIdForPartyAsync(
        Guid tenantId,
        Guid partyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The reverse: the party on the <c>PersonalProfile</c> of <paramref name="userId"/>, or null.
    /// </summary>
    /// <remarks>
    /// Added by Spec 086 P4, for the same seeded personas and the same reason. <c>GroupService</c>
    /// authorises by party, so a caller it cannot resolve is a caller who cannot create or manage a
    /// group at all — and on every seeded environment that is <em>every</em> caller, because the
    /// personas have a profile and no bridge row. Groups consumes this as an <b>optional</b>
    /// fallback after <c>IUserPartyResolver</c>, exactly as <c>ProjectUserBriefEndpoint</c> already
    /// does in the other direction.
    /// </remarks>
    Task<Guid?> GetPartyIdForUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
