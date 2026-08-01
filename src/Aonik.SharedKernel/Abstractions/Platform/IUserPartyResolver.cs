namespace Aonik.SharedKernel.Abstractions.Platform;

/// <summary>
/// Resolves the owner <c>Party</c> linked to an authenticated user (the <c>AnkUserParties</c>
/// bridge). Used to derive the document-search owner-party scope from authenticated context —
/// never from model/agent input (Spec 035 §14 / R7), so a prompt cannot widen its own retrieval
/// across parties within a tenant. Returns <c>null</c> when the user is not linked to a party
/// (e.g. an operator/admin), which fail-closed keeps a generic search tenant-wide (Public/Internal)
/// rather than surfacing any party's personal documents.
/// </summary>
public interface IUserPartyResolver
{
    Task<Guid?> GetPartyIdForUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The reverse: the user linked to a party, or null when the party has no login.
    /// </summary>
    /// <remarks>
    /// Added by Spec 086 for <c>IGroupService.AddMemberAsync</c>, whose whole safety rests on it.
    /// Direct addition exists for people who <em>cannot</em> consent; a party that has a user can,
    /// and must go through the invitation flow where their consent is recorded. Without a way to ask
    /// this question, direct addition becomes a way to put any adult in a group without asking them.
    /// Null therefore means "may be added directly", so this must fail closed if it cannot answer.
    /// </remarks>
    Task<Guid?> GetUserIdForPartyAsync(
        Guid tenantId,
        Guid partyId,
        CancellationToken cancellationToken = default);
}
