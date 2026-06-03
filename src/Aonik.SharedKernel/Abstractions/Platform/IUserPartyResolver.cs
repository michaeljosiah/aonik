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
}
