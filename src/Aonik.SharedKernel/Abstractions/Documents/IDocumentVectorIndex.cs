namespace Aonik.SharedKernel.Abstractions.Documents;

/// <summary>
/// Write side of the document vector store: indexing a document's chunks and purging
/// them on deletion. Implemented in <c>Aonik.Infrastructure</c> over the shared vector
/// store, which already stamps and filters <c>tenant_id</c> fail-closed; this adapter
/// adds <c>owner_party_id</c> / <c>classification</c> / <c>purpose</c> scoping so that,
/// within a tenant, one party's documents are not retrievable by another party's agent.
/// </summary>
public interface IDocumentVectorIndex
{
    /// <summary>
    /// Embeds and upserts the supplied chunks, stamping the scope fields on every vector.
    /// Returns the number of chunks indexed plus the embedding model and cost estimate so the
    /// caller can record them on its ingestion audit row (Spec 035 §9).
    /// </summary>
    Task<DocumentIndexResult> IndexDocumentAsync(
        DocumentIndexRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all vectors for a document (right-to-erasure / re-index). Scoped to the
    /// current tenant by the vector store. Returns the number of vectors removed.
    /// </summary>
    Task<int> PurgeDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);
}
