namespace Aonik.SharedKernel.Abstractions.Documents;

/// <summary>
/// Cross-module write access to generic documents owned by <c>Aonik.Documents</c>.
/// Creating a document and uploading a file persists the blob + metadata and (for
/// indexable classifications) publishes <c>DocumentUploadedEvent</c> so the async
/// ingestion pipeline can make it searchable. Tenant scope is resolved from ambient context.
/// </summary>
public interface IDocumentWriter
{
    /// <summary>Creates a generic document record (no compliance usage required).</summary>
    Task<DocumentDto> CreateDocumentAsync(
        CreateDocumentCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a file's bytes into an existing document: stores the blob (tenant-scoped key,
    /// Sha256), persists the <c>DocumentFile</c>, and triggers indexing when the document's
    /// classification permits it.
    /// </summary>
    Task<DocumentFileDto> UploadFileAsync(
        UploadFileCommand command,
        Stream content,
        CancellationToken cancellationToken = default);
}
