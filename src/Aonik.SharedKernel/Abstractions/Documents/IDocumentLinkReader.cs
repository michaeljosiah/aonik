namespace Aonik.SharedKernel.Abstractions.Documents;

/// <summary>
/// Cross-module read access to document links (Spec 046) — lets PersonalFinance
/// (the CareEntity profile, Spec 043 §8) and the circle gate (Spec 048) list
/// "documents for entity X" without a ProjectReference to the Documents module,
/// mirroring the <c>SharedKernel.Abstractions.Finance</c> read-contract pattern
/// (ADR-006). Results are owner-scoped to the current caller and amount-free.
/// </summary>
public interface IDocumentLinkReader
{
    /// <summary>Document refs linked to a target (careEntity | paymentLog | commitment) — never bytes.</summary>
    Task<IReadOnlyList<DocumentRef>> GetForTargetAsync(
        string targetType,
        Guid targetId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Document refs linked to a target, scoped to a SPECIFIC owner (by user id) rather than
    /// the current caller — for the Circle shared view (Spec 048), where a member views the
    /// owner's entity. The caller MUST have already authorised access (an active Circle grant);
    /// this method does not itself check the grant. Returns only the owner's documents, refs
    /// only (no bytes, no amounts).
    /// </summary>
    Task<IReadOnlyList<DocumentRef>> GetForOwnerTargetAsync(
        Guid ownerUserId,
        string targetType,
        Guid targetId,
        CancellationToken cancellationToken = default);

    /// <summary>Linked-document counts per CareEntity, for grid badges. Entities with no links are omitted.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountForEntitiesAsync(
        IReadOnlyList<Guid> careEntityIds,
        CancellationToken cancellationToken = default);
}
