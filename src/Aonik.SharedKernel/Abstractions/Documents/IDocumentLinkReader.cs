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

    /// <summary>Linked-document counts per CareEntity, for grid badges. Entities with no links are omitted.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountForEntitiesAsync(
        IReadOnlyList<Guid> careEntityIds,
        CancellationToken cancellationToken = default);
}
