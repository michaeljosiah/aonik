namespace Aonik.SharedKernel.Abstractions.Documents;

using Aonik.SharedKernel.Abstractions;

/// <summary>
/// Cross-module read access to generic documents owned by <c>Aonik.Documents</c>.
/// Consumers (notably Platform/Compliance, which references a document by id from
/// <c>DocumentUsage</c>) must not reference the Documents entity types directly;
/// they read through this contract instead. Tenant scope is enforced inside the
/// implementation via the ambient tenant provider.
/// See <a href="../../../docs/specifications/035.extract-documents-module.html">Spec 035 §11</a>.
/// </summary>
public interface IDocumentReader
{
    /// <summary>Returns a single document's metadata, or null if not found in the current tenant.</summary>
    Task<DocumentDto?> GetDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a paged, filtered list of documents in the current tenant.</summary>
    Task<PagedResult<DocumentListItem>> ListDocumentsAsync(
        ListDocumentsQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the files attached to a document, ordered by page index.</summary>
    Task<IReadOnlyList<DocumentFileDto>> GetFilesAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    /// <summary>Issues a time-limited signed URL to read a file's bytes from blob storage.</summary>
    Task<Uri> GetReadUrlAsync(
        Guid documentFileId,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);
}
