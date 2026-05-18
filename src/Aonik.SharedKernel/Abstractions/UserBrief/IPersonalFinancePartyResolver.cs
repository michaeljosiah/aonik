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
}
