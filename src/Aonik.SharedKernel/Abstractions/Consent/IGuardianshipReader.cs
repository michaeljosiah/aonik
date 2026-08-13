namespace Aonik.SharedKernel.Abstractions.Consent;

/// <summary>
/// "Does this party hold active guardian authority over that one?" (Spec 095 §12).
///
/// One query, callable by any module on an authorisation path — which is why it lives in
/// SharedKernel rather than behind an HTTP call to Platform.
/// </summary>
public interface IGuardianshipReader
{
    /// <summary>
    /// True when <paramref name="guardianPartyId"/> holds an <em>active</em> Guardian edge to
    /// <paramref name="childPartyId"/>. Kinship alone never satisfies this.
    /// </summary>
    Task<bool> HasAuthorityAsync(
        Guid tenantId,
        Guid guardianPartyId,
        Guid childPartyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every party holding active guardian authority over this child. A child may have several, each
    /// able to act independently — families are not uniform, and a design admitting only one is wrong
    /// about most of them.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetGuardiansAsync(
        Guid tenantId,
        Guid childPartyId,
        CancellationToken cancellationToken = default);

    /// <summary>Children this party may act for. Used to scope listings, never to grant access.</summary>
    Task<IReadOnlyList<Guid>> GetWardsAsync(
        Guid tenantId,
        Guid guardianPartyId,
        CancellationToken cancellationToken = default);
}
