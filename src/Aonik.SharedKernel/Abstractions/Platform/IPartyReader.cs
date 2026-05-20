namespace Aonik.SharedKernel.Abstractions.Platform;

/// <summary>
/// Cross-module read access to Platform's Party + PartyRelationship aggregates.
/// PersonalFinance and other modules read party display names, statuses, and
/// relationships through this contract instead of depending on
/// <c>Aonik.Finance.Entities.PartyReadModel</c> (a transitional read projection)
/// or <c>Aonik.Platform.Entities.Party</c> directly.
/// See <a href="../../../../docs/specifications/027.extract-personal-finance-module.html">Spec 027</a>.
/// </summary>
public interface IPartyReader
{
    /// <summary>
    /// Returns parties matching the supplied identifiers, scoped to the tenant.
    /// </summary>
    Task<IReadOnlyList<PartyHistoryItem>> GetByIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> partyIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the active relationships for a party (where they are either
    /// the FromPartyId or ToPartyId). Scoped to the tenant.
    /// </summary>
    Task<IReadOnlyList<PartyRelationshipHistoryItem>> GetRelationshipsForPartyAsync(
        Guid tenantId,
        Guid partyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether a party exists in the given tenant. Used by graph
    /// node-type resolution where only existence (not the full record) matters.
    /// </summary>
    Task<bool> ExistsAsync(
        Guid tenantId,
        Guid partyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether an active relationship exists between two parties in either
    /// direction. Used by the FinancialLifeGraph canonical-relationship conflict
    /// guard and by node-type resolution for related-party lookups.
    /// </summary>
    Task<bool> HasActiveRelationshipBetweenAsync(
        Guid tenantId,
        Guid partyAId,
        Guid partyBId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Cross-module projection of a Party. Carries only what non-Platform consumers
/// actually read.
/// </summary>
public sealed record PartyHistoryItem(
    Guid PartyId,
    string DisplayName,
    string Status,
    string? CustomerTierCode);

/// <summary>
/// Cross-module projection of a PartyRelationship.
/// </summary>
public sealed record PartyRelationshipHistoryItem(
    Guid RelationshipId,
    Guid FromPartyId,
    Guid ToPartyId,
    string RelationshipTypeCode,
    bool IsActive,
    string? Notes);
